using NetMQ;
using NetMQ.Sockets;
using LettuceTalk.Core.MessageHandlers;
using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

/// <summary>
/// A <see cref="TalkingPoint"/> that uses <see cref="NetMQ"/> as the backing communication for the <see cref="LettuceTalk"/> Protocol
/// This serves as a base class for a specfic NetMQ backing <see cref="NetMQSocket"/>
/// </summary>
public abstract class NetMQTalker : TalkingPoint, IDisposable {
    protected readonly NetMQSocket _socket;
    protected readonly NetMQPoller _poller;
    protected readonly NetMQQueue<Message> _messageQueue;
    
    /// <summary>
    /// Creates an endpoint at the given ip and port
    /// </summary>
    /// <param name="ip">the string representation of the IP to create an endpoint with</param>
    /// <param name="port">the port to create an endpoint with</param>
    /// <param name="isBind">wether it should bind the endpoint (server) or connect the endpoint (client)</param>
    protected NetMQTalker(string ip, int port, bool isBind = true) : base() {
        _messageQueue = new NetMQQueue<Message>();
        _socket = GetSocket(ip, port, isBind);
        _poller = new NetMQPoller{_messageQueue};

        _poller.Add(_socket);
        _socket.ReceiveReady += HandleMessageReceived;
        _messageQueue.ReceiveReady += HandleSendMessage;
        _poller.RunAsync();
    }

    ~NetMQTalker() {
        Dispose(false);
    }

    /// <summary>
    /// Gets the specfic <see cref="NetMQSocket"/> to use for communication
    /// </summary>
    /// <param name="ip">the string representation of the IP to create an endpoint with</param>
    /// <param name="port">the port to create an endpoint with</param>
    /// <param name="isBind">wether it should bind the endpoint (server) or connect the endpoint (client)</param>
    /// <returns>the <see cref="NetMQSocket"/> to use</returns>
    protected abstract NetMQSocket GetSocket(string ip, int port, bool isBind);

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
    protected virtual void HandleMessageReceived(object? sender, NetMQSocketEventArgs args) {
        NetMQMessage messageData = args.Socket.ReceiveMultipartMessage(3);
        Message message = MessageFactory.GetMessage(messageData[2].Buffer);
        Publish(message);
    }

    /// <summary>
    /// Handles when a message in the <see cref="NetMQQueue{Message}"/> is ready to send
    /// </summary>
    /// <param name="sender">unsed</param>
    /// <param name="args">the message to send</param>
    protected virtual void HandleSendMessage(object? sender, NetMQQueueEventArgs<Message> args) { 
        if (!_messageQueue.TryDequeue(out Message message, TimeSpan.FromSeconds(5))) {
            // log dequeue warning
            return;
        }

        byte[] messageData = MessageFactory.GetMessageData(message);
        NetMQMessage messageToSend = new NetMQMessage();
        messageToSend.Push(_socket.Options.Identity);
        messageToSend.PushEmptyFrame();
        messageToSend.Push(messageData);
        if (_socket.TrySendMultipartMessage(TimeSpan.FromSeconds(7), messageToSend))
            //message sent
            return;
        else
            // log failure
            return;
    }

    /// <summary>
    /// Dispose of the <see cref="NetMQTalker"/> closing any sockets and freeing resources 
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