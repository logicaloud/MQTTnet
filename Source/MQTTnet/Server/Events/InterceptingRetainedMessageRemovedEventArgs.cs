using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class InterceptingRetainedMessageRemovedEventArgs : EventArgs
    {
        public InterceptingRetainedMessageRemovedEventArgs(string clientId, MqttApplicationMessage applicationMessage)
        {
            ClientId = clientId;
            RemovedMessage = applicationMessage;
        }
        /// <summary>
        /// Changed message
        /// </summary>
        public MqttApplicationMessage RemovedMessage { get; }

        /// <summary>
        /// Client that changed the message
        /// </summary>
        public string ClientId { get; }

    }
}
