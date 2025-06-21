using System.Text.Json.Serialization;
using LettuceTalk.Core;

namespace Lettuce.Talk.NetMQ;

[MessageData]
public class RegisterClient : Message {
    [JsonInclude]
    public readonly bool PublishMessagesToServer;
    [JsonInclude]
    public readonly bool PreRegisterClient;

    [JsonConstructor]
    public RegisterClient(bool publishMessagesToServer = false, bool preRegisterClient = true) {
        PublishMessagesToServer = publishMessagesToServer;
        PreRegisterClient = preRegisterClient;
    } 
}