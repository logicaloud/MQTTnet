using System;

namespace MQTTnet.Server
{
    public sealed class RemovePersistedSessionEventArgs : EventArgs
    {
        public RemovePersistedSessionEventArgs(string clientId)
        {
            ClientId = clientId;
        }

        public string ClientId { get; private set; }
    }
}
