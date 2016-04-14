# LiteNetLib 

Lite reliable UDP library for .NET and Mono.
Minimal .NET version - 3.5

### Build status
[![](https://ci.appveyor.com/api/projects/status/354501wnvxs8kuh3/branch/master?svg=true)](https://ci.appveyor.com/project/RevenantX/litenetlib/branch/master)

## Features

* Simple connection handling
* Helper classes for sending and reading messages
* Different send mechanics
 * Reliable with order
 * Reliable without order
 * Ordered but unreliable with duplication prevention
 * Simple UDP packets without order and reliability
* Packet flow control
* Automatic fragmentation of reliable packets
* Automatic MTU detection
* UDP NAT hole punching
* NTP time requests
* Packet loss and latency simulation
* IPv6 support
* Small CPU and RAM usage
* Unity3d support (you can use library source in project)
* Universal Windows Platform (Windows 8.1 and Windows 10 including phones) support

## Usage samples

### Server
```csharp
EventBasedNetListener listener = new EventBasedNetListener();
NetServer server = new NetServer(listener, 2 /* maximum clients */, "SomeConnectionKey");
server.Start(9050 /* port */);

listener.PeerConnectedEvent += peer =>
{
    Console.WriteLine("We got connection: {0}", peer.EndPoint); // Show peer ip
    NetDataWriter writer = new NetDataWriter();                 // Create writer class
    writer.Put("Hello client!");                                // Put some string
    peer.Send(writer, SendOptions.ReliableOrdered);             // Send with reliability
};

while (!Console.KeyAvailable)
{
    server.PollEvents();
    Thread.Sleep(15);
}

server.Stop();
```
### Client
```csharp
EventBasedNetListener listener = new EventBasedNetListener();
NetClient client = new NetClient(listener, "SomeConnectionKey");
client.Start();
client.Connect("localhost" /* host ip or name */, 9050 /* port */);
listener.NetworkReceiveEvent += (fromPeer, dataReader) =>
{
    Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */);
};

while (!Console.KeyAvailable)
{
    client.PollEvents();
    Thread.Sleep(15);
}

client.Stop();
```
