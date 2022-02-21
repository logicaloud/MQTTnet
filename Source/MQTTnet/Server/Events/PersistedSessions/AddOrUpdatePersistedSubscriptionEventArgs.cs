using System;

namespace MQTTnet.Server
{
    public sealed class AddOrUpdatePersistedSubscriptionEventArgs : EventArgs
    {
        public AddOrUpdatePersistedSubscriptionEventArgs(string clientId, Packets.MqttTopicFilter topicFilter)
        {
            ClientId = clientId;
            TopicFilter = topicFilter;
        }

        public string ClientId { get; private set; }
        public Packets.MqttTopicFilter TopicFilter { get; private set; }
    }
}
