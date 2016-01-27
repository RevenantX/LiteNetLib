using LiteNetLib.Utils;

namespace LiteNetLib
{
    public enum NetEventType
    {
        None,
        Connect,
        Disconnect,
        Receive,
        ReceiveUnconnected,
        Error,
        NatIntroductionSuccess
    }

    public sealed class NetEvent
    {
        public NetPeer Peer { get; internal set; }
        public NetDataReader DataReader { get; internal set; }
        public NetEventType Type { get; internal set; }
        public NetEndPoint RemoteEndPoint { get; internal set; }
    }
}
