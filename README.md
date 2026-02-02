# LiteNetLib 2

Lite reliable UDP library for .NET Standard 2.1 (Mono, .NET)

[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://stand-with-ukraine.pp.ua)

**HighLevel API Part**: [LiteEntitySystem](https://github.com/RevenantX/LiteEntitySystem)

**Discord chat**: [![Discord](https://img.shields.io/discord/501682175930925058.svg)](https://discord.gg/FATFPdy)

[OLD BRANCH (and examples) for 1.x](https://github.com/RevenantX/LiteNetLib/tree/1.x)

[Little Game Example on Unity](https://github.com/RevenantX/NetGameExample)

[Documentation](https://revenantx.github.io/LiteNetLib/index.html)

## Build

### [NuGet](https://www.nuget.org/packages/LiteNetLib/) [![NuGet](https://img.shields.io/nuget/v/LiteNetLib?color=blue)](https://www.nuget.org/packages/LiteNetLib/) [![NuGet](https://img.shields.io/nuget/vpre/LiteNetLib)](https://www.nuget.org/packages/LiteNetLib/#versions-body-tab) [![NuGet](https://img.shields.io/nuget/dt/LiteNetLib)](https://www.nuget.org/packages/LiteNetLib/) 

### [Release builds](https://github.com/RevenantX/LiteNetLib/releases) [![GitHub (pre-)release](https://img.shields.io/github/release/RevenantX/LiteNetLib/all.svg)](https://github.com/RevenantX/LiteNetLib/releases)

### [DLL build from master](https://ci.appveyor.com/project/RevenantX/litenetlib/branch/master/artifacts) [![](https://ci.appveyor.com/api/projects/status/354501wnvxs8kuh3/branch/master?svg=true)](https://ci.appveyor.com/project/RevenantX/litenetlib/branch/master)
( Warning! Master branch can be unstable! )

## Features

* Lightweight
  * Small CPU and RAM usage
  * Small packet size overhead ( 1 byte for unreliable, 4 bytes for reliable packets )
* Simple connection handling
* Peer to peer connections
* Helper classes for sending and reading messages
* Multiple data channels
* Different send mechanics
  * Reliable with order
  * Reliable without order
  * Reliable sequenced (reliable only last packet)
  * Ordered but unreliable with duplication prevention
  * Simple UDP packets without order and reliability
* Fast packet serializer [(Usage manual)](https://revenantx.github.io/LiteNetLib/articles/netserializerusage.html)
* Automatic small packets merging
* Automatic fragmentation of reliable packets
* Automatic MTU detection
* Optional CRC32C checksums
* UDP NAT hole punching
* NTP time requests
* Packet loss and latency simulation
* IPv6 support (using separate socket for performance)
* Connection statistics
* Multicasting (for discovering hosts in local network)
* Unity support
* Support for .NET8 optimized socket calls (much less gc)
* Supported platforms:
  * Windows/Mac/Linux (.NET Framework, Mono, .NET Core, .NET Standard)
  * Lumin OS (Magic Leap)
  * MonoGame
  * Godot
  * Unity 2021.2 (Desktop platforms, Android, iOS, Switch)
  
## Support developer
* [!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/revx)

* USDT TRC20: `TE5eBgq8SyEeZFKtCgZG9GwL34sANmbc67`

* USDT BEP20/ERC20: `0x4c0D6DC76c6A6B354f5ec6c9e51893fFC6510d1E`

* Bitcoin: `bc1q269ecs8r5vnrum5qr5j98sdglhnxlulv0f6egd`

## Unity notes!!!
* Minimal supported Unity is 2021.2. For older Unity versions use [0.9.x library](https://github.com/RevenantX/LiteNetLib/tree/0.9) versions
* Always use library sources or [OpenUPM package](https://openupm.com/packages/com.revenantx.litenetlib/) instead of precompiled DLL files ( because there are platform specific #ifdefs and workarounds for unity bugs )

## Usage samples

### Client
```csharp
var listener = new EventBasedNetListener();
var client = new NetManager(listener);
client.Start();
client.Connect("localhost" /* host IP or name */, 9050 /* port */, "SomeConnectionKey" /* text key or NetDataWriter */);
listener.NetworkReceiveEvent += (fromPeer, dataReader, deliveryMethod, channel) =>
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
var listener = new EventBasedNetListener();
var server = new NetManager(listener);
server.Start(9050 /* port */);

listener.ConnectionRequestEvent += request =>
{
    if(server.ConnectedPeersCount < 10 /* max connections */)
        request.AcceptIfKey("SomeConnectionKey");
    else
        request.Reject();
};

listener.PeerConnectedEvent += peer =>
{
    Console.WriteLine("We got connection: {0}", peer);  // Show peer IP
    var writer = new NetDataWriter();         // Create writer class
    writer.Put("Hello client!");                        // Put some string
    peer.Send(writer, DeliveryMethod.ReliableOrdered);  // Send with reliability
};

while (!Console.KeyAvailable)
{
    server.PollEvents();
    Thread.Sleep(15);
}
server.Stop();
```


