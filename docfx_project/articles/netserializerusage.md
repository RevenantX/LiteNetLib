# NetPacketProcessor
Fast specialized for network purposes serializer.<br>
It supports **classes** with **public properties with "get" and "set"** methods or **classes/structs which implements `INetSerializable`**.<br>
Serializer adds some overhead to packet size: 64 bit hash of class name and namespace (8 bytes). All other class fields will be as is in resulting packet.

## Serialization speed comparsion
Serialization 100000 times of simple structure from [example](https://github.com/RevenantX/LiteNetLib/blob/master/LibSample/SerializerBenchmark.cs) (`NET 4.5`):
Serializer|Time|Size
---|---|---|
BinaryFormatter|3334 ms|1096 bytes
NetSerializer (first run)|45 ms|204 bytes
NetSerializer (second run)|37 ms|204 bytes
Raw|24 ms|204 bytes

## Supported property types
```csharp
byte sbyte short ushort int uint long ulong float double bool string char IPEndPoint
```
Arrays of all these types (and custom types) are also supported. <br>
Enums are supported but work a bit slower than other types.

## Custom types
NetPacketProcessor doesn't support nested structs or classes, but you can register your own custom type processors.<br>
That useful for game engine types such as Vector3 and Quaternion (in Unity3d).

```csharp
// Your packet that will be sent over network
class SamplePacket
{
	// Both property and array are supported
    public MyType SomeMyType { get; set; }
    public MyType[] SomeMyTypes { get; set; } 
}

// Some custom type variant 1: Basic struct
struct MyType
{
    public int Value1;
    public string Value2;

    public static void Serialize(NetDataWriter writer, SomeMyType mytype)
    {
        writer.Put(mytype.Value1);
        writer.Put(mytype.Value2);
    }

    public static MyType Deserialize(NetDataReader reader)
    {
        MyType res = new MyType();
        res.Value1 = reader.GetInt();
        res.Value2 = reader.GetString();
        return res;
    }
}
...
netPacketProcessor = new NetPacketProcessor();
netPacketProcessor.RegisterNestedType( MyType.Serialize, MyType.Deserialize ); // Supply Serialization methods
```

You can also implement INetSerializable interface:
```csharp
// Some custom type variant 2: INetSerializable struct
struct MyType : INetSerializable
{
    public int Value1;
    public string Value2;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Value1);
        writer.Put(Value2);
    }

    public void Deserialize(NetDataReader reader)
    {
        Value1 = reader.GetInt();
        Value2 = reader.GetString();
    }
}
...
netPacketProcessor = new NetPacketProcessor();
netPacketProcessor.RegisterNestedType<MyType>(); // Serialization handled automatically thanks to INetSerializable
```

If you want to use a class instead of a struct you must implement the INetSerializable interface and provide a constructor: 
```csharp
// Some custom type variant 3: Class, must implement INetSerializable
class MyType : INetSerializable
{
    public int Value1;
    public string Value2;

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Value1);
        writer.Put(Value2);
    }

    public void Deserialize(NetDataReader reader)
    {
        Value1 = reader.GetInt();
        Value2 = reader.GetString();
    }
}
...
netPacketProcessor = new NetPacketProcessor();
netPacketProcessor.RegisterNestedType<MyType>(() => { return new SomeMyType(); }); // Must provide constructor
```

## Usage example
For full example look at source [SerializerBenchmark](https://github.com/RevenantX/LiteNetLib/blob/master/LibSample/SerializerBenchmark.cs)

### Packet
```csharp
class SamplePacket
{
	// All of these will be automatically serialized and deserialized
    public string SomeString { get; set; }
    public float SomeFloat { get; set; }
    public int[] SomeIntArray { get; set; }
}
```

### Sending / recieving
```csharp
// Client
class SomeClientListener : INetEventListener
{
   private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();
...
   public void OnPeerConnected(NetPeer peer)
   {
		// After connection is established you will have the server as a NetPeer
       SamplePacket packet = new SamplePacket
       {
           SomeFloat = 3.42f,
           SomeIntArray = new[] {6, 5, 4},
           SomeString = "Test String",
       }
       // Serialize the packet with NetSerializer and send it to the peer (server)
       peer.Send(_netPacketProcessor.Write(packet), DeliveryMethod.ReliableOrdered);
       //You can also use _netPacketProcessor.Send(peer, packet, DeliveryMethod.ReliableOrdered);
   }
}

// Server 
class SomeServerListener : INetEventListener
{
    private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();

    public SomeServerListener()
    {
        // Subscribe to recieving packets.        
        _netPacketProcessor.SubscribeReusable<SamplePacket, NetPeer>(OnSamplePacketReceived);
    }

	// Handler for SamplePacket, registered in constructor above
    private void OnSamplePacketReceived(SamplePacket samplePacket, NetPeer peer)
    {
        Console.WriteLine("[Server] ReceivedPacket:\n" + samplePacket.SomeString);
    }

	// INetEventListener function.
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        Console.WriteLine("[Server] received data. Processing...");
        // Deserializes packet and calls the handler registered in constructor
        _netPacketProcessor.ReadAllPackets(reader, peer);
    }
}
```

### Mini FAQ
Q: `NetPacketProcessor` throws "`Undefined packet in NetDataReader`" but all packets are registered. <br>
A: This can happen when packet definitions resides in different namespaces. Check that registered packet classes/structs are in the same namespace on both ends. 
To avoid this error altogether, use shared code/-assembly for packets.
