# Standard Network Library
High Performance, easy to use, network library supporting 16k+ concurrent clients. 
Provides infrastructure for high throughput message passing, P2P, Nat Traversal, Reliable Udp.
</br>This repository consist one main assembly and several serialization spesific implemtation assemblies. 
## Main Assembly
 Network Library, which is the core of entire network library. It has all the logic associated with the network system and sub systems, starting from raw bytes to abstractions such as P2P lobbies. It provides generic templates to be used with any serialization methodology. 

### Core Models
Plug&Play models, working with raw bytes.
- ```Tcp Server/Client model``` with dynamic buffering and queuing sub systems, bytes can come fragmented like regular sockets.
- ```Tcp Byte Message Server/Client``` model where bytes are sent with 4 byte lenght header. It ensure atomic message delivery without fragmentation.
- ```Udp Server/Client``` models optimised for performance.
- ```Reliable Udp Client/Server``` models where modern TCP protocol is implemented over Udp.
- Secure variants of all of the above(SSL with TLS for Tcp, Symetric Key AES for Udp).

### Templates
Involves generic clases which works with any serialization protocol.
- ```Generic Message Server/Client``` model which send and receives serialized messages atomically.
- ```Generic MessageProtocol Server/client``` model, similar to above, but with addition of "MessageEnvelope" carrier class, used as header/metadata.
- ```P2P Relay Client/Server``` Model where Peers(Clients) discover each other via Relay server, can use Udp/Tcp to communicate. Supports Udp Holepunch.
- ```P2P Room/Lobby Server/Client```, extention of Relay model where peers can define a room, similar to game matchmaking servers.

### Low Level Features

#### Advanced Memory Management
- Library implements "Shared Thread Local Memory Pool", where all byte arrays and the stream backing buffers are rented from. Memory is weak referenced. Pool provides minimum GC cycles and automatically trims on system memory pressure.
- As an example, RelayServer can relay 21 Gigabytes/s Udp traffic using 36 mb process memory with 0 GC Collects.

#### Message Buffering
- Since the System calls for socket operations are rather expensive, on Tcp models a buffering/queueing system is implemented. This system is active only during high load.
In a nutsell, if the socket is busy sending, next messages are collected and stiched together and sent as batch when the socket becomes available again. Its like Naggle, but without sacrificing the fast sends on moderate traffic.
- This improves the throughput of small messages quite significantly which will be shown on benchmarks.

Library is tested with as many clients as OS(Windows) supports (around 16k dynamic ports). Data reliability(includung RUDP) is tested over the internet extensively.
Nat Traversal Udp holepunching is also tested over the internet with great success.

Note: Libary has unsafe code and stack allocations. But these parts are well tested and not subject to change.

## Sub Assemblies 
Generic models from main assembly are implemented with the spesific serializers.
It includes:
- Protobuf-Net
- MessagePack
- NetSerializer
- System.Text.Json
 
## Supported Runtimes
- .NET Standard 2.0+

Nuget Packages are available:
|Core Network Library| Protobuf Network Library|MessagePack Network Library|NetSeralizer Network Library|Json Network Library|
|---------------|---------------|---------------|---------------|---------------|
|[![NuGet](https://img.shields.io/nuget/v/Standard.Network.Library)](https://www.nuget.org/packages/Standard.Network.Library)| [![NuGet](https://img.shields.io/nuget/v/Protobuf.Network.Library)](https://www.nuget.org/packages/Protobuf.Network.Library/)|[![NuGet](https://img.shields.io/nuget/v/MessagePack.Network.Library)](https://www.nuget.org/packages/MessagePack.Network.Library)|[![NuGet](https://img.shields.io/nuget/v/NetSerializer.Network.Library)](https://www.nuget.org/packages/NetSerializer.Network.Library)|[![NuGet](https://img.shields.io/nuget/v/Json.Network.Library)](https://www.nuget.org/packages/Json.Network.Library)



# Benchmarks

Infinite Echo benchmarks are done by sending set of messages to server and getting echo response. Each response causes new request. Each server response is counted as 1 Echo. 
- Separate client and server applications are used for the tests.
- Tests are done on my personal laptop with CPU AMD Ryzen 7 5800H.
- Benchmark programs are provided in the project.

### TCP/SSL Byte Message Server 
1000 seed messages (32 bytes message + 4 header) each:

|Mumber Of Clients|TCP Echo per Second|SSL Echo per Second
|---|---|---|
|100|53,400,000|41,600,000|
|1000|43,600,000|22,200,000|
|5000|43,400,000|21,800,000|
|10000|42,800,000|21,700,000|

### MessageProtocol 
1000 seed message envelopes ( 32 byte payload, 48 byte total):

|Mumber Of Clients|Protobuf Echo per Second|Secure Protobuf Echo per Second|
|---|---|---|
|100|9,440,000|8,050,000|
|1000|8,380,000|7,980,000|
|5000|8,360,000|7,950,000|
|10000|8,340,000|7,890,000|

#### Note
This benchmarks is only sending message envelope with raw byte payload. For serialization spesific performance please refer to:
[SerializationBenchmarks](SerializationBenchmarks.md)


# Code Samples & Documentation
## Byte Message TCP Server/Client
Any chunk of byte array or array segment will reach the destination without fragmentation.
```csharp
        private static void ExampleByteMessage()
        {
             ByteMessageTcpServer server = new ByteMessageTcpServer(20008);
            server.OnBytesReceived += ServerBytesReceived;
            server.StartServer();

            ByteMessageTcpClient client = new ByteMessageTcpClient();
            client.OnBytesReceived += ClientBytesReceived;
            client.Connect("127.0.0.1", 20008);

            client.SendAsync(Encoding.UTF8.GetBytes("Hello I'm a client!"));

            void ServerBytesReceived(Guid clientId, byte[] bytes, int offset, int count)
            {
                Console.WriteLine(Encoding.UTF8.GetString(bytes, offset, count));
                server.SendBytesToClient(clientId, Encoding.UTF8.GetBytes("Hello I'm the server"));
            }

            void ClientBytesReceived(byte[] bytes, int offset, int count)
            {
                Console.WriteLine(Encoding.UTF8.GetString(bytes, offset, count));
            }
        }
 ```
```Console
output:
Hello I'm a client!
Hello I'm the server
```
</br>For SSL variants difference is:
```c#
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   client = new SslByteMessageClient(ccert);
 
   var scert = new X509Certificate2("server.pfx", "greenpass");
   server = new SslByteMessageServer(8888,scert);
   
   // You can override the SSL cerificate validation callback
   server.RemoteCertificateValidationCallback+= ...
   client.RemoteCertificateValidationCallback+= ...
```
For base Server/Client where raw bytes are transfered you can use following classes. Method and callback signarures are identical to byte message models.
```c#
   AsyncTcpServer server = new AsyncTcpServer(port: 20000);
   AsyncTpcClient client = new AsyncTpcClient();
   
   // SSL variant
   var ccert = new X509Certificate2("client.pfx", "greenpass");
   var scert = new X509Certificate2("server.pfx", "greenpass");
   SslServer server = new SslServer(2000, scert);
   SslClient client = new SslClient(ccert);
```
There is no protocol implemented over base Server/Client described above, so bytes may come fragmented depending on your MTU size.
## Serialized Networks
Serialized Networks are implementations of generic classes provided by Standard Library
It is applicable to all serialization protocols. 
Examples here is only given for Protobuf-net,
but signature is identical for any other provided serialization protocol(MessagePack, Json etc).

## Pure Message Server/Client
Implements a server client model where serialized messages are transfered atomically.
Declare your type:
```c#
    [ProtoContract]
    class SampleMessage
    {
        [ProtoMember(1)]
        public string sample;
    }
```
```c#
    PureProtoServer server = new PureProtoServer(11234);
    server.StartServer();

    server.BytesReceived += (clientId,bytes, offset, count) => 
    {
        SampleMessage msg = server.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
        Console.WriteLine(msg.sample);
        msg.sample = "Jesse Lets cook";
        server.SendAsync(clientId,msg);
    };

    PureProtoClient client = new PureProtoClient();
    client.Connect("127.0.0.1", 11234);

    client.BytesReceived += (bytes, offset, count) =>
    {
        SampleMessage msg = client.Serializer.Deserialize<SampleMessage>(bytes, offset, count);
        Console.WriteLine(msg.sample);
    };

    client.SendAsync(new SampleMessage() { sample = "Yo! Mr White" });
```
## MessageProtocol Server/Client

Message protocol is something I came up with to wrap all types of messages with a standard header.
It is an extention of Pure Message Server/Client. Difference here is we have ```MessageEnvelope``` As a Carrier/Metadata/Header class.
```MessageEnvelope``` is serialized Statically, independently of serialization protocol.
As for the reason for it, please refer the document [SerializationBenchmarks](SerializationBenchmarks.md)

You can define a header, key-value pairs, as metadata and information about the payload.
Most importantly the Payload propery of the envelope carries the Inner message bytes. 
```UnpackPayload<T>()``` will deserialize the message on your type from the payload bytes.

It can also be raw bytes coming from some source (i.e. jpg image). Which you can set it by MessageEnvelope.SetPayload() method on the sender side.
You cant send both raw bytes and an serializable inner message with same envelope, Inner message will overwrite your payload.
This bytes are volatile (will be overwitten once code leaves the stack) unless you call LockBytes() method or copy manually. (important if you store it somewhere)


Its a simple implementation of a generic class from Standard Library.
```c#
 public class SecureProtoMessageServer : GenericSecureMessageServerWrapper<ProtoSerializer>
    {
        public SecureProtoMessageServer(int port, X509Certificate2 cerificate) : base(port, cerificate)
        {
        }
    }
```
### Usage
You can declare your payload types, which any type that is serializable with protobuf.
```c#
        [ProtoContract]
        class SamplePayload :IProtoMessage
        {
            [ProtoMember(1)]
            public string sample;
        }
```
Example for the Secure Variant:
``` c#
  private static async Task ExampleProtoSecure()
        {
            var scert = new X509Certificate2("server.pfx", "greenpass");
            var cert = new X509Certificate2("client.pfx", "greenpass");

            SecureProtoMessageServer server = new SecureProtoMessageServer(20008, scert);
            server.StartServer();
            server.OnMessageReceived += ServerMessageReceived;

            var client = new SecureProtoMessageClient(cert);
            client.OnMessageReceived += ClientMessageReceived;
            client.Connect("127.0.0.1", 20008);

            var Payload = new SamplePayload() { sample = "Hello" };
            var messageEnvelope = new MessageEnvelope();
            messageEnvelope.Header = "PayloadTest";

            // You can just send a message, get replies on ClientMessageReceived.
            client.SendAsyncMessage(messageEnvelope);
            client.SendAsyncMessage(messageEnvelope, Payload);

            // Or you can wait for a reply async.
            MessageEnvelope result = await client.SendMessageAndWaitResponse(messageEnvelope, Payload);
            var payload = result.UnpackPayload<SamplePayload>();
            Console.WriteLine($"Client Got Response {payload.sample}");

            void ServerMessageReceived(Guid clientId, MessageEnvelope message)
            {
                Console.WriteLine($"Server Received message {message.Header}");
                server.SendAsyncMessage(clientId, message);
            }

            void ClientMessageReceived(MessageEnvelope message)
            {
            }
        }
```
- Unsecure variants ``` ProtoMessageServer ``` and ``` ProtoMessageClient ``` has identical signarures except the constructors doesnt take a ceritificate

## Relay Server/Client and P2P

This model is what I personally use on my other projects such as P2PVideocall and Multiplayer Starfighter Game.
Basically you have a Relay server somewhere in your network, which can act as a local network hub in LAN and/or open to connections from internet if port forwarding is enabled. 
<br/>Relay clients (Peers) connect to Relay server and get notifications about existince of other peers. Peers can send messages to each other through Relay Server, or directly to each other (Udp holepunch).
<br/><img src="https://user-images.githubusercontent.com/109621184/204115163-3c8da2c3-9030-4325-9f4a-28935ed98977.png" width=50% height=50%>

### Relay server
Server is completely passive, allowing other peers to discover and send messages to each other. Additionally NAT traversal methods such as UDP holepunching provided to allow direct communication via Internet or LAN (UDP only so far, but we have reliable udp).
<br/> To use the Relay server, simply declere your server as:
``` c#
      var scert = new X509Certificate2("server.pfx", "greenpass");
      var server = new SecureProtoRelayServer(20010, scert);
      server.StartServer();
```
Relay server is already pre-configured.
### Relay Client
Relay client is where your application logic is implemented. You can web your client applications to discover and talk with each other.
</br>To declere a client:
``` c#
      var cert = new X509Certificate2("client.pfx", "greenpass");
      var client = new RelayClient(cert);

      client.OnPeerRegistered += (Guid peerId) => { // Save it to some concurrent dictionary etc..};
      client.OnPeerUnregistered += (Guid peerId) => { };
      client.OnMessageReceived += (MessageEnvelope message) => { // Handle your messages, 
                                                                 // I use switch case on message.Header };
      client.OnUdpMessageReceived += (MessageEnvelope message) => { };
      client.OnDisconnected += () => { };

      client.Connect("127.0.0.1", 20010);
```
Method signatures and callbacks are identical to proto client/server model (also with Payloads). Only difference is you have to specify the destination peer Guid Id. It comes from OnPeerRegistered event, whenever a new peer is connected to relay server. Relay Server guaranties syncronisation of current peer set with eventual consistency among all peers. So new peers will receive all other connected peers from this event and old peers will receive an update.
``` c#
      client.SendAsyncMessage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      client.SendUdpMesssage(destinationPeerId, new MessageEnvelope() { Header = "Hello" });
      // Or with an async reply
      MessageEnvelope response = await client.SendRequestAndWaitResponse(destinationPeerId,
                                            new MessageEnvelope() { Header = "Who Are You?" });
```
Additionally there is a broadcast support for TCP and Udp. This allows a single message to be send to relay server and multiplexed to all reachable peers there.
On Udp case, if holpucnh is active between 2 peers message is sent directly like regular message.
``` c#
      client.BroadcastMessage(peerId, envelope);
      client.BroadcastUdpMessage(peerId, envelope);
```
Udp messages can be more than the datagram limit of 65,527 bytes. The system detects large udp messages as Jumbo messages and sends them in chunks. Receiving end with will try to reconstruct the message. if all the part did not arrive on timeout message will be dropped. In a nutsell it works same as regular udp.
Max message size for udp is 16,256,000 bytes.
</br>This is also applicable to Udp broadcasts.
```c#
      client.SendUdpMesssage(destinationPeerId, new MessageEnvelope() { Header = "Hello" Payload = new byte[256000]});
```
We have build in reliable udp protocol implemneted aswell which is TCP implemented over UDP. 
```c#
      client.SendRudpMessage(peerId, envelope);
      client.SendRudpMessage(peerId, envelope, innerMessage);
      client.SendRudpMessageAndWaitResponse(peerId, envelope, innerMessage);
      client.SendRudpMessageAndWaitResponse(peerId, envelope);
```
On top of that 3 channels are provided for Rudp. Ch1,Ch2 and Realtime.
Ch1 and 2 are standard channels with same timeout mechanics of windows TCP implmentation.
</br> Realtime channel is more sensitive to timeouts and may resend messages "pre-emtively". but resends are much faster so data is delivered reliably with minimum delay.
```c#
      client.SendRudpMessage(peerId, envelope, RudpChannel.Ch1);
      client.SendRudpMessage(peerId, envelope, RudpChannel.Ch2);
      client.SendRudpMessage(peerId, envelope, RudpChannel.Realtime);
```
Following image shows a peer update diagram on connection to relay server:
<img src="https://user-images.githubusercontent.com/109621184/232113846-d70be35b-b5a9-4c72-8a0b-86d67d3cae49.png" width=60% height=60%>

Holepunch Support:
``` c#
      bool result = await client.RequestHolePunchAsync(destinationPeerId, timeOut:10000);
```
if succesfull, it will allow you to send direct udp messages between current and destination peers for the rest of the udp messages in both directions.

## Room/Lobby Server
This is an extention of Relay Server/Client. The addition is the room system where peers can create or join rooms, query available rooms, sends message to rooms(multicast).
Additionally keeping same message system to send 1-1 messages among peers.

You can join multiple rooms

Decleration of Server and client
``` c#
     var server = new SecureProtoRoomServer(20010, scert);
     server.StartServer();

     var client1 = new SecureProtoRoomClient(cert);
```
To create/join and leave rooms simply:
```c#
     client1.CreateOrJoinRoom("Kitchen");
     client1.LeaveRoom("Kitchen");

```
Room callbacks are as followed. This callbacks are only triggered if you are in the same room.
```c#
    client1.OnPeerJoinedRoom += (roomName, peerId) =>..
    client1.OnPeerLeftRoom += (roomName, peerId) =>..
    client1.OnPeerDisconnected +=(peerId) =>..
```
Additionally to the standard 1-1 message callback we have room message callbacks.
```c#
    client1.OnTcpRoomMesssageReceived += (roomName, message) => ..
    client1.OnUdpRoomMesssageReceived += (roomName, message) => ..
    client1.OnTcpMessageReceived += (message) => ..
    client1.OnUdpMessageReceived += (message) => ..

```
