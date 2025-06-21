using System.Reflection;
using System.Runtime.CompilerServices;
using LettuceTalk.Core;

namespace Lettuce.Talk.NetMQ;

public static class NetMQMessageLoader {

    [ModuleInitializer]
    internal static void AssociateMessages() {
        MessageFactory.AssociateAssembly(Assembly.GetAssembly(typeof(NetMQMessageLoader)));
    }
}