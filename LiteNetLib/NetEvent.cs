using LiteNetLib.Utils;

namespace LiteNetLib
{
    public enum NetEventType
    {
        Connect,
        Disconnect,
        Receive,
        ReceiveUnconnected,
        Error
    }

    public sealed class NetEvent
    {
        public NetPeer Peer { get; internal set; }
        public NetDataReader DataReader { get; internal set; }
        public byte[] Data { get { return DataReader.Data; } }
        public NetEventType Type { get; internal set; }
        public NetEndPoint RemoteEndPoint { get; internal set; }
        public string AdditionalInfo { get; internal set; }
    }
}
