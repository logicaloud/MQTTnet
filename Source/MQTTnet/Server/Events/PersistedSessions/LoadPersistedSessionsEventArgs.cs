using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    public sealed class LoadPersistedSessionsEventArgs : EventArgs
    {
        /// <summary>
        /// To be set by event handler
        /// </summary>
        public List<IPersistedSession> PersistedSessions { get; set; } = new List<IPersistedSession>();
    }
}
