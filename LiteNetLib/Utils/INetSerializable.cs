namespace LiteNetLib.Utils
{
    public interface INetSerializable
    {
        void Serialize(NetDataWriter writer);
        void Desereialize(NetDataReader reader);
    }
}
