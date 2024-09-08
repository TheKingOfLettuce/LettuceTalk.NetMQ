using NetMQ;
using NetMQ.Sockets;
using LettuceTalk.Core.MessageHandlers;
using LettuceTalk.Core;

namespace LettuceTalk.NetMQ;

public class NetMQTalker : TalkingPoint, IDisposable {
    public readonly string Name;

    protected readonly DealerSocket _socket;
    protected readonly NetMQPoller _poller;
    protected readonly NetMQQueue<Message> _messageQueue;
    
    protected NetMQTalker(string ip, int port, string name, bool isBind = true) : base() {
        Name = name;
        _messageQueue = new NetMQQueue<Message>();
        _socket = new DealerSocket($"{(isBind ? '@' : '>')}tcp://{ip}:{port}");
        _poller = new NetMQPoller{_messageQueue};

        _poller.Add(_socket);
        _socket.ReceiveReady += HandleMessageReceived;
        _messageQueue.ReceiveReady += HandleSendMessage;
        _poller.RunAsync();
    }

    ~NetMQTalker() {
        Dispose(false);
    }

    public void SendMessage(Message message) {
        _messageQueue.Enqueue(message);
    }

    public override bool SendMessage(SendMessageArgs args) {
        SendMessage(args.Message);
        return true;        
    }

    protected virtual void HandleMessageReceived(object? sender, NetMQSocketEventArgs args) {
        Message message = MessageFactory.GetMessage(args.Socket.ReceiveFrameBytes());
        Publish(message);
    }

    /// <summary>
    /// Send message, blocking until message has been sent
    /// </summary>
    /// <param name="message">the message to send</param>
    protected virtual void HandleSendMessage(object? sender, NetMQQueueEventArgs<Message> args) { 
        if (!_messageQueue.TryDequeue(out Message message, TimeSpan.FromSeconds(5))) {
            // log dequeue warning
            return;
        }

        byte[] messageData = MessageFactory.GetMessageData(message);
        if (_socket.TrySendFrame(TimeSpan.FromSeconds(7), messageData, messageData.Length))
            //message sent
            return;
        else
            // log failure
            return;
    }

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