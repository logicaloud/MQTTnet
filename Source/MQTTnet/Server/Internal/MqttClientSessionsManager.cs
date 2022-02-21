// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Adapter;
using MQTTnet.Diagnostics;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Internal;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace MQTTnet.Server
{
    public sealed class MqttClientSessionsManager : ISubscriptionChangedNotification, IDisposable
    {
        readonly Dictionary<string, MqttClient> _clients = new Dictionary<string, MqttClient>(4096);

        readonly AsyncLock _createConnectionSyncRoot = new AsyncLock();
        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;
        readonly MqttServerOptions _options;
        readonly MqttPacketFactories _packetFactories = new MqttPacketFactories();

        readonly MqttRetainedMessagesManager _retainedMessagesManager;
        readonly MqttPersistedSessionManager _persistedSessionManager;
        readonly MqttApplicationMessageFactory _applicationMessageFactory;

        readonly IMqttNetLogger _rootLogger;

        // Event key is ClientId, event args is null
        readonly KeyEventSchedule<string, object> _sessionExpiryEvents;
        // Event key it client ID, event arg is WillMessage
        readonly KeyEventSchedule<string, MqttApplicationMessage> _willDelayEvents;
        // Event key it topic, event arg is null
        readonly KeyEventSchedule<string, object> _retainedMessageExpiryEvents;

        // The _sessions dictionary contains all session, the _subscriberSessions hash set contains subscriber sessions only.
        // See the MqttSubscription object for a detailed explanation.
        readonly Dictionary<string, MqttSession> _sessions = new Dictionary<string, MqttSession>(4096);

        readonly object _sessionsManagementLock = new object();
        readonly HashSet<MqttSession> _subscriberSessions = new HashSet<MqttSession>();

        public MqttClientSessionsManager(
            MqttServerOptions options,
            MqttRetainedMessagesManager retainedMessagesManager,
            MqttPersistedSessionManager persistedSessionManager,
            MqttServerEventContainer eventContainer,
            IMqttNetLogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _logger = logger.WithSource(nameof(MqttClientSessionsManager));
            _rootLogger = logger;

            _options = options ?? throw new ArgumentNullException(nameof(options));
            _retainedMessagesManager = retainedMessagesManager ?? throw new ArgumentNullException(nameof(retainedMessagesManager));
            _persistedSessionManager = persistedSessionManager ?? throw new ArgumentNullException(nameof(persistedSessionManager));
            _applicationMessageFactory = new MqttApplicationMessageFactory();
            _eventContainer = eventContainer ?? throw new ArgumentNullException(nameof(eventContainer));
            _sessionExpiryEvents = new KeyEventSchedule<string, object>(OnSessionExpiredEvent, default);
            _willDelayEvents = new KeyEventSchedule<string, MqttApplicationMessage>(OnWillDelayExpiredEvent, default);
            _retainedMessageExpiryEvents = new KeyEventSchedule<string, object>(OnRetainedMessageExpiredEvent, default);
        }

        /// <summary>
        /// Load persistent messages when MQTT server starts before client connections are allowed.
        /// </summary>
        public async Task LoadPersistedSessionsAsync()
        {
            if (_persistedSessionManager.IsWritable)
            {
                throw new InvalidOperationException("Persisted sessions already loaded");
            }

            // Load sessions from storage so that messages can be queued for offline clients;
            // _persistedSessionManager 'IsWritable == false' at this stage so that no additional storage
            // attempts are made while subscriptions are being restored.

            var persistedSessions = await _persistedSessionManager.LoadSessionsAsync().ConfigureAwait(false);

            foreach (var persistedSession in persistedSessions)
            {
                // restore session for client
                var clientId = persistedSession.ClientId;
                // session items are not persisted and remain empty
                var sessionItems = new Dictionary<object, object>();
                var clientSession = CreateSession(clientId, sessionItems, true, persistedSession.SessionExpiryInterval, persistedSession.WillDelayInterval);
                _sessions[clientId] = clientSession;
                // restore subscriptions for client
                if (persistedSession.SubscriptionsByTopic != null)
                {
                    var topicFilters = persistedSession.SubscriptionsByTopic.Values.ToList();
                    // Subscribe will not attempt to (re-)store the subscription because the 
                    // _persistedSessionManager is not yet 'writable'. 
                    await SubscribeAsync(clientId, topicFilters, true).ConfigureAwait(false);
                }
            }

            // Loading of persisted queued messages is deferred until clients reconnect
        }

        public int GetNumClients()
        {
            lock (_clients)
            {
                return _clients.Count;
            }
        }

        public int GetNumSessions()
        {
            lock (_sessions)
            {
                return _sessions.Count;
            }
        }

        public async Task CloseAllConnectionsAsync()
        {
            List<MqttClient> connections;
            lock (_clients)
            {
                connections = _clients.Values.ToList();
                _clients.Clear();
            }

            foreach (var connection in connections)
            {
                await connection.StopAsync(MqttDisconnectReasonCode.NormalDisconnection).ConfigureAwait(false);
            }
        }

        public async Task DeleteSessionAsync(string clientId)
        {
            MqttClient connection;

            lock (_clients)
            {
                _clients.TryGetValue(clientId, out connection);
            }

            MqttSession session;

            lock (_sessionsManagementLock)
            {
                _sessions.TryGetValue(clientId, out session);
                _sessions.Remove(clientId);

                if (session != null)
                {
                    _subscriberSessions.Remove(session);
                }
            }

            try
            {
                if (connection != null)
                {
                    await connection.StopAsync(MqttDisconnectReasonCode.NormalDisconnection).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"Error while deleting session '{clientId}'.");
            }

            try
            {
                if (_eventContainer.SessionDeletedEvent.HasHandlers)
                {
                    var eventArgs = new SessionDeletedEventArgs
                    {
                        Id = session?.Id
                    };

                    await _eventContainer.SessionDeletedEvent.TryInvokeAsync(eventArgs, _logger).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, $"Error while executing session deleted event for session '{clientId}'.");
            }

            session?.Dispose();

            _logger.Verbose("Session for client '{0}' deleted.", clientId);
        }

        public async Task DispatchApplicationMessage(string senderId, MqttApplicationMessage applicationMessage)
        {
            try
            {
                if (applicationMessage.Retain)
                {
                    await _retainedMessagesManager.UpdateMessageAsync(senderId, applicationMessage).ConfigureAwait(false);
                }

                var deliveryCount = 0;
                List<MqttSession> subscriberSessions;
                lock (_sessionsManagementLock)
                {
                    // only subscriber clients are of interest here.
                    subscriberSessions = _subscriberSessions.ToList();
                }

                // Remains null unless messages need to be persisted for clients:
                List<MqttPersistedApplicationMessageClient> persistingApplicationMessageClients = null;

                // Calculate application message topic hash once for subscription checks
                MqttSubscription.CalculateTopicHash(applicationMessage.Topic, out var topicHash, out _, out _);

                foreach (var session in subscriberSessions)
                {
                    var checkSubscriptionsResult = session.SubscriptionsManager.CheckSubscriptions(
                        applicationMessage.Topic,
                        topicHash,
                        applicationMessage.QualityOfServiceLevel,
                        senderId);

                    if (!checkSubscriptionsResult.IsSubscribed)
                    {
                        continue;
                    }

                    var newPublishPacket = _packetFactories.Publish.Create(applicationMessage);
                    newPublishPacket.QualityOfServiceLevel = checkSubscriptionsResult.QualityOfServiceLevel;
                    newPublishPacket.SubscriptionIdentifiers = checkSubscriptionsResult.SubscriptionIdentifiers;

                    if (newPublishPacket.QualityOfServiceLevel > 0)
                    {
                        newPublishPacket.PacketIdentifier = session.PacketIdentifierProvider.GetNextPacketIdentifier();

                        if (session.IsPersistent && _persistedSessionManager.IsWritable)
                        {
                            if (persistingApplicationMessageClients == null)
                            {
                                persistingApplicationMessageClients = new List<MqttPersistedApplicationMessageClient>();
                            }
                            persistingApplicationMessageClients.Add(
                                new MqttPersistedApplicationMessageClient(
                                    session.Id,
                                    checkSubscriptionsResult.QualityOfServiceLevel,
                                    checkSubscriptionsResult.SubscriptionIdentifiers
                                    )
                                );
                        }
                    }

                    if (applicationMessage.MessageExpiryInterval > 0)
                    {
                        newPublishPacket.MessageExpiryTimestamp = DateTime.UtcNow.AddSeconds(applicationMessage.MessageExpiryInterval);
                    }

                    if (checkSubscriptionsResult.RetainAsPublished)
                    {
                        // Transfer the original retain state from the publisher. This is a MQTTv5 feature.
                        newPublishPacket.Retain = applicationMessage.Retain;
                    }
                    else
                    {
                        newPublishPacket.Retain = false;
                    }

                    // TODO:
                    // Review: there is currently nothing that removes an expired message from the queue.
                    // The message is simply skipped when packets are processed.

                    session.EnqueuePacket(new MqttPacketBusItem(newPublishPacket));
                    deliveryCount++;

                    _logger.Verbose("Client '{0}': Queued PUBLISH packet with topic '{1}'.", session.Id, applicationMessage.Topic);
                }

                if (deliveryCount == 0)
                {
                    await FireApplicationMessageNotConsumedEvent(applicationMessage, deliveryCount, senderId);
                }
                else if (persistingApplicationMessageClients != null)
                {
                    // persist message for one or more clients
                    await _persistedSessionManager.AddMessageAsync(applicationMessage, persistingApplicationMessageClients).ConfigureAwait(false);
                }

                if (applicationMessage.Retain)
                {
                    if ((applicationMessage.MessageExpiryInterval > 0) && (applicationMessage.Payload != null) && (applicationMessage.Payload.Length > 0))
                    {
                        // schedule expiry
                        _retainedMessageExpiryEvents.AddOrUpdateEvent(applicationMessage.MessageExpiryInterval, applicationMessage.Topic, null);
                    }
                    else
                    {
                        // Payload empty or retain forever; remove expiry event if it exists
                        _retainedMessageExpiryEvents.RemoveEvent(applicationMessage.Topic);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while processing next queued application message.");
            }
        }

        public void Dispose()
        {
            _createConnectionSyncRoot?.Dispose();

            lock (_sessionsManagementLock)
            {
                foreach (var sessionItem in _sessions)
                {
                    sessionItem.Value.Dispose();
                }
            }
        }

        public MqttClient GetClient(string id)
        {
            lock (_clients)
            {
                if (!_clients.TryGetValue(id, out var client))
                {
                    throw new InvalidOperationException($"Client with ID '{id}' not found.");
                }

                return client;
            }
        }

        public List<MqttClient> GetClients()
        {
            lock (_clients)
            {
                return _clients.Values.ToList();
            }
        }

        public Task<IList<MqttClientStatus>> GetClientStatusAsync()
        {
            var result = new List<MqttClientStatus>();

            lock (_clients)
            {
                foreach (var connection in _clients.Values)
                {
                    var clientStatus = new MqttClientStatus(connection)
                    {
                        Session = new MqttSessionStatus(connection.Session)
                    };

                    result.Add(clientStatus);
                }
            }

            return Task.FromResult((IList<MqttClientStatus>)result);
        }

        public Task<IList<MqttSessionStatus>> GetSessionStatusAsync()
        {
            var result = new List<MqttSessionStatus>();

            lock (_sessionsManagementLock)
            {
                foreach (var sessionItem in _sessions)
                {
                    var sessionStatus = new MqttSessionStatus(sessionItem.Value);
                    result.Add(sessionStatus);
                }
            }

            return Task.FromResult((IList<MqttSessionStatus>)result);
        }

        public async Task HandleClientConnectionAsync(IMqttChannelAdapter channelAdapter, CancellationToken cancellationToken)
        {
            MqttClient client = null;

            try
            {
                var connectPacket = await ReceiveConnectPacket(channelAdapter, cancellationToken).ConfigureAwait(false);
                if (connectPacket == null)
                {
                    // Nothing was received in time etc.
                    return;
                }

                var validatingConnectionEventArgs = await ValidateConnection(connectPacket, channelAdapter).ConfigureAwait(false);
                var connAckPacket = _packetFactories.ConnAck.Create(validatingConnectionEventArgs);

                if (validatingConnectionEventArgs.ReasonCode != MqttConnectReasonCode.Success)
                {
                    // Send failure response here without preparing a connection and session!
                    await channelAdapter.SendPacketAsync(connAckPacket, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // Pass connAckPacket so that IsSessionPresent flag can be set if the client session already exists.
                client = await CreateClientConnection(connectPacket, connAckPacket, channelAdapter, validatingConnectionEventArgs).ConfigureAwait(false);

                await client.SendPacketAsync(connAckPacket, cancellationToken).ConfigureAwait(false);

                if (_eventContainer.ClientConnectedEvent.HasHandlers)
                {
                    var eventArgs = new ClientConnectedEventArgs
                    {
                        ClientId = connectPacket.ClientId,
                        UserName = connectPacket.Username,
                        ProtocolVersion = channelAdapter.PacketFormatterAdapter.ProtocolVersion,
                        Endpoint = channelAdapter.Endpoint
                    };

                    await _eventContainer.ClientConnectedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                }

                await client.RunAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                _logger.Error(exception, exception.Message);
            }
            finally
            {
                if (client != null)
                {
                    if (client.Id != null)
                    {
                        // in case it is a takeover _clientConnections already contains the new connection
                        if (!client.IsTakenOver)
                        {
                            lock (_clients)
                            {
                                _clients.Remove(client.Id);
                            }

                            if (!_options.EnablePersistentSessions || !client.Session.IsPersistent)
                            {
                                await DeleteSessionAsync(client.Id).ConfigureAwait(false);
                            }
                            else
                            {
                                if (client.Session.SessionExpiryInterval != uint.MaxValue) // if not keep forever then expire
                                {
                                    if (_persistedSessionManager.IsWritable)
                                    {
                                        // update store with expiry timestamp
                                        var expiryTimeStamp = DateTime.UtcNow.AddSeconds(client.Session.SessionExpiryInterval);
                                        await _persistedSessionManager.UpdateSessionExpiryTimestampAsync(client.Id, expiryTimeStamp).ConfigureAwait(false);
                                    }
                                    // schedule expiry
                                    _sessionExpiryEvents.AddOrUpdateEvent(client.Session.SessionExpiryInterval, client.Id, client.Id);
                                }
                            }
                        }
                    }

                    var endpoint = client.Endpoint;

                    if (client.Id != null && !client.IsTakenOver && _eventContainer.ClientDisconnectedEvent.HasHandlers)
                    {
                        var eventArgs = new ClientDisconnectedEventArgs
                        {
                            ClientId = client.Id,
                            DisconnectType = client.IsCleanDisconnect ? MqttClientDisconnectType.Clean : MqttClientDisconnectType.NotClean,
                            Endpoint = endpoint
                        };

                        await _eventContainer.ClientDisconnectedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                    }
                }

                using (var timeout = new CancellationTokenSource(_options.DefaultCommunicationTimeout))
                {
                    await channelAdapter.DisconnectAsync(timeout.Token).ConfigureAwait(false);
                }
            }
        }

        public void OnSubscriptionsAdded(MqttSession clientSession, List<string> topics)
        {
            lock (_sessionsManagementLock)
            {
                if (!clientSession.HasSubscribedTopics)
                {
                    // first subscribed topic
                    _subscriberSessions.Add(clientSession);
                }

                foreach (var topic in topics)
                {
                    clientSession.AddSubscribedTopic(topic);
                }
            }
        }

        public void OnSubscriptionsRemoved(MqttSession clientSession, List<string> subscriptionTopics)
        {
            lock (_sessionsManagementLock)
            {
                foreach (var subscriptionTopic in subscriptionTopics)
                {
                    clientSession.RemoveSubscribedTopic(subscriptionTopic);
                }

                if (!clientSession.HasSubscribedTopics)
                {
                    // last subscription removed
                    _subscriberSessions.Remove(clientSession);
                }
            }
        }

        public void Start()
        {
            if (!_options.EnablePersistentSessions)
            {
                _sessions.Clear();
            }
        }

        public async Task SubscribeAsync(string clientId, ICollection<MqttTopicFilter> topicFilters, bool asEstablishedSubscription = false)
        {
            if (clientId == null)
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (topicFilters == null)
            {
                throw new ArgumentNullException(nameof(topicFilters));
            }

            var fakeSubscribePacket = new MqttSubscribePacket();
            fakeSubscribePacket.TopicFilters.AddRange(topicFilters);

            var clientSession = GetClientSession(clientId);

            var subscribeResult = await clientSession.SubscriptionsManager.Subscribe(fakeSubscribePacket, CancellationToken.None).ConfigureAwait(false);

            if (subscribeResult.RetainedMessages != null)
            {
                foreach (var retainedApplicationMessage in subscribeResult.RetainedMessages)
                {
                    var publishPacket = _packetFactories.Publish.Create(retainedApplicationMessage.ApplicationMessage);
                    /* 
                     * MQTT 3.1.1 spec
                     * When sending a PUBLISH Packet to a Client the Server MUST set the RETAIN flag to 1 if a message is sent as a result 
                     * of a new subscription being made by a Client [MQTT-3.3.1-8]. It MUST set the RETAIN flag to 0 when a PUBLISH Packet is 
                     * sent to a Client because it matches an established subscription regardless of how the flag was set in the message it
                     * received [MQTT-3.3.1-9].
                     */
                    if (asEstablishedSubscription)
                    {
                        publishPacket.Retain = false;
                    }
                    // TODO, REVIEW. Quality of service level irrelevant. If > AtMostOnce then we need a packet identifier
                    publishPacket.QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce; clientSession.EnqueuePacket(new MqttPacketBusItem(publishPacket));
                }
            }
        }

        public Task UnsubscribeAsync(string clientId, ICollection<string> topicFilters)
        {
            if (clientId == null)
            {
                throw new ArgumentNullException(nameof(clientId));
            }

            if (topicFilters == null)
            {
                throw new ArgumentNullException(nameof(topicFilters));
            }

            var fakeUnsubscribePacket = new MqttUnsubscribePacket();
            fakeUnsubscribePacket.TopicFilters.AddRange(topicFilters);

            return GetClientSession(clientId).SubscriptionsManager.Unsubscribe(fakeUnsubscribePacket, CancellationToken.None);
        }

        async Task<MqttClient> CreateClientConnection(
            MqttConnectPacket connectPacket,
            MqttConnAckPacket connAckPacket,
            IMqttChannelAdapter channelAdapter,
            ValidatingConnectionEventArgs validatingConnectionEventArgs)
        {
            MqttClient connection;

            bool sessionShouldPersist;
            bool startWithCleanSession;
            uint persistedSessionExpiryInterval = uint.MaxValue; // infinite by default for persisted MQTT 3.1.1 sessions

            if (validatingConnectionEventArgs.ProtocolVersion == MqttProtocolVersion.V500)
            {
                // MQTT 5.0 section 3.1.2.11.2
                // The Client and Server MUST store the Session State after the Network Connection is closed if the Session Expiry Interval is greater than 0 [MQTT-3.1.2-23].
                //
                // A Client that only wants to process messages while connected will set the Clean Start to 1 and set the Session Expiry Interval to 0.
                // It will not receive Application Messages published before it connected and has to subscribe afresh to any topics that it is interested
                // in each time it connects.

                // Persist if SessionExpiryInterval != 0, but may start with a clean session
                persistedSessionExpiryInterval = validatingConnectionEventArgs.SessionExpiryInterval;
                sessionShouldPersist = persistedSessionExpiryInterval != 0;
                startWithCleanSession = connectPacket.CleanSession;
            }
            else
            {
                // MQTT 3.1.1 section 3.1.2.4: persist only if 'not CleanSession'
                //
                // If CleanSession is set to 1, the Client and Server MUST discard any previous Session and start a new one.
                // This Session lasts as long as the Network Connection. State data associated with this Session MUST NOT be
                // reused in any subsequent Session [MQTT-3.1.2-6].

                sessionShouldPersist = !connectPacket.CleanSession;
                startWithCleanSession = connectPacket.CleanSession;
            }

            // Prevent potential session expiry by removing any expiry event
            _sessionExpiryEvents.RemoveEvent(connectPacket.ClientId);
            // Prevent potentially delayed will message being sent
            _willDelayEvents.RemoveEvent(connectPacket.ClientId);

            using (await _createConnectionSyncRoot.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                MqttSession session;
                lock (_sessionsManagementLock)
                {
                    if (!_sessions.TryGetValue(connectPacket.ClientId, out session))
                    {
                        session = CreateSession(connectPacket.ClientId, validatingConnectionEventArgs.SessionItems, sessionShouldPersist, persistedSessionExpiryInterval, connectPacket.WillDelayInterval);
                    }
                    else
                    {
                        if (connectPacket.CleanSession)
                        {
                            _logger.Verbose("Deleting existing session of client '{0}'.", connectPacket.ClientId);
                            session = CreateSession(connectPacket.ClientId, validatingConnectionEventArgs.SessionItems, sessionShouldPersist, persistedSessionExpiryInterval, connectPacket.WillDelayInterval);
                        }
                        else
                        {
                            _logger.Verbose("Reusing existing session of client '{0}'.", connectPacket.ClientId);
                            connAckPacket.IsSessionPresent = true;
                            // Session persistence could change for MQTT 5 clients that reconnect with different SessionExpiryInterval
                            session.IsPersistent = sessionShouldPersist;
                            session.SessionExpiryInterval = persistedSessionExpiryInterval;
                            session.WillDelayInterval = connectPacket.WillDelayInterval;
                            session.Recover();
                        }
                    }

                    _sessions[connectPacket.ClientId] = session;
                }

                if (!connAckPacket.IsSessionPresent)
                {
                    // TODO: This event is not yet final. It can already be used but restoring sessions from storage will be added later!
                    var preparingSessionEventArgs = new PreparingSessionEventArgs();
                    await _eventContainer.PreparingSessionEvent.InvokeAsync(preparingSessionEventArgs).ConfigureAwait(false);
                }

                MqttClient existing;

                lock (_clients)
                {
                    _clients.TryGetValue(connectPacket.ClientId, out existing);
                    connection = CreateConnection(connectPacket, channelAdapter, session);

                    _clients[connectPacket.ClientId] = connection;
                }

                if (existing != null)
                {
                    existing.IsTakenOver = true;
                    await existing.StopAsync(MqttDisconnectReasonCode.SessionTakenOver).ConfigureAwait(false);

                    if (_eventContainer.ClientConnectedEvent.HasHandlers)
                    {
                        var eventArgs = new ClientDisconnectedEventArgs
                        {
                            ClientId = existing.Id,
                            DisconnectType = MqttClientDisconnectType.Takeover,
                            Endpoint = existing.Endpoint
                        };

                        await _eventContainer.ClientDisconnectedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                    }
                }


                // Any previously existing client is disconnected now; queue persisted messages (if any) for new session
                
                if (_persistedSessionManager.IsWritable)
                {
                    if (startWithCleanSession)
                    {
                        // This will remove the session and queued messages from the store
                        await _persistedSessionManager.RemoveSessionAsync(connectPacket.ClientId).ConfigureAwait(false);
                    }
                    if (sessionShouldPersist)
                    {
                        MqttApplicationMessage willMessage = null;
                        uint? willDelayInterval = null;
                        if (connectPacket.WillFlag)
                        {
                            var willPublishPacket = _packetFactories.Publish.Create(connectPacket);
                            willMessage = _applicationMessageFactory.Create(willPublishPacket);
                            willDelayInterval = connectPacket.WillDelayInterval;
                        }
                        // Add or update session parameters
                        await _persistedSessionManager.AddOrUpdateSessionAsync(
                            connectPacket.ClientId,
                            willMessage,
                            willDelayInterval.GetValueOrDefault(),
                            session.SessionExpiryInterval
                            ).ConfigureAwait(false);
                    }
                    if ((!startWithCleanSession) && (!session.PersistedMessagesAreRestored))
                    {
                        //  Restore and queue persisted messages if there are any
                        var persistedMessages = await _persistedSessionManager.LoadSessionMessagesAsync(connectPacket.ClientId).ConfigureAwait(false);
                        foreach (var message in persistedMessages)
                        {
                            var publishPacket = _packetFactories.Publish.Create(message.ApplicationMessage);
                            // adjust publish packet with granted Qos Level and subscription identifers as at the time of publishing (DispatchPublishPacket)
                            publishPacket.QualityOfServiceLevel = message.CheckedQualityOfServiceLevel;
                            if ((message.CheckedSubscriptionIdentifiers != null) && (message.CheckedSubscriptionIdentifiers.Count > 0))
                            {
                                publishPacket.SubscriptionIdentifiers.AddRange(message.CheckedSubscriptionIdentifiers);
                            }
                            publishPacket.PacketIdentifier = session.PacketIdentifierProvider.GetNextPacketIdentifier();
                            publishPacket.PersistedMessageKey = message.PersistedMessageKey;
                            session.EnqueuePacket(new MqttPacketBusItem(publishPacket));
                        }
                        // Set flag to indicate that messages are now tracked in memory with the persisted session manager kept in sync.
                        // If client disconnects and reconnects while server is running then loading of session messages from storage is not required.
                        session.PersistedMessagesAreRestored = true;
                    }
                }
            }

            return connection;
        }

        MqttClient CreateConnection(MqttConnectPacket connectPacket, IMqttChannelAdapter channelAdapter, MqttSession session)
        {
            return new MqttClient(connectPacket, channelAdapter, session, _options, _eventContainer, this, _rootLogger);
        }

        MqttSession CreateSession(string clientId, IDictionary sessionItems, bool isPersistent, uint sessionExpiryInterval, uint willDelayInterval)
        {
            _logger.Verbose("Created a new session for client '{0}'.", clientId);

            return new MqttSession(
                clientId,
                isPersistent,
                sessionExpiryInterval,
                willDelayInterval,
                sessionItems,
                _options,
                _eventContainer,
                _retainedMessagesManager,
                _persistedSessionManager,
                this
                );
        }

        async Task FireApplicationMessageNotConsumedEvent(MqttApplicationMessage applicationMessage, int deliveryCount, string senderId)
        {
            if (deliveryCount > 0)
            {
                return;
            }

            if (!_eventContainer.ApplicationMessageNotConsumedEvent.HasHandlers)
            {
                return;
            }

            var eventArgs = new ApplicationMessageNotConsumedEventArgs
            {
                ApplicationMessage = applicationMessage,
                SenderId = senderId
            };

            await _eventContainer.ApplicationMessageNotConsumedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
        }

        MqttSession GetClientSession(string clientId)
        {
            lock (_sessionsManagementLock)
            {
                if (!_sessions.TryGetValue(clientId, out var session))
                {
                    throw new InvalidOperationException($"Client session '{clientId}' is unknown.");
                }

                return session;
            }
        }

        async Task<MqttConnectPacket> ReceiveConnectPacket(IMqttChannelAdapter channelAdapter, CancellationToken cancellationToken)
        {
            try
            {
                using (var timeoutToken = new CancellationTokenSource(_options.DefaultCommunicationTimeout))
                using (var effectiveCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutToken.Token, cancellationToken))
                {
                    var firstPacket = await channelAdapter.ReceivePacketAsync(effectiveCancellationToken.Token).ConfigureAwait(false);
                    if (firstPacket is MqttConnectPacket connectPacket)
                    {
                        return connectPacket;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Client '{0}': Connected but did not sent a CONNECT packet.", channelAdapter.Endpoint);
            }
            catch (MqttCommunicationTimedOutException)
            {
                _logger.Warning("Client '{0}': Connected but did not sent a CONNECT packet.", channelAdapter.Endpoint);
            }

            _logger.Warning("Client '{0}': First received packet was no 'CONNECT' packet [MQTT-3.1.0-1].", channelAdapter.Endpoint);
            return null;
        }

        async Task<ValidatingConnectionEventArgs> ValidateConnection(MqttConnectPacket connectPacket, IMqttChannelAdapter channelAdapter)
        {
            var context = new ValidatingConnectionEventArgs(connectPacket, channelAdapter)
            {
                SessionItems = new ConcurrentDictionary<object, object>()
            };

            await _eventContainer.ValidatingConnectionEvent.InvokeAsync(context).ConfigureAwait(false);

            // Check the client ID and set a random one if supported.
            if (string.IsNullOrEmpty(connectPacket.ClientId) && channelAdapter.PacketFormatterAdapter.ProtocolVersion == MqttProtocolVersion.V500)
            {
                connectPacket.ClientId = context.AssignedClientIdentifier;
            }

            if (string.IsNullOrEmpty(connectPacket.ClientId))
            {
                context.ReasonCode = MqttConnectReasonCode.ClientIdentifierNotValid;
            }

            return context;
        }

        public void ScheduleWillMessage(string clientId, MqttApplicationMessage willMessage, uint delayInterval)
        {
            _willDelayEvents.AddOrUpdateEvent(delayInterval, clientId, willMessage);
        }

        public async Task OnSessionExpiredEvent(string expiredSessionClientId, object args, CancellationToken cancellationToken)
        {
            using (await _createConnectionSyncRoot.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                // This is inside _createConnectionSyncRoot to warrant consistent behaviour in HandleClientConnectionAsync

                bool isConnected;

                lock (_clients)
                {
                    isConnected = _clients.ContainsKey(expiredSessionClientId);
                }

                // if not reconnected then delete

                if (!isConnected)
                {
                    if (_persistedSessionManager.IsWritable)
                    {
                        await _persistedSessionManager.RemoveSessionAsync(expiredSessionClientId);
                    }

                    await DeleteSessionAsync(expiredSessionClientId).ConfigureAwait(false);
                }
            }
        }

        public Task OnWillDelayExpiredEvent(string clientId, MqttApplicationMessage willMessage, CancellationToken cancellationToken)
        {
            bool isConnected;

            lock (_clients)
            {
                isConnected = _clients.ContainsKey(clientId);
            }

            if (!isConnected)
            {
                // Still not connected, send will message
                /*
                 * The Server delays publishing the Client’s Will Message until the Will Delay Interval has passed or the Session ends, 
                 * whichever happens first. If a new Network Connection to this Session is made before the Will Delay Interval has passed, 
                 * the Server MUST NOT send the Will Message [MQTT-3.1.3-9].
                 */
                return DispatchApplicationMessage(clientId, willMessage);
            }

            return Implementations.PlatformAbstractionLayer.CompletedTask;
        }

        public Task OnRetainedMessageExpiredEvent(string topic, object args, CancellationToken cancellationToken)
        {
            return _retainedMessagesManager.RemoveMessageAsync(topic);
        }
    }
}