using MQTTnet.Diagnostics;
using MQTTnet.Packets;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MQTTnet.Server
{
    /// <summary>
    /// Object to hook up MqttServer events related to persisted sessions
    /// </summary>
    public class MqttPersistedSessionManager
    {
        MqttServerEventContainer _eventContainer;
        MqttNetSourceLogger _logger;

        const string WriteNotAllowedErrorMessage = "Writing to the persisted session store is not allowed; check 'IsWritable' first.";

        public MqttPersistedSessionManager(MqttServerEventContainer eventContainer, IMqttNetLogger logger)
        {
            _eventContainer = eventContainer;
            _logger = logger.WithSource("MqttPersistedSessionManager");
        }

        /// <summary>
        /// Flag indicating whether state can be stored; False to begin with so that sessions can be 
        /// restored first without writing back to the store at the same time.
        /// </summary>
        public bool IsWritable { get; set; }

        /// <summary>
        /// Load persisted sessions from storage. Call once when server starts.
        /// </summary>
        /// <returns></returns>
        public async Task<List<IPersistedSession>> LoadSessionsAsync()
        {
            var args = new LoadPersistedSessionsEventArgs();
            try
            {
                await _eventContainer.LoadPersistedSessionsEvent.InvokeAsync(args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return args.PersistedSessions;
        }

        /// <summary>
        /// Unload persisted sessions and close storage. Call once when server stops.
        /// </summary>
        /// <returns></returns>
        public Task UnloadSessionsAsync()
        {
            var args = new UnloadPersistedSessionsEventArgs();
            try
            {
                return _eventContainer.UnloadPersistedSessionsEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Fetch all persisted messages that need delivery to the client in the same order that they were stored.
        /// </summary>
        public async Task<List<IPersistedApplicationMessage>> LoadSessionMessagesAsync(string clientId)
        {
            var args = new LoadPersistedSessionMessagesEventArgs(clientId);
            try
            {
                await _eventContainer.LoadPersistedSessionMessagesEvent.InvokeAsync(args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return args.PersistedMessages;
        }

        /// <summary>
        /// Clear all stored sessions and persisted messages
        /// </summary>
        public Task ClearSessionsAsync()
        {
            // This is allowed regardless of the IsWritable flag
            var args = new ClearPersistedSessionsEventArgs();
            try
            {
                return _eventContainer.ClearPersistedSessionsEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Add or update session details for the given client 
        /// </summary>
        public Task AddOrUpdateSessionAsync(
            string clientId,
            MqttApplicationMessage willMessage,
            uint willDelayInterval,
            uint sessionExpiryInterval
            )
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            try
            {
                var args = new AddOrUpdatePersistedSessionEventArgs(clientId, willMessage, willDelayInterval, sessionExpiryInterval);
                return _eventContainer.AddOrUpdatePersistedSessionEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Update session details for the given client 
        /// </summary>
        public Task UpdateSessionExpiryTimestampAsync(string clientId, DateTime sessionExpiryTimestamp)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            try
            {
                var args = new UpdatePersistedSessionExpiryTimestampEventArgs(clientId, sessionExpiryTimestamp);
                return _eventContainer.UpdatePersistedSessionExpiryTimestampEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Remove session including subscription data and pending messages for the given client
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public Task RemoveSessionAsync(string clientId)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            try
            {
                var args = new RemovePersistedSessionEventArgs(clientId);
                return _eventContainer.RemovePersistedSessionEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Add or update subscription details for the given client
        /// </summary>
        public Task AddOrUpdateSubscriptionAsync(string clientId, Packets.MqttTopicFilter topicFilter)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            try
            {
                var args = new AddOrUpdatePersistedSubscriptionEventArgs(clientId, topicFilter);
                return _eventContainer.AddOrUpdatePersistedSubscriptionEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Remove a single subscription and all associated pending messages for the given client
        /// </summary>
        public Task RemoveSubscriptionAsync(string clientId, string topic)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            try
            {
                var args = new RemovePersistedSubscriptionEventArgs(clientId, topic);
                return _eventContainer.RemovePersistedSubscriptionEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        /// <summary>
        /// Add a pending message for the given clientId
        /// </summary>
        /// <returns>Message key to use when removing the message</returns>
        public async Task<object> AddMessageAsync(MqttApplicationMessage applicationMessage, List<MqttPersistedApplicationMessageClient> messageClients)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            var args = new AddPersistedSessionMessageEventArgs(applicationMessage, messageClients);
            try
            {
                await _eventContainer.AddPersistedSessionMessageEvent.InvokeAsync(args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return args.PersistedMessageKey;
        }

        /// <summary>
        /// Remove a pending message
        /// </summary>
        public Task RemoveMessageAsync(object messageKey)
        {
            if (!IsWritable) throw new InvalidOperationException(WriteNotAllowedErrorMessage);

            var args = new RemovePersistedSessionMessageEventArgs(messageKey);
            try
            {
                return _eventContainer.RemovePersistedSessionMessageEvent.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
            return Internal.CompletedTask.Instance;
        }

        void LogException(Exception ex)
        {
            _logger.Error("Persisted Session Store has caused an exception: " + ex.Message);
        }
    }
}
