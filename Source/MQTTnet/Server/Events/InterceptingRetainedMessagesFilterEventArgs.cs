using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class InterceptingRetainedMessagesFilterEventArgs : EventArgs
    {
        /// <summary>
        /// Event invoked by the server when retained messages need to be retrieved
        /// </summary>
        /// <param name="filterTopic">The topic that retained messages are requested for</param>
        /// <param name="grantedQualityOfServiceLevel">The quality of service level granted for the subscription</param>
        public InterceptingRetainedMessagesFilterEventArgs(string filterTopic, MqttQualityOfServiceLevel grantedQualityOfServiceLevel)
        {
            FilterTopic = filterTopic;
            GrantedQualityOfServiceLevel = grantedQualityOfServiceLevel;
        }

        /// <summary>
        /// The topic that retained messages are requested for
        /// </summary>
        public string FilterTopic { get; }

        /// <summary>
        /// The quality of service level granted for the subscription
        /// </summary>
        public MqttQualityOfServiceLevel GrantedQualityOfServiceLevel { get; }

        /// <summary>
        /// Filtered messages returned by the handler. The handler may return Null.
        /// </summary>
        public IList<MqttRetainedMessageMatch> FilteredMessages { get; set; }
    }
}
