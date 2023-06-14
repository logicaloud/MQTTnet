// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Diagnostics;
using MQTTnet.Internal;
using MQTTnet.Server.Internal;

namespace MQTTnet.Server
{
    public sealed class MqttRetainedMessagesManager
    {
        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;

        /// <summary>
        /// Points to either the external message store, if _eventContainer.InterceptingGetRetainedMessagesEvent 
        /// has handlers at start or points to the in-memory store
        /// </summary>
        private IMqttRetainedMessageStore _messageStore;

        public MqttRetainedMessagesManager(MqttServerEventContainer eventContainer, IMqttNetLogger logger)
        {
            _eventContainer = eventContainer ?? throw new ArgumentNullException(nameof(eventContainer));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger.WithSource(nameof(MqttRetainedMessagesManager));

            // ugly, create default memory store so testing works
            _messageStore = new MqttRetainedMessageStoreInMemory(_eventContainer, _logger);
        }

        public async Task Start()
        {
            try
            {
                if ((_eventContainer.InterceptingRetainedMessagesFetchEvent.HasHandlers) ||
                    (_eventContainer.InterceptingFilterRetainedMessagesEvent.HasHandlers) ||
                    (_eventContainer.InterceptingRetainedMessageAddedOrUpdatedEvent.HasHandlers) ||
                    (_eventContainer.InterceptingRetainedMessageRemovedEvent.HasHandlers) ||
                    (_eventContainer.InterceptingRetainedMessagesClearedEvent.HasHandlers)
                    )
                {
                    // Loading and fetching of messages is intercepted, all related
                    // event handlers must be defined also and this is checked in the
                    // MqttRetainedMessageStoreExternal constructor
                    _messageStore = new MqttRetainedMessageStoreExternal(_eventContainer, _logger);
                }
                //else // keep default
                //{
                //    _messageStore = new MqttRetainedMessageStoreInMemory(_eventContainer, _logger);
                //}

                await _messageStore.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while loading retained messages.");
            }
        }

        public async Task UpdateMessage(string clientId, MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null)
            {
                throw new ArgumentNullException(nameof(applicationMessage));
            }

            var payloadSegment = applicationMessage.PayloadSegment;
            var hasPayload = payloadSegment.Count > 0;

            try
            {
                if (!hasPayload)
                {
                    await _messageStore.RemoveMessageAsync(clientId, applicationMessage).ConfigureAwait(false);

                    _logger.Verbose("Client '{0}' cleared retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                }
                else
                {
                    await _messageStore.AddOrUpdateMessageAsync(clientId, applicationMessage).ConfigureAwait(false);

                    _logger.Verbose("Client '{0}' set retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                }           
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while handling retained messages.");
            }
        }


        public Task<IList<MqttApplicationMessage>> GetMessages()
        {
            return _messageStore.GetMessagesAsync();
        }

        public Task<MqttApplicationMessage> GetMessage(string topic)
        {
            return _messageStore.GetMessageAsync(topic);
        }

        public async Task ClearMessages()
        {
            await _messageStore.ClearMessagesAsync();
        }
    }
}