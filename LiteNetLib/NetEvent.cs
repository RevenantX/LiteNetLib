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
        public NetPeer Peer;
        public byte[] Data;
        public NetEventType Type;

        internal void Init(NetPeer peer, byte[] data, NetEventType type)
        {
            Peer = peer;
            Data = data;
            Type = type;
        }
    }
}
