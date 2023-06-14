using MQTTnet.Protocol;

namespace MQTTnet.Server
{
    public class MqttRetainedMessageMatchFactory
    {
        public static MqttRetainedMessageMatch CreateMatchingRetainedMessage(MqttApplicationMessage retainedMessage, MqttQualityOfServiceLevel grantedQualityOfServiceLevel)
        {
            var retainedMessageMatch = new MqttRetainedMessageMatch(retainedMessage, grantedQualityOfServiceLevel);
            if (retainedMessageMatch.SubscriptionQualityOfServiceLevel > retainedMessageMatch.ApplicationMessage.QualityOfServiceLevel)
            {
                // UPGRADING the QoS is not allowed! 
                // From MQTT spec: Subscribing to a Topic Filter at QoS 2 is equivalent to saying
                // "I would like to receive Messages matching this filter at the QoS with which they were published".
                // This means a publisher is responsible for determining the maximum QoS a Message can be delivered at,
                // but a subscriber is able to require that the Server downgrades the QoS to one more suitable for its usage.
                retainedMessageMatch.SubscriptionQualityOfServiceLevel = retainedMessageMatch.ApplicationMessage.QualityOfServiceLevel;
            }

            return retainedMessageMatch;
        }
    }
}
