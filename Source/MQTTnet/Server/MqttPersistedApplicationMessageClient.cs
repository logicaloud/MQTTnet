using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    /// <summary>
    /// Holds Client ID and Publish parameters for a persisted message
    /// </summary>
    public class MqttPersistedApplicationMessageClient
    {
        public MqttPersistedApplicationMessageClient(
            string recipientClientId, 
            Protocol.MqttQualityOfServiceLevel grantedQualityOfServiceLevel, 
            List<uint> subscriptionIdentifiers
            )
        {
            RecipientClientId = recipientClientId;
            CheckedQualityOfServiceLevel = grantedQualityOfServiceLevel;
            CheckedSubscriptionIdentifiers = subscriptionIdentifiers;
        }

        public string RecipientClientId { get; }

        public Protocol.MqttQualityOfServiceLevel CheckedQualityOfServiceLevel { get; }

        public List<uint> CheckedSubscriptionIdentifiers { get; }

    }
}
