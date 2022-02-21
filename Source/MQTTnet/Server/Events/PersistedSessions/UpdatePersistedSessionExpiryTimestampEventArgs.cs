using System;

namespace MQTTnet.Server
{
    public sealed class UpdatePersistedSessionExpiryTimestampEventArgs : EventArgs
    {
        public UpdatePersistedSessionExpiryTimestampEventArgs(string clientId, DateTime sessionExpiryTimestamp)
        {
            ClientId = clientId;
            SessionExpiryTimestamp = sessionExpiryTimestamp;
        }

        public string ClientId { get; private set; }

        public DateTime SessionExpiryTimestamp { get; private set; }
    }
}
