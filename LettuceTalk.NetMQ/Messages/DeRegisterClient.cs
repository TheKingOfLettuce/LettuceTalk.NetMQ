using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

[MessageData(NetMQMessageCodes.DEREGISTER_CLIENT)]
public class DeRegisterClient : Message {

}