# LiteNetLib 

Lite reliable UDP library for .NET, Mono, and .NET Core.
Minimal .NET version - 3.5

### Build status
[![](https://ci.appveyor.com/api/projects/status/354501wnvxs8kuh3/branch/master?svg=true)](https://ci.appveyor.com/project/RevenantX/litenetlib/branch/master)

### DLL build from master ( Warning! Master branch can be unstable! )
https://ci.appveyor.com/project/RevenantX/litenetlib/branch/master/artifacts

## Features

* Lightweight
 * Small CPU and RAM usage
 * Small packet size overhead ( 1 byte for unrealiable, 3 bytes for reliable packets )
* Simple connection handling
* Peer to peer connections
* Helper classes for sending and reading messages
* Different send mechanics
 * Reliable with order
 * Reliable without order
 * Ordered but unreliable with duplication prevention
 * Simple UDP packets without order and reliability
* Packet flow control
* Automatic small packets merging ( if enabled )
* Automatic fragmentation of reliable packets
* Automatic MTU detection
* UDP NAT hole punching
* NTP time requests
* Packet loss and latency simulation
* IPv6 support (dual mode)
* Connection statisitcs (need DEBUG or STATS_ENABLED flag)
* Multicasting (for discovering hosts in local network)
* Unity3d support (you can use library source in project)
* Supported platforms:
 * Windows/Mac/Linux (.net framework, Mono, .net core)
 * Android
 * iOS
 * Universal Windows (Windows 8.1 and Windows 10 including phones)

## Usage samples

### Server
```csharp
EventBasedNetListener listener = new EventBasedNetListener();
NetManager server = new NetManager(listener, 2 /* maximum clients */, "SomeConnectionKey");
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
NetManager client = new NetManager(listener, "SomeConnectionKey");
client.Start();
client.Connect("localhost" /* host ip or name */, 9050 /* port */);
listener.NetworkReceiveEvent += (fromPeer, dataReader) =>
{
    Console.WriteLine("We got: {0}", dataReader.GetString(100 /* max length of string */));
};

while (!Console.KeyAvailable)
{
    client.PollEvents();
    Thread.Sleep(15);
}

client.Stop();
```

### NetManager settings description

* **UnconnectedMessagesEnabled**
 * enable messages receiving without connection. (with SendUnconnectedMessage method)
 * default value: **false**
* **NatPunchEnabled**
 * enable nat punch messages
 * default value: **false**
* **UpdateTime**
 * library logic update (and send) period in milliseconds
 * default value: **100 msec**. For games you can use 15 msec (66 ticks per second)
* **ReliableResendTime**
 * time for resending lost reliable packets in milliseconds
 * default value: **500 msec**. Set that value to 4x-5x update time ( if UpdateTime = 15 then ReliableResendTime = 75 )
* **PingInterval**
 * Interval for latency detection and checking connection
 * default value: **1000 msec**.
* **DisconnectTimeout**
 * if client or server doesn't receive any packet from remote peer during this time then connection will be closed
 * (including library internal keepalive packets)
 * default value: **5000 msec**.
* **SimulatePacketLoss**
 * simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)
 * default value: **false**
* **SimulateLatency**
 * simulate latency by holding packets for random time. (Works only in DEBUG mode)
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
* **DiscoveryEnabled**
 * Allows receive DiscoveryRequests
 * default value: **false**
* **MergeEnabled**
 * Merge small packets into one before sending to reduce outgoing packets count. (May increase a bit outgoing data size)
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
