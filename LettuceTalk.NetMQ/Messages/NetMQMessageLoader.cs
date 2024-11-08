using System.Reflection;
using System.Runtime.CompilerServices;
using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

public static class NetMQMessageLoader {

    [ModuleInitializer]
    internal static void AssociateMessages() {
        MessageFactory.AssociateAssembly(Assembly.GetAssembly(typeof(NetMQMessageLoader)));
    }
}