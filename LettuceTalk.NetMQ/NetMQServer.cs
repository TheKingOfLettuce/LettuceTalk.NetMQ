using NetMQ;
using NetMQ.Sockets;

namespace LettuceTalk.NetMQ;

/// <summary>
/// A <see cref="NetMQTalker"/> that uses a <see cref="RouterSocket"/> to act as a server.
/// Can handle multiple connections from <see cref="NetMQPeer"/>
/// </summary>
public class NetMQServer : NetMQTalker {
    public NetMQServer(string ip, int port, bool isBind = true) : base(ip, port, isBind) {}

    /// <summary>
    /// Creates a <see cref="RouterSocket"/>
    /// </summary>
    /// <param name="ip">the string representation of the IP to create an endpoint with</param>
    /// <param name="port">the port to create an endpoint with</param>
    /// <param name="isBind">wether it should bind the endpoint (server) or connect the endpoint (client)</param>
    /// <returns>the <see cref="RouterSocket"/> to use</returns>
    protected override NetMQSocket GetSocket(string ip, int port, bool isBind) {
        return new RouterSocket($"{(isBind ? '@' : '>')}tcp://{ip}:{port}");
    }
}