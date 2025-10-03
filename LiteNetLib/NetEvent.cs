using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    /// <summary>
    /// Internally used event type
    /// </summary>
    public sealed class NetEvent
    {
        public NetEvent Next;

        public enum EType
        {
            Connect,
            Disconnect,
            Receive,
            ReceiveUnconnected,
            Error,
            ConnectionLatencyUpdated,
            Broadcast,
            ConnectionRequest,
            MessageDelivered,
            PeerAddressChanged
        }
        public EType Type;

        public LiteNetPeer Peer;
        public IPEndPoint RemoteEndPoint;
        public object UserData;
        public int Latency;
        public SocketError ErrorCode;
        public DisconnectReason DisconnectReason;
        public ConnectionRequest ConnectionRequest;
        public DeliveryMethod DeliveryMethod;
        public byte ChannelNumber;
        public readonly NetPacketReader DataReader;

        public NetEvent(LiteNetManager manager)
        {
            DataReader = new NetPacketReader(manager, this);
        }
    }
}
