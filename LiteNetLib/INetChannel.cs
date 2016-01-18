namespace LiteNetLib
{
    public interface INetChannel
    {
        //Send part
        void AddToQueue(NetPacket packet);
        NetPacket GetQueuedPacket();

        //Receive part
        bool ProcessPacket(NetPacket packet);
    }
}
