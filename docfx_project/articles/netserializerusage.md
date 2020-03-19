# NetPacketProcessor
Fast specialized for network purposes serializer.<br>
It supports **classes** with **public properties with "get" and "set"** methods or **classes/structs with implemented `INetSerializable`**.<br>
Serializer adds some overhead in size: 64 bit hash of class name and namespace (8 bytes). All other class fields will be as is in resulting packet.
## Supported property types
```csharp
byte sbyte short ushort int uint long ulong float double bool string char IPEndPoint
```
Arrays of all this types and custom types also supported <br>
Enums also supported but work a bit slower than other types
## Serialization speed comparsion
Serialization 100000 times of simple structure from [example](https://github.com/RevenantX/LiteNetLib/blob/master/LibSample/SerializerBenchmark.cs) (`NET 4.5`):
Serializer|Time|Size
---|---|---|
BinaryFormatter|3334 ms|1096 bytes
NetSerializer (first run)|45 ms|204 bytes
NetSerializer (second run)|37 ms|204 bytes
Raw|24 ms|204 bytes
## Packet Example
```csharp
class SamplePacket
{
    public string SomeString { get; set; }
    public float SomeFloat { get; set; }
    public int[] SomeIntArray { get; set; }
}
```
## Custom field
NetPacketProcessor doesn't support nested structs or classes.<br>
But you can register custom type processor.<br>
That usefull for game engine types such as Vector3 and Quaternion (in Unity3d).
```csharp
//Your packet that will be sent over network
class SamplePacket
{
    public MyType SomeMyType { get; set; }
    //Arrays of custom types supported too
    public MyType[] SomeMyTypes { get; set; } 
}

//Some custom type (variant 1)
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
netPacketProcessor.RegisterNestedType( MyType.Serialize, MyType.Deserialize );
```
Another variant you can implement INetSerializable interface:
```csharp
//Some custom type (variant 2)
struct SomeMyType : INetSerializable
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
netPacketProcessor.RegisterNestedType<SomeMyType>();
```
Or if you want use struct instead of class (and implement INetSerializable interface)
you must provide constructor:
```csharp
netPacketProcessor.RegisterNestedType<SomeMyType>(() => { return new SomeMyType(); });
```
## Usage example (for full example look at source [SerializerBenchmark](https://github.com/RevenantX/LiteNetLib/blob/master/LibSample/SerializerBenchmark.cs))
```csharp
//First side
class SomeClientListener : INetEventListener
{
   private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();
...
   public void OnPeerConnected(NetPeer peer)
   {
       SamplePacket sp = new SamplePacket
       {
           SomeFloat = 3.42f,
           SomeIntArray = new[] {6, 5, 4},
           SomeString = "Test String",
       }
       peer.Send(_netPacketProcessor.Write(sp), DeliveryMethod.ReliableOrdered);
       //or you can use _netPacketProcessor.Send(peer, sp, DeliveryMethod.ReliableOrdered);
   }
}

//Other side 
class SomeServerListener : INetEventListener
{
    private readonly NetPacketProcessor _netPacketProcessor = new NetPacketProcessor();

    public SomeServerListener()
    {
        //Subscribe to packet receiving
        _netPacketProcessor.SubscribeReusable<SamplePacket, NetPeer>(OnSamplePacketReceived);
    }

    private void OnSamplePacketReceived(SamplePacket samplePacket, NetPeer peer)
    {
        Console.WriteLine("[Server] ReceivedPacket:\n" + samplePacket.SomeString);
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
    {
        Console.WriteLine("[Server] received data. Processing...");
        _netPacketProcessor.ReadAllPackets(reader, peer);
    }
}
```

### Mini FAQ

Q: `NetPacketProcessor` throws "`Undefined packet in NetDataReader`" but all packets registered <br>
A: check that registered packet classes in same namespace (and better use shared code for packets)