using MQTTnet.Packets;
using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    /// <summary>
    /// Interface returned by the persisted session manager
    /// </summary>
    public interface IPersistedSession
    {
        string ClientId { get; }

        MqttApplicationMessage WillMessage { get; }

        uint WillDelayInterval { get; }

        uint SessionExpiryInterval { get; }

        IDictionary<string, MqttTopicFilter> SubscriptionsByTopic { get; }
    }
}
