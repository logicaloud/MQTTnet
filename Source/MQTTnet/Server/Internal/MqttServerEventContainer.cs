// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MQTTnet.Internal;

namespace MQTTnet.Server
{
    public sealed class MqttServerEventContainer
    {
        public AsyncEvent<InterceptingSubscriptionEventArgs> InterceptingSubscriptionEvent { get; } = new AsyncEvent<InterceptingSubscriptionEventArgs>();

        public AsyncEvent<InterceptingUnsubscriptionEventArgs> InterceptingUnsubscriptionEvent { get; } = new AsyncEvent<InterceptingUnsubscriptionEventArgs>();

        public AsyncEvent<InterceptingPublishEventArgs> InterceptingPublishEvent { get; } = new AsyncEvent<InterceptingPublishEventArgs>();

        public AsyncEvent<ValidatingConnectionEventArgs> ValidatingConnectionEvent { get; } = new AsyncEvent<ValidatingConnectionEventArgs>();

        public AsyncEvent<ClientConnectedEventArgs> ClientConnectedEvent { get; } = new AsyncEvent<ClientConnectedEventArgs>();

        public AsyncEvent<ClientDisconnectedEventArgs> ClientDisconnectedEvent { get; } = new AsyncEvent<ClientDisconnectedEventArgs>();

        public AsyncEvent<ClientSubscribedTopicEventArgs> ClientSubscribedTopicEvent { get; } = new AsyncEvent<ClientSubscribedTopicEventArgs>();

        public AsyncEvent<ClientUnsubscribedTopicEventArgs> ClientUnsubscribedTopicEvent { get; } = new AsyncEvent<ClientUnsubscribedTopicEventArgs>();

        public AsyncEvent<EventArgs> PreparingSessionEvent { get; } = new AsyncEvent<EventArgs>();

        public AsyncEvent<SessionDeletedEventArgs> SessionDeletedEvent { get; } = new AsyncEvent<SessionDeletedEventArgs>();

        public AsyncEvent<ApplicationMessageNotConsumedEventArgs> ApplicationMessageNotConsumedEvent { get; } = new AsyncEvent<ApplicationMessageNotConsumedEventArgs>();

        public AsyncEvent<RetainedMessageChangedEventArgs> RetainedMessageChangedEvent { get; } = new AsyncEvent<RetainedMessageChangedEventArgs>();

        public AsyncEvent<RetainedMessageRemovedEventArgs> RetainedMessageRemovedEvent { get; } = new AsyncEvent<RetainedMessageRemovedEventArgs>();

        public AsyncEvent<LoadingRetainedMessagesEventArgs> LoadingRetainedMessagesEvent { get; } = new AsyncEvent<LoadingRetainedMessagesEventArgs>();

        public AsyncEvent<EventArgs> RetainedMessagesClearedEvent { get; } = new AsyncEvent<EventArgs>();

        public AsyncEvent<InterceptingPacketEventArgs> InterceptingInboundPacketEvent { get; } = new AsyncEvent<InterceptingPacketEventArgs>();

        public AsyncEvent<InterceptingPacketEventArgs> InterceptingOutboundPacketEvent { get; } = new AsyncEvent<InterceptingPacketEventArgs>();

        public AsyncEvent<EventArgs> StartedEvent { get; } = new AsyncEvent<EventArgs>();

        public AsyncEvent<EventArgs> StoppedEvent { get; } = new AsyncEvent<EventArgs>();


        #region Persisted Sessions

        public AsyncEvent<LoadPersistedSessionsEventArgs> LoadPersistedSessionsEvent { get; } = new AsyncEvent<LoadPersistedSessionsEventArgs>();

        public AsyncEvent<AddOrUpdatePersistedSessionEventArgs> AddOrUpdatePersistedSessionEvent { get; } = new AsyncEvent<AddOrUpdatePersistedSessionEventArgs>();

        public AsyncEvent<RemovePersistedSessionEventArgs> RemovePersistedSessionEvent { get; } = new AsyncEvent<RemovePersistedSessionEventArgs>();

        public AsyncEvent<UpdatePersistedSessionExpiryTimestampEventArgs> UpdatePersistedSessionExpiryTimestampEvent { get; } = new AsyncEvent<UpdatePersistedSessionExpiryTimestampEventArgs>();

        public AsyncEvent<AddOrUpdatePersistedSubscriptionEventArgs> AddOrUpdatePersistedSubscriptionEvent { get; } = new AsyncEvent<AddOrUpdatePersistedSubscriptionEventArgs>();

        public AsyncEvent<RemovePersistedSubscriptionEventArgs> RemovePersistedSubscriptionEvent { get; } = new AsyncEvent<RemovePersistedSubscriptionEventArgs>();

        public AsyncEvent<LoadPersistedSessionMessagesEventArgs> LoadPersistedSessionMessagesEvent { get; } = new AsyncEvent<LoadPersistedSessionMessagesEventArgs>();

        public AsyncEvent<AddPersistedSessionMessageEventArgs> AddPersistedSessionMessageEvent { get; } = new AsyncEvent<AddPersistedSessionMessageEventArgs>();

        public AsyncEvent<RemovePersistedSessionMessageEventArgs> RemovePersistedSessionMessageEvent { get; } = new AsyncEvent<RemovePersistedSessionMessageEventArgs>();

        public AsyncEvent<ClearPersistedSessionsEventArgs> ClearPersistedSessionsEvent { get; } = new AsyncEvent<ClearPersistedSessionsEventArgs>();

        public AsyncEvent<UnloadPersistedSessionsEventArgs> UnloadPersistedSessionsEvent { get; } = new AsyncEvent<UnloadPersistedSessionsEventArgs>();

        #endregion
    }
}
