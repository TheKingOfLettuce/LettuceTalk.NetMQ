using System.Text;
using NetMQ;
using NetMQ.Sockets;

namespace LettuceTalk.NetMQ;

/// <summary>
/// A <see cref="NetMQTalker"/> that uses a <see cref="DealerSocket"/> to act as a client.
/// This can either a single bind/connect to another <see cref="NetMQPeer"/> or connecting to a <see cref="NetMQServer"/>
/// </summary>
public class NetMQPeer : NetMQTalker {
    public readonly string Name;

    public NetMQPeer(string name, string ip, int port, bool isBind = true) : base(ip, port, isBind) {
        Name = name;
        _socket.Options.Identity = Encoding.Unicode.GetBytes(Name);
    }

    /// <summary>
    /// Creates a <see cref="DealerSocket"/>
    /// </summary>
    /// <param name="ip">the string representation of the IP to create an endpoint with</param>
    /// <param name="port">the port to create an endpoint with</param>
    /// <param name="isBind">wether it should bind the endpoint (server) or connect the endpoint (client)</param>
    /// <returns>the <see cref="DealerSocket"/> to use</returns>
    protected override NetMQSocket GetSocket(string ip, int port, bool isBind) {
        return new DealerSocket($"{(isBind ? '@' : '>')}tcp://{ip}:{port}");
    }
}