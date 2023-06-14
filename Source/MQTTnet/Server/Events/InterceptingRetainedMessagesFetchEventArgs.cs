using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class InterceptingRetainedMessagesFetchEventArgs : EventArgs
    {
        /// <summary>
        /// Event invoke by the server to fetch one or more retained messages
        /// </summary>
        /// <param name="topicFilter">Topic to fetch the message for or null if all messages should be retrieved</param>
        public InterceptingRetainedMessagesFetchEventArgs(string topicFilter)
        {
            TopicFilter = topicFilter;
        }

        /// <summary>
        /// Topic to fetch the message for or null if all messages should be retrieved
        /// </summary>
        public string TopicFilter { get; }

        /// <summary>
        /// Application messages returned by the event handler or null
        /// </summary>
        public IList<MqttApplicationMessage> ApplicationMessages { get; set; }
    }
}
