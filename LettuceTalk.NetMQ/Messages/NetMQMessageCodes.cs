using System.Reflection;
using System.Runtime.CompilerServices;
using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

public static class NetMQMessageCodes {
    public const int REGISTER_CLIENT = 100;
    public const int REGISTER_CLIENT_ACK = 101;

    [ModuleInitializer]
    internal static void AssociateMessages() {
        MessageFactory.AssociateAssembly(Assembly.GetAssembly(typeof(NetMQMessageCodes)));
    }
}