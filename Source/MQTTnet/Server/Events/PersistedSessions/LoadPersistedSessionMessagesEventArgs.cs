using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    public sealed class LoadPersistedSessionMessagesEventArgs : EventArgs
    {
        public LoadPersistedSessionMessagesEventArgs(string clientId)
        {
            ClientId = clientId;
        }

        public string ClientId { get; }

        public List<IPersistedApplicationMessage> PersistedMessages { get; set; } = new List<IPersistedApplicationMessage>();
    }
}
