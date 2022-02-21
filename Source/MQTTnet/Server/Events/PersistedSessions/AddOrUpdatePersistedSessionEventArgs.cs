using MQTTnet.Packets;
using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    public sealed class AddOrUpdatePersistedSessionEventArgs : EventArgs
    {
        public AddOrUpdatePersistedSessionEventArgs(
            string clientId,
            MqttApplicationMessage willMessage,
            uint willDelayInterval,
            uint sessionExpiryInterval
            )
        {
            ClientId = clientId;
            WillMessage = willMessage;
            WillDelayInterval = willDelayInterval;
            SessionExpiryInterval = sessionExpiryInterval;
        }

        public string ClientId { get; private set; }
        public MqttApplicationMessage WillMessage { get; private set; }
        public uint WillDelayInterval { get; private set; }
        public uint SessionExpiryInterval { get; private set; }
    }
}
