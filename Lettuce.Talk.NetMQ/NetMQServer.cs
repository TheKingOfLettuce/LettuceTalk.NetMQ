using NetMQ;
using NetMQ.Sockets;
using LettuceTalk.Core;
using LettuceTalk.Core.MessageHandlers;
using System.Text;

namespace Lettuce.Talk.NetMQ;

/// <summary>
/// Send message args that contains an arugment for the ClientID to send to
/// </summary>
public class SendClientMessageArgs : SendMessageArgs {
    public readonly string ClientID;

    /// <param name="clientID">the client ID to send to, pass empty string to send to all clients</param>
    /// <param name="message">the message to send</param>
    /// <exception cref="ArgumentException"></exception>
    public SendClientMessageArgs(string clientID, Message message) : base(message) {
        if (clientID == null) {
            throw new ArgumentException("Cannot have clientID be null", nameof(clientID));
        }
        ClientID = clientID;
    }
}

/// <summary>
/// A <see cref="TalkingPoint"/> server that uses a <see cref="RouterSocket"/> to handle client connections.<para/>
/// Can send messages via <see cref="SendClientMessageArgs"/> to individual clients or all clients<para/><para/>
/// Messages received from unregistered clients are published to the <see cref="TalkingPoint"/>, otherwise they are published
/// to the <see cref="NetMQServer.GetClientCallbackHandler(string)"/>
/// </summary>
public class NetMQServer : TalkingPoint, IDisposable {
    public event Action<string>? OnClientRegistered;
    public event Action<string>? OnClientDeRegistered;

    protected readonly RouterSocket _socket;
    protected readonly NetMQPoller _poller;
    protected readonly NetMQQueue<SendClientMessageArgs> _messageQueue;

    private readonly Dictionary<string, ClientInstance> _clients;

    public NetMQServer(string ip, int port) {
        _clients = new Dictionary<string, ClientInstance>();

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

    /// <summary>
    /// Handles a generic version of sending message, ensuring the provided args are of type <see cref="SendClientMessageArgs"/>
    /// </summary>
    /// <param name="args">the send message args</param>
    /// <returns>if it could send the message successfully</returns>
    public override bool SendMessage(SendMessageArgs args) {
        if (args is not SendClientMessageArgs sendArgs)
            throw new InvalidCastException($"Could not convert {nameof(SendMessageArgs)} to {nameof(SendClientMessageArgs)}");
        SendMessage(sendArgs);
        return true;
    }

    /// <summary>
    /// Send a message to a specific client, use empty string to the ClientID to send to all
    /// </summary>
    /// <param name="args"></param>
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

    /// <summary>
    /// Pre-Registers a client with the given ID with a default callback instance
    /// </summary>
    /// <param name="clientID">the ID to register with</param>
    public void PreRegisterClient(string clientID, bool publishMessagesToServer) 
        => PreRegisterClient(clientID, new MessageCallbackHandler(), publishMessagesToServer);

    /// <summary>
    /// Pre-Registers a client with the given ID and callback instance
    /// </summary>
    /// <param name="clientID">the ID to register with</param>
    /// <param name="callbackHandler">the message callback handler to associate with</param>
    public void PreRegisterClient(string clientID, MessageCallbackHandler callbackHandler, bool publishMessagesToServer) {
        if (_clients.ContainsKey(clientID))
            throw new ArgumentException($"Received duplicate client ID during registration: {clientID}");
        _clients[clientID] = new ClientInstance(callbackHandler, publishMessagesToServer);
    }

    /// <summary>
    /// Deregisters a client with the given ID
    /// </summary>
    /// <param name="clientID">the client ID to remove</param>
    /// <exception cref="ArgumentException">if the client ID is not registered to the server</exception>
    public void DeregisterClient(string clientID) {
        if (!_clients.ContainsKey(clientID))
            throw new ArgumentException($"Cannot remove client {clientID}, it is not registered");

        OnClientDeRegistered?.Invoke(clientID);
        ClientInstance client = _clients[clientID];
        _ = _clients.Remove(clientID);
        // should clean up client instance here
        client.IsRegistered = false;
    }

    /// <summary>
    /// Gets a callback instance associated with the client ID
    /// </summary>
    /// <param name="clientID">the client ID to check</param>
    /// <returns>the message callback instance associated with the client</returns>
    public MessageCallbackHandler GetClientCallbackHandler(string clientID) {
        if (!_clients.ContainsKey(clientID))
            throw new ArgumentException("ClientID is not registered to the server");

        return _clients[clientID].CallbackHandler;
    }

    /// <summary>
    /// Handles when a message is ready to process and send in the queue
    /// </summary>
    /// <param name="sender">unused</param>
    /// <param name="queueArgs">unused but could be used to get the queue</param>
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

    /// <summary>
    /// Handles when the socket receives a message and is ready to process it
    /// </summary>
    /// <param name="sender">unused</param>
    /// <param name="args">the message data</param>
    protected void HandleMessageReceived(object? sender, NetMQSocketEventArgs args) {
        NetMQMessage messageData = args.Socket.ReceiveMultipartMessage(3);
        string clientID = Encoding.Unicode.GetString(messageData[0].Buffer);
        Message message = MessageFactory.GetMessage(messageData[2].Buffer);
        if (message is RegisterClient registerMessage) {
            if (registerMessage.PreRegisterClient) {
                PreRegisterClient(clientID, registerMessage.PublishMessagesToServer);
            }
            if (!_clients.ContainsKey(clientID)) {
                return;
            }
            _clients[clientID].IsRegistered = true;
            OnClientRegistered?.Invoke(clientID);
            SendMessage(new SendClientMessageArgs(clientID, new RegisterClientAck()));
        }
        else if (message is DeRegisterClient) {
            SendMessage(new SendClientMessageArgs(clientID, new DeRegisterClientAck()));
            DeregisterClient(clientID);
        }
        else if (_clients.ContainsKey(clientID)) {
            _clients[clientID].CallbackHandler.Publish(message);
            if (_clients[clientID].PublishMessagesToServer)
                Publish(message);
        }
        else {
            Publish(message);
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

internal class ClientInstance {
    public readonly MessageCallbackHandler CallbackHandler;
    public readonly bool PublishMessagesToServer;
    public bool IsRegistered;

    public ClientInstance(MessageCallbackHandler callbackHandler, bool publishMessagesToServer = false) {
        CallbackHandler = callbackHandler;
        PublishMessagesToServer = publishMessagesToServer;
    }
}