using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Diagnostics;
using MQTTnet.Internal;
using MQTTnet.Protocol;

namespace MQTTnet.Server
{
    public class MqttRetainedMessagesManager : IMqttRetainedMessageStore
    {
        readonly Dictionary<string, MqttApplicationMessage> _messages = new Dictionary<string, MqttApplicationMessage>(4096);
        readonly AsyncLock _storageAccessLock = new AsyncLock();

        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;

        public MqttRetainedMessagesManager(MqttServerEventContainer eventContainer, IMqttNetLogger logger)
        {
            _eventContainer = eventContainer;

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger.WithSource(nameof(MqttRetainedMessagesManager));
        }

        public async Task StartAsync()
        {
            try
            {
                var eventArgs = new LoadingRetainedMessagesEventArgs();
                await _eventContainer.LoadingRetainedMessagesEvent.InvokeAsync(eventArgs).ConfigureAwait(false);

                lock (_messages)
                {
                    _messages.Clear();

                    if (eventArgs.LoadedRetainedMessages != null)
                    {
                        foreach (var retainedMessage in eventArgs.LoadedRetainedMessages)
                        {
                            _messages[retainedMessage.Topic] = retainedMessage;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while loading retained messages.");
            }
        }

        public Task StopAsync()
        {
            // free memory
            return ClearMessagesAsync();
        }
         
        public async Task UpdateMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            if (applicationMessage == null)
            {
                throw new ArgumentNullException(nameof(applicationMessage));
            }

            try
            {
                List<MqttApplicationMessage> messagesForSave = null;
                var saveIsRequired = false;

                lock (_messages)
                {
                    var payloadSegment = applicationMessage.PayloadSegment;
                    var hasPayload = payloadSegment.Count > 0;

                    if (!hasPayload)
                    {
                        saveIsRequired = _messages.Remove(applicationMessage.Topic);
                        _logger.Verbose("Client '{0}' cleared retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }
                    else
                    {
                        if (!_messages.TryGetValue(applicationMessage.Topic, out var existingMessage))
                        {
                            _messages[applicationMessage.Topic] = applicationMessage;
                            saveIsRequired = true;
                        }
                        else
                        {
                            if (existingMessage.QualityOfServiceLevel != applicationMessage.QualityOfServiceLevel || !SequenceEqual(existingMessage.PayloadSegment, payloadSegment))
                            {
                                _messages[applicationMessage.Topic] = applicationMessage;
                                saveIsRequired = true;
                            }
                        }

                        _logger.Verbose("Client '{0}' set retained message for topic '{1}'.", clientId, applicationMessage.Topic);
                    }

                    if (saveIsRequired)
                    {
                        messagesForSave = new List<MqttApplicationMessage>(_messages.Values);
                    }
                }

                if (saveIsRequired)
                {
                    using (await _storageAccessLock.EnterAsync().ConfigureAwait(false))
                    {
                        var eventArgs = new RetainedMessageChangedEventArgs(clientId, applicationMessage, messagesForSave);
                        await _eventContainer.RetainedMessageChangedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Unhandled exception while handling retained messages.");
            }
        }

        public Task<IList<MqttApplicationMessage>> GetMessagesAsync()
        {
            lock (_messages)
            {
                var result = new List<MqttApplicationMessage>(_messages.Values);
                return Task.FromResult((IList<MqttApplicationMessage>)result);
            }
        }

        public Task<MqttApplicationMessage> GetMessageAsync(string topic)
        {
            lock (_messages)
            {
                if (_messages.TryGetValue(topic, out var message))
                {
                    return Task.FromResult<MqttApplicationMessage>(message);
                }
            }

            return Task.FromResult<MqttApplicationMessage>(null);
        }

        public async Task ClearMessagesAsync()
        {
            lock (_messages)
            {
                _messages.Clear();
            }

            using (await _storageAccessLock.EnterAsync().ConfigureAwait(false))
            {
                await _eventContainer.RetainedMessagesClearedEvent.InvokeAsync(EventArgs.Empty).ConfigureAwait(false);
            }
        }

        public async Task<IList<MqttRetainedMessageMatch>> FilterMessagesAsync(string topic, MqttQualityOfServiceLevel grantedQualityOfServiceLevel)
        {
            var retainedMessages = await GetMessagesAsync().ConfigureAwait(false);

            var matchingRetainedMessages = new List<MqttRetainedMessageMatch>();

            for (var index = retainedMessages.Count - 1; index >= 0; index--)
            {
                var retainedMessage = retainedMessages[index];
                if (retainedMessage == null)
                {
                    continue;
                }

                if (MqttTopicFilterComparer.Compare(retainedMessage.Topic, topic) != MqttTopicFilterCompareResult.IsMatch)
                {
                    continue;
                }

                var matchingMessage = MqttRetainedMessageMatchFactory.CreateMatchingRetainedMessage(retainedMessage, grantedQualityOfServiceLevel);

                matchingRetainedMessages.Add(matchingMessage);

                // Clear the retained message from the list because the client should receive every message only 
                // one time even if multiple subscriptions affect them.
                retainedMessages[index] = null;
            }

            return matchingRetainedMessages;

        }

        private static bool SequenceEqual(ArraySegment<byte> source, ArraySegment<byte> target)
        {
#if NETCOREAPP3_1_OR_GREATER || NETSTANDARD2_1
            return source.AsSpan().SequenceEqual(target);
#else
            return source.Count == target.Count && Enumerable.SequenceEqual(source, target);
#endif
        }

    }
}
