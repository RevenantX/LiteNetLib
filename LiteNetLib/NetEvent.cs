namespace LiteNetLib
{
    public enum NetEventType
    {
        None,
        Connect,
        Disconnect,
        Receive,
        ReceiveUnconnected,
        Error
    }

    public sealed class NetEvent
    {
        public NetPeer Peer { get; internal set; }
        public byte[] Data { get; internal set; }
        public NetEventType Type { get; internal set; }
        public NetEndPoint RemoteEndPoint { get; internal set; }
    }
}
