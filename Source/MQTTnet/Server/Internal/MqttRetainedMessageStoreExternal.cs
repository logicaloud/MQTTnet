using MQTTnet.Diagnostics;
using MQTTnet.Internal;
using MQTTnet.Server.Internal;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MQTTnet.Server
{
    internal class MqttRetainedMessageStoreExternal : IMqttRetainedMessageStore
    {
        readonly MqttServerEventContainer _eventContainer;
        readonly MqttNetSourceLogger _logger;

        const string EventHandlingExceptionMessage = "Unhandled exception during InterceptingGetRetainedMessagesEvent event handling.";

        public MqttRetainedMessageStoreExternal(MqttServerEventContainer eventContainer, MqttNetSourceLogger logger)
        {
            if ((!_eventContainer.InterceptingRetainedMessagesFetchEvent.HasHandlers) ||
                (!_eventContainer.InterceptingFilterRetainedMessagesEvent.HasHandlers) ||
                (!_eventContainer.InterceptingRetainedMessageAddedOrUpdatedEvent.HasHandlers) ||
                (!_eventContainer.InterceptingRetainedMessageRemovedEvent.HasHandlers) ||
                (!_eventContainer.InterceptingRetainedMessagesClearedEvent.HasHandlers)
                )
            {
                throw new ArgumentException("Some intercepting event handlers for external retained messages are not defined");
            }
            _eventContainer = eventContainer;
            _logger = logger;
        }

        public Task InitializeAsync()
        {
            // nothing to do
            return CompletedTask.Instance;
        }

        public async Task<IList<MqttApplicationMessage>> GetMessagesAsync()
        {
            try
            {
                // no topic filter, pass null to get all messages
                var interceptArgs = new InterceptingRetainedMessagesFetchEventArgs(null); 
                await _eventContainer.InterceptingRetainedMessagesFetchEvent.InvokeAsync(interceptArgs).ConfigureAwait(false);
                return interceptArgs.ApplicationMessages;
            }
            catch (Exception exception)
            {
                _logger.Error(exception, EventHandlingExceptionMessage, nameof(GetMessagesAsync));
            }

            return null;
        }

        public async Task<MqttApplicationMessage> GetMessageAsync(string topic)
        {
            try
            {
                var interceptArgs = new InterceptingRetainedMessagesFetchEventArgs(topic);
                await _eventContainer.InterceptingRetainedMessagesFetchEvent.InvokeAsync(interceptArgs).ConfigureAwait(false);

                if (interceptArgs.ApplicationMessages != null)
                {
                    // excactly one message should be returned
                    if (interceptArgs.ApplicationMessages.Count != 1)
                    {
                        throw new Exception("The InterceptingGetRetainedMessagesEvent has returned more than one application message when only one was expected.");
                    }
                    return interceptArgs.ApplicationMessages[0];
                }
            }
            catch (Exception exception)
            {
                _logger.Error(exception, EventHandlingExceptionMessage, nameof(GetMessageAsync));
            }

            return null;
        }

        public Task RemoveMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            try
            {
                var interceptArgs = new InterceptingRetainedMessageRemovedEventArgs(clientId, applicationMessage);
                return _eventContainer.InterceptingRetainedMessageRemovedEvent.InvokeAsync(interceptArgs);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, EventHandlingExceptionMessage, nameof(RemoveMessageAsync));
                return CompletedTask.Instance;
            }
        }

        public Task AddOrUpdateMessageAsync(string clientId, MqttApplicationMessage applicationMessage)
        {
            try
            {
                var interceptArgs = new InterceptingRetainedMessageAddedOrUpdatedEventArgs(clientId, applicationMessage);
                return _eventContainer.InterceptingRetainedMessageAddedOrUpdatedEvent.InvokeAsync(interceptArgs);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, EventHandlingExceptionMessage, nameof(AddOrUpdateMessageAsync));
                return CompletedTask.Instance;
            }
        }

        public Task ClearMessagesAsync()
        {
            try
            {
                var interceptArgs = new InterceptingRetainedMessagesClearedEventArgs();
                return _eventContainer.InterceptingRetainedMessagesClearedEvent.InvokeAsync(interceptArgs);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, EventHandlingExceptionMessage, nameof(ClearMessagesAsync));
                return CompletedTask.Instance;
            }
        }
    }
}
