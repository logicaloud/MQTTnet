using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    public sealed class RetainedMessageRemovedEventArgs : EventArgs
    {
        public RetainedMessageRemovedEventArgs(string topic)
        {
            Topic = topic;
        }

        public string Topic { get; private set; }
    }
}