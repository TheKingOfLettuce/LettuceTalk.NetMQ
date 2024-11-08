# LettuceTalk.NetMQ
Integrates the LettuceTalk.Core protcol using NetMQ as the communication pipeline

Please see [NetMQ]()

## Quick Start
`LettuceTalk.NetMQ` is available on [nuget](https://www.nuget.org/packages/LettuceTalk.NetMQ)

### Inter-Process Communication
The first thing you need to do is create a message by inheriting from `Message` that also has a `MessageDataAttribute` decorated with it:

```csharp
[MessageData]
public class EchoMessage : Message {
    public readonly string EchoString;

    public EchoMessage(string echoString) {
        EchoString = echoString;
    }
}
```
Communication can be established in one of two ways:

**Peer-To-Peer**

Using `LettuceTalk.NetMQ.NetMQPeer`, you can establish two way communication between peers. In this example we will create two peers, one will bind the IP endpoint, while the other will connect to it.

Peer A will be our binder:

```csharp
public static class Program {
    public NetMQPeer PeerA;

    public static void Main() {
        MessageFactory.AssociateMessage(10, typeof(EchoMessage)); // associate EchoMessage with 10
        PeerA = new NetMQPeer("PeerA", "127.0.0.1", 1234, true); // create peer connection at localhost:1234, passing true to bind connection
        PeerA.Subscribe<EchoMessage>(HandleEchoMessage);
    }

    public static void HandleEchoMessage(EchoMessage message) {
        Console.WriteLine($"Received from PeerB : {message.EchoString}");
        PeerA.SendMessage(new EchoMessage("I got your message"));
    }
}
```

Peer B will be our connector:

```csharp
public static class Program {
    public NetMQPeer PeerB;

    public static void Main() {
        MessageFactory.AssociateMessage(10, typeof(EchoMessage)); // associate EchoMessage with 10
        PeerB = new NetMQPeer("PeerB", "127.0.0.1", 1234); // create peer connection at localhost:1234
        PeerB.Subscribe<EchoMessage>(HandleEchoMessage);
        Thread.Sleep(TimeSpan.FromSeconds(5)); // give time for PeerA to startup
        PeerB.SendMessage(new EchoMessage("Hello PeerA!"));
    }

    public static void HandleEchoMessage(EchoMessage message) {
        Console.WriteLine($"Received from PeerA : {message.EchoString}");
    }
}
```

Spin the two peers up in seperate executables and watch them communicate.

NOTE: While you can connect multiple peers to a single binded peer, message queueing will not be as you expect, especially when sending messages from the binded peer.

**Server/Client**

Using a single `LettuceTalk.NetMQ.NetMQServer`, you can establish communication to as many `LettuceTalk.NetMQ.NetMQPeer` as desired. Clients must register themselves to the server with a unique ID.

We will setup our server to track any clients who register to  periodically send a message to all clients and select a random client for a special message.

Server Code:

```csharp
public static class Program {
    public static NetMQServer Server;
    private static List<string> _clients = new List<string>();

    public static void Main() {
        MessageFactory.AssociateMessage<EchoMessage>(); // let IPC framework know about EchoMessage
        Server = new NetMQServer("127.0.0.1", 1234); // bind server to localhost:1234
        Server.OnClientRegistered += HandleClientRegistered; // listen for client registers
        Task.Run(RandomDeliveryLoop); // start our delivery loop
    }

    private static void HandleClientRegistered(string clientID) {
        Console.WriteLine($"Client {clientID} has registered");
        _clients.Add(clientID); // track registered clients
    }

    private static async void RandomDeliveryLoop() {
        Random rand = new Random();
        while (true) {
            await Task.Delay(TimeSpan.FromSeconds(10));
            Console.WriteLine("Sending message to all clients");
            Server.SendMessage(new SendClientMessageArgs(string.Empty, new EchoMessage("Hello from the server"))); // send a message to all clients by passing empty string for ID

            if (_clients.Count == 0) continue;
            int randIndex = rand.Next(0, _clients.Count);
            string randClientID = _clients[randIndex];
            Console.WriteLine($"Sending special message to client {randClientID}");
            Server.SendMessage(new SendClientMessageArgs(randClientID, new EchoMessage("Special Delivery from the server"))); // send a special message to random client ID
        }
    }
}
```

Next is our client code, which takes a single string argument for the client ID:

```csharp
public static class Program {
    public NetMQPeer PeerB;

    public static void Main(string[] args) {
        MessageFactory.AssociateMessage<EchoMessage>(); // let IPC framework know about EchoMessage
        PeerB = new NetMQPeer(args[0], "127.0.0.1", 1234); // create client connection at localhost:1234
        PeerB.Subscribe<EchoMessage>(HandleEchoMessage);
        PeerB.Subscribe<RegisterClientAck>(HandleRegisterAck)
        PeerB.SendMessage(new RegisterClient()); // send a registration message to the server
    }

    private static void HandleEchoMessage(EchoMessage message) {
        Console.WriteLine($"Received from Server : {message.EchoString}");
    }

    private static void HandleRegisterAck(RegisterClientAck message) {
        Console.WriteLine("Registration acknowledged from server");
    }
}
```

Spin up the server and launch multiple clients with unique IDs and watch the communcation.

It is possible for clients to send messages to the server, however an example for that is too lengthy for this "quick" start guide, a more lengthy tutorial will be made later.