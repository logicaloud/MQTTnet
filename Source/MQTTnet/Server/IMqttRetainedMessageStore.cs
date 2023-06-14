using MQTTnet.Protocol;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MQTTnet.Server
{
    public interface IMqttRetainedMessageStore
    {
        Task StartAsync();
        
        Task StopAsync();

        Task<IList<MqttApplicationMessage>> GetMessagesAsync();

        Task<MqttApplicationMessage> GetMessageAsync(string topicFilter);

        Task UpdateMessageAsync(string clientId, MqttApplicationMessage applicationMessage);
 
        Task<IList<MqttRetainedMessageMatch>> FilterMessagesAsync(string topicFilter, MqttQualityOfServiceLevel grantedQualityOfServiceLevel);

        Task ClearMessagesAsync();
    }
}
