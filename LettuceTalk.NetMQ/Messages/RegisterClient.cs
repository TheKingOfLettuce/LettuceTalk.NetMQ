using System.Text.Json.Serialization;
using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

[MessageData(NetMQMessageCodes.REGISTER_CLIENT)]
public class RegisterClient : Message {
    [JsonInclude]
    public readonly bool PublishMessagesToServer;

    [JsonConstructor]
    public RegisterClient(bool publishMessagesToServer = false) {
        PublishMessagesToServer = publishMessagesToServer;
    } 
}