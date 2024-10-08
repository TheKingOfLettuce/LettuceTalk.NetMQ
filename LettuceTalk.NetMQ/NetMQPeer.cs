using NetMQ;
using NetMQ.Sockets;
using LettuceTalk.Core.MessageHandlers;
using LettuceTalk.Core;
using System.Text;

namespace LettuceTalk.NetMQ;

/// <summary>
/// A <see cref="TalkingPoint"/> that uses a <see cref="DealerSocket"/> to act as a client.<para/>
/// This can either do a single bind/connect to another <see cref="NetMQPeer"/> or connect to a <see cref="NetMQServer"/>
/// </summary>
public class NetMQPeer : TalkingPoint, IDisposable {
    public readonly string Name;
    protected readonly DealerSocket _socket;
    protected readonly NetMQPoller _poller;
    protected readonly NetMQQueue<Message> _messageQueue;
    
    /// <summary>
    /// Creates an endpoint at the given ip and port
    /// </summary>
    /// <param name="ip">the string representation of the IP to create an endpoint with</param>
    /// <param name="port">the port to create an endpoint with</param>
    /// <param name="isBind">wether it should bind the endpoint (server) or connect the endpoint (client)</param>
    public NetMQPeer(string name, string ip, int port, bool isBind = false) : base() {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Peer name cannot be null or empty", nameof(name));

        _messageQueue = new NetMQQueue<Message>();
        _socket = new DealerSocket($"{(isBind ? '@' : '>')}tcp://{ip}:{port}");
        _poller = new NetMQPoller{_messageQueue};

        _poller.Add(_socket);
        _socket.ReceiveReady += HandleMessageReceived;
        _messageQueue.ReceiveReady += HandleSendMessage;
        _poller.RunAsync();

        Name = name;
        _socket.Options.Identity = Encoding.Unicode.GetBytes(Name);
    }

    ~NetMQPeer() {
        Dispose(false);
    }

    /// <summary>
    /// Sends a <see cref="Message"/> by placing it into the <see cref="NetMQQueue{Message}"/> for the <see cref="NetMQSocket"/> to process
    /// </summary>
    /// <param name="message">the <see cref="Message"/> to send</param>
    public void SendMessage(Message message) {
        _messageQueue.Enqueue(message);
    }

    /// <summary>
    /// Sends a <see cref="Message"/> by placing it into the <see cref="NetMQQueue{Message}"/> for the <see cref="NetMQSocket"/> to process
    /// </summary>
    /// <param name="args">the message args to send with</param>
    /// <returns>if it sent the message sucesfully</returns>
    public override bool SendMessage(SendMessageArgs args) {
        SendMessage(args.Message);
        return true;        
    }

    /// <summary>
    /// Handles receiving a message from the <see cref="NetMQSocket"/>
    /// </summary>
    /// <param name="sender">unused</param>
    /// <param name="args">the message data</param>
    protected void HandleMessageReceived(object? sender, NetMQSocketEventArgs args) {
        NetMQMessage messageData = args.Socket.ReceiveMultipartMessage(2);
        Message message = MessageFactory.GetMessage(messageData[1].Buffer);
        Publish(message);
    }

    /// <summary>
    /// Handles when a message in the <see cref="NetMQQueue{Message}"/> is ready to send
    /// </summary>
    /// <param name="sender">unsed</param>
    /// <param name="args">the message to send</param>
    protected void HandleSendMessage(object? sender, NetMQQueueEventArgs<Message> args) {
        if (!_messageQueue.TryDequeue(out Message message, TimeSpan.FromSeconds(5)) || message == default) {
            throw new Exception("Failed to dequeue send message for peer");
        }

        byte[] messageData = MessageFactory.GetMessageData(message);
        NetMQMessage messageToSend = new NetMQMessage();
        messageToSend.AppendEmptyFrame();
        messageToSend.Append(messageData);
        if (_socket.TrySendMultipartMessage(TimeSpan.FromSeconds(7), messageToSend))
            //message sent
            return;
        else
            throw new Exception("Failed to send message through peer connection");
    }

    /// <summary>
    /// Dispose of the <see cref="NetMQPeer"/> closing any sockets and freeing resources 
    /// </summary>
    public virtual void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal dispose method to handle freeing or closing any resources
    /// </summary>
    /// <param name="fromDispose">if it was called from <see cref="Dispose"/></param>
    protected virtual void Dispose(bool fromDispose) {
        if (!fromDispose) return;

        _poller.Stop();
        _poller.Dispose();
        _socket.Dispose();
    }
}