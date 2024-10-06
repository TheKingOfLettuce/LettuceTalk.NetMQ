using NetMQ;
using NetMQ.Sockets;
using LettuceTalk.Core;
using LettuceTalk.Core.MessageHandlers;
using System.Text;

namespace LettuceTalk.NetMQ;

public class SendClientMessageArgs : SendMessageArgs {
    public readonly string ClientID;

    public SendClientMessageArgs(string clientID, Message message) : base(message) {
        if (clientID == null) {
            throw new ArgumentException("Cannot have clientID be null", nameof(clientID));
        }
        ClientID = clientID;
    }
}

public class NetMQServer : IDisposable {
    public event Action<string>? OnClientRegistered;

    protected readonly RouterSocket _socket;
    protected readonly NetMQPoller _poller;
    protected readonly NetMQQueue<SendClientMessageArgs> _messageQueue;

    protected readonly Dictionary<string, MessageCallbackHandler> _clients;

    public NetMQServer(string ip, int port) {
        _clients = new Dictionary<string, MessageCallbackHandler>();

        _messageQueue = new NetMQQueue<SendClientMessageArgs>();
        _socket = new RouterSocket($"tcp://{ip}:{port}");
        _poller = new NetMQPoller{_messageQueue};

        _poller.Add(_socket);
        _socket.ReceiveReady += HandleMessageReceived;
        _messageQueue.ReceiveReady += HandleSendMessage;
        _poller.RunAsync();
    }

    ~NetMQServer() {
        Dispose(false);
    }

    public void SendMessage(SendClientMessageArgs args) {
        // if empty string, send to all clients
        if (args.ClientID == string.Empty) {
            foreach(string clientID in _clients.Keys) {
                _messageQueue.Enqueue(new SendClientMessageArgs(clientID, args.Message));
            }
        }
        else {
            _messageQueue.Enqueue(args);
        }
    }

    protected void HandleSendMessage(object? sender, NetMQQueueEventArgs<SendClientMessageArgs> queueArgs) {
        if (!_messageQueue.TryDequeue(out SendClientMessageArgs args, TimeSpan.FromSeconds(5)) || args == default) {
            throw new Exception("Failed to dequeue send message for server");
        }

        if (!_clients.ContainsKey(args.ClientID)) {
            throw new ArgumentException($"Cannot send message to {args.ClientID}, they are not registered");
        }

        NetMQMessage message = new NetMQMessage();
        message.Append(Encoding.Unicode.GetBytes(args.ClientID));
        message.AppendEmptyFrame();
        message.Append(MessageFactory.GetMessageData(args.Message));
        if (!_socket.TrySendMultipartMessage(TimeSpan.FromSeconds(7), message))
            throw new Exception($"Failed to send message to {args.ClientID}");    
    }

    protected void HandleMessageReceived(object? sender, NetMQSocketEventArgs args) {
        NetMQMessage messageData = args.Socket.ReceiveMultipartMessage(3);
        string clientID = Encoding.Unicode.GetString(messageData[0].Buffer);
        Message message = MessageFactory.GetMessage(messageData[2].Buffer);
        if (message.GetType() == typeof(RegisterClient)) {
            if (_clients.ContainsKey(clientID))
                throw new ArgumentException($"Received duplicate client ID during registration: {clientID}");
            _clients[clientID] = new MessageCallbackHandler();
            OnClientRegistered?.Invoke(clientID);
        }
        else if (_clients.ContainsKey(clientID)) {
            _clients[clientID].Publish(message);
        }
        else {
            throw new ArgumentException($"Cannot handle message due to client not being registered: {clientID}");
        }
    }

    /// <summary>
    /// Dispose of the <see cref="NetMQSever"/> closing any sockets and freeing resources 
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