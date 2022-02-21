using System;

namespace MQTTnet.Server
{
    public sealed class RemovePersistedSubscriptionEventArgs : EventArgs
    {
        public RemovePersistedSubscriptionEventArgs(string clientId, string topic)
        {
            ClientId = clientId;
            Topic = topic;
        }

        public string ClientId { get; private set; }
        public string Topic { get; private set; }
    }
}
