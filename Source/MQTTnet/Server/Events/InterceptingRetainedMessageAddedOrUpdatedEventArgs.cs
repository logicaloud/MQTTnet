using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class InterceptingRetainedMessageAddedOrUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// Event invoked by the server to notify that an retained application message has changed
        /// </summary>
        /// <param name="clientId">Client that changed the message</param>
        /// <param name="changedRetainedMessage">Changed message</param>
        /// <exception cref="ArgumentNullException"></exception>
        public InterceptingRetainedMessageAddedOrUpdatedEventArgs(string clientId, MqttApplicationMessage changedRetainedMessage)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            ChangedRetainedMessage = changedRetainedMessage ?? throw new ArgumentNullException(nameof(changedRetainedMessage));
        }

        /// <summary>
        /// Changed message
        /// </summary>
        public MqttApplicationMessage ChangedRetainedMessage { get; }

        /// <summary>
        /// Client that changed the message
        /// </summary>
        public string ClientId { get; }
    }
}
