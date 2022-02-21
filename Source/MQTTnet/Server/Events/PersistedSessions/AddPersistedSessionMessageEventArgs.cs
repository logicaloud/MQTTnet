using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class AddPersistedSessionMessageEventArgs : EventArgs
    {
        public AddPersistedSessionMessageEventArgs(MqttApplicationMessage applicationMessage, List<MqttPersistedApplicationMessageClient> messageClients)
        {
            ApplicationMessage = applicationMessage;
            MessageClients = messageClients;
        }

        public MqttApplicationMessage ApplicationMessage { get; private set; }
        
        public List<MqttPersistedApplicationMessageClient> MessageClients { get; private set; }

        /// <summary>
        /// To be set by event handler as a storage reference
        /// </summary>
        public object PersistedMessageKey { get; set; }
    }
}
