using System;
using System.Collections.Generic;
using System.Text;

namespace MQTTnet.Server
{
    public sealed class RemovePersistedSessionMessageEventArgs : EventArgs
    {
        public RemovePersistedSessionMessageEventArgs(object persistedMessageKey)
        {
            PersistedMessageKey = persistedMessageKey;
        }

        public object PersistedMessageKey { get; private set; }
    }
}
