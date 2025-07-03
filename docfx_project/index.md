# LiteNetLib

Lite reliable UDP library for .NET Framework 3.5, Mono, .NET Core 2.0, .NET Standard 2.0.

[![Discord](https://img.shields.io/discord/501682175930925058.svg)](https://discord.gg/FATFPdy)

[Little Game Example on Unity](https://github.com/RevenantX/NetGameExample)

## Features

* Lightweight
  * Small CPU and RAM usage
  * Small packet size overhead ( 1 byte for unreliable, 3 bytes for reliable packets )
* Simple connection handling
* Peer to peer connections
* Helper classes for sending and reading messages
* Multiple data channels
* Different send mechanics
  * Reliable with order
  * Reliable without order
  * Reliable sequenced (realiable only last packet)
  * Ordered but unreliable with duplication prevention
  * Simple UDP packets without order and reliability
* Fast packet serializer [(Usage manual)](https://github.com/RevenantX/LiteNetLib/wiki/NetSerializer-usage)
* Automatic small packets merging
* Automatic fragmentation of reliable packets
* Automatic MTU detection
* UDP NAT hole punching
* NTP time requests
* Packet loss and latency simulation
* IPv6 support (dual mode)
* Connection statisitcs (need DEBUG or STATS_ENABLED flag)
* Multicasting (for discovering hosts in local network)
* Unity support
* Supported platforms:
  * Windows/Mac/Linux (.NET Framework, Mono, .NET Core)
  * Android (Unity)
  * iOS (Unity)
  * UWP Windows 10 including phones
  * Lumin OS (Magic Leap)

## Unity notes!!!
* Always use library sources instead of precompiled DLL files ( because there are platform specific #ifdefs and workarounds for unity bugs )

## Usage samples

### Client
```csharp
EventBasedNetListener listener = new EventBasedNetListener();
NetManager client = new NetManager(listener);
client.Start();
client.Connect("localhost" /* host ip or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);
listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod) =>
{
    Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */));
    dataReader.Recycle();
};

while (!Console.KeyAvailable)
{
    client.PollEvents();
    Thread.Sleep(15);
}

client.Stop();
```
### Server
```csharp
EventBasedNetListener listener = new EventBasedNetListener();
NetManager server = new NetManager(listener);
server.Start(9050 /* port */);

listener.ConnectionRequestEvent += request =>
{
    if(server.PeersCount < 10 /* max connections */)
        request.AcceptIfKey("SomeConnectionKey");
    else
        request.Reject();
};

listener.PeerConnectedEvent += peer =>
{
    Console.WriteLine("We got connection: {0}", peer.EndPoint); // Show peer ip
    NetDataWriter writer = new NetDataWriter();                 // Create writer class
    writer.Put("Hello client!");                                // Put some string
    peer.Send(writer, DeliveryMethod.ReliableOrdered);             // Send with reliability
};

while (!Console.KeyAvailable)
{
    server.PollEvents();
    Thread.Sleep(15);
}
server.Stop();
```

## NetManager settings description

* **UnconnectedMessagesEnabled**
  * enable messages receiving without connection. (with SendUnconnectedMessage method)
  * default value: **false**
* **NatPunchEnabled**
  * enable NAT punch messages
  * default value: **false**
* **UpdateTime**
  * library logic update (and send) period in milliseconds
  * default value: **15 msec**.
* **PingInterval**
  * Interval for latency detection and checking connection
  * default value: **1000 msec**.
* **DisconnectTimeout**
  * if client or server doesn't receive any packet from remote peer during this time then connection will be closed
  * (including library internal keepalive packets)
  * default value: **5000 msec**.
* **SimulatePacketLoss**
  * simulate packet loss by dropping random amout of packets. (Works only in DEBUG builds or when SIMULATE_NETWORK is defined)
  * default value: **false**
* **SimulateLatency**
  * simulate latency by holding packets for random time. (Works only in DEBUG builds or when SIMULATE_NETWORK is defined)
  * default value: **false**
* **SimulationPacketLossChance**
  * chance of packet loss when simulation enabled. value in percents.
  * default value: **10 (%)**
* **SimulationMinLatency**
  * minimum simulated latency
  * default value: **30 msec**
* **SimulationMaxLatency**
  * maximum simulated latency
  * default value: **100 msec**
* **BroadcastEnabled**
  * Allows receive Broadcast packets
  * default value: **false**
* **ReconnectDelay**
  * delay betwen connection attempts
  * default value: **500 msec**
* **MaxConnectAttempts**
  * maximum connection attempts before client stops and call disconnect event.
  * default value: **10**
* **UnsyncedEvents**
  * Experimental feature. Events automatically will be called without PollEvents method from another thread
  * default value: **false**
