namespace LiteNetLib
{
    public enum NetEventType
    {
        None,
        Connect,
        Disconnect,
        Receive,
        Error
    }

    public class NetEvent
    {
        public NetPeer peer;
        public byte[] data;
        public NetEventType type;

        public NetEvent(NetPeer peer, byte[] data, NetEventType type)
        {
            this.peer = peer;
            this.data = data;
            this.type = type;
        }
    }
}
