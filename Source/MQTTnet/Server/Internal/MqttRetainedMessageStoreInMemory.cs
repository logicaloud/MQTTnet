using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Diagnostics;
using MQTTnet.Internal;
using MQTTnet.Server.Internal;



namespace MQTTnet.Server
{
    internal class MqttRetainedMessageStoreInMemory : IMqttRetainedMessageStore
    {
        readonly AsyncLock _storageAccessLock = new AsyncLock();

        readonly Dictionary<string, MqttApplicationMessage> _messages = new Dictionary<string, MqttApplicationMessage>(4096);

        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;

        public MqttRetainedMessageStoreInMemory(MqttServerEventContainer eventContainer, MqttNetSourceLogger logger)
        {
            _eventContainer = eventContainer;
            _logger = logger;
        }

        public async Task InitializeAsync()
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


        public async Task AddOrUpdateMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            RetainedMessageChangedEventArgs.RetainedMessageChangeType? changeType = null;

            lock (_messages)
            {
                if (!_messages.TryGetValue(applicationMessage.Topic, out var existingMessage))
                {
                    _messages[applicationMessage.Topic] = applicationMessage;
                    changeType = RetainedMessageChangedEventArgs.RetainedMessageChangeType.Add;
                }
                else
                {
                    if (existingMessage.QualityOfServiceLevel != applicationMessage.QualityOfServiceLevel || !SequenceEqual(existingMessage.PayloadSegment, applicationMessage.PayloadSegment))
                    {
                        _messages[applicationMessage.Topic] = applicationMessage;
                        changeType = RetainedMessageChangedEventArgs.RetainedMessageChangeType.Replace;
                    }
                }
            }

            if (changeType != null)
            {
                using (await _storageAccessLock.EnterAsync().ConfigureAwait(false))
                {
                    var eventArgs = new RetainedMessageChangedEventArgs(clientId, applicationMessage, changeType.Value);

                    await _eventContainer.RetainedMessageChangedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                }
            }
        }

        public async Task RemoveMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            bool removed;
            lock (_messages)
            {
                removed = _messages.Remove(applicationMessage.Topic);
            }

            if (removed)
            {
                using (await _storageAccessLock.EnterAsync().ConfigureAwait(false))
                {
                    var eventArgs = new RetainedMessageChangedEventArgs(clientId, applicationMessage, RetainedMessageChangedEventArgs.RetainedMessageChangeType.Remove);

                    await _eventContainer.RetainedMessageChangedEvent.InvokeAsync(eventArgs).ConfigureAwait(false);
                }
            }
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
