namespace LiteNetLib
{
    public interface INetChannel
    {
        void AddToQueue(NetPacket packet);
        NetPacket GetQueuedPacket();
    }
}
