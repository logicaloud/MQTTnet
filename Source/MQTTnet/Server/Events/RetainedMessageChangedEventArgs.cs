// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace MQTTnet.Server
{
    public sealed class RetainedMessageChangedEventArgs : EventArgs
    {
        public enum RetainedMessageChangeType { Add, Remove, Replace };

        public RetainedMessageChangedEventArgs(string clientId, RetainedMessageChangeType changeType, MqttApplicationMessage changedMessage)
        {
            ClientId = clientId;
            ChangeType = changeType;
            ChangedRetainedMessage = changedMessage;
        }

        public string ClientId { get; }

        public RetainedMessageChangeType ChangeType { get; }

        public MqttApplicationMessage ChangedRetainedMessage { get; }
    }
}