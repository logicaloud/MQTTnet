using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MQTTnet.Server.Internal
{
    public enum RetainedMessageChangeType { Add, Remove, Replace };

    internal interface IMqttRetainedMessageStore
    {
        Task InitializeAsync();

        Task<IList<MqttApplicationMessage>> GetMessagesAsync();

        Task<MqttApplicationMessage> GetMessageAsync(string topicFilter);

        Task AddOrUpdateMessageAsync(string clientId, MqttApplicationMessage applicationMessage);

        Task RemoveMessageAsync(string clientId, MqttApplicationMessage applicationMessage);
        
        Task ClearMessagesAsync();
    }
}
