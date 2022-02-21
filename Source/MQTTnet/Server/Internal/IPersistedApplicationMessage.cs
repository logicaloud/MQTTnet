using MQTTnet.Packets;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    /// <summary>
    /// Interface returned by the Persisted Session Manager
    /// </summary>
    public interface IPersistedApplicationMessage
    {
        MqttApplicationMessage ApplicationMessage { get; }
        
        Protocol.MqttQualityOfServiceLevel CheckedQualityOfServiceLevel { get; }

        List<uint> CheckedSubscriptionIdentifiers { get; }

        object PersistedMessageKey { get; }
    }
}