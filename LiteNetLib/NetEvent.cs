using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    /// <summary>
    /// Internally used event type
    /// </summary>
    public sealed class NetEvent
    {
        /// <summary>
        /// Reference to the next event in the pool or event queue.
        /// </summary>
        public NetEvent Next;

        /// <summary>
        /// Specifies the category of the network event.
        /// </summary>
        public enum EType
        {
            /// <summary>New peer connected.</summary>
            Connect,
            /// <summary>Peer disconnected.</summary>
            Disconnect,
            /// <summary>Data received from a connected peer.</summary>
            Receive,
            /// <summary>Unconnected message received.</summary>
            ReceiveUnconnected,
            /// <summary>Socket or internal protocol error occurred.</summary>
            Error,
            /// <summary>Round-trip time (RTT) for a peer has been updated.</summary>
            ConnectionLatencyUpdated,
            /// <summary>Broadcast message received.</summary>
            Broadcast,
            /// <summary>Incoming connection request from a new peer.</summary>
            ConnectionRequest,
            /// <summary>Reliable message was successfully delivered to the remote peer.</summary>
            MessageDelivered,
            /// <summary>The IP address or port of an existing peer has changed (e.g., roaming).</summary>
            PeerAddressChanged
        }

        /// <summary>
        /// The type of network event that occurred.
        /// </summary>
        public EType Type;

        /// <summary>
        /// The peer associated with this event. <see langword="null"/> for unconnected events.
        /// </summary>
        public LiteNetPeer Peer;

        /// <summary>
        /// The remote endpoint (IP and Port) from which the event originated.
        /// </summary>
        public IPEndPoint RemoteEndPoint;

        /// <summary>
        /// Optional user data associated with a connection request or disconnect.
        /// </summary>
        public object UserData;

        /// <summary>
        /// The updated latency value in milliseconds. Only valid when <see cref="Type"/> is <see cref="EType.ConnectionLatencyUpdated"/>.
        /// </summary>
        public int Latency;

        /// <summary>
        /// The specific socket error. Only valid when <see cref="Type"/> is <see cref="EType.Error"/>.
        /// </summary>
        public SocketError ErrorCode;

        /// <summary>
        /// The reason for a peer's disconnection. Only valid when <see cref="Type"/> is <see cref="EType.Disconnect"/>.
        /// </summary>
        public DisconnectReason DisconnectReason;

        /// <summary>
        /// Information about an incoming connection. Only valid when <see cref="Type"/> is <see cref="EType.ConnectionRequest"/>.
        /// </summary>
        public LiteConnectionRequest ConnectionRequest;

        /// <summary>
        /// The delivery method used for the received packet. Only valid when <see cref="Type"/> is <see cref="EType.Receive"/>.
        /// </summary>
        public DeliveryMethod DeliveryMethod;

        /// <summary>
        /// The channel on which the packet was received.
        /// </summary>
        public byte ChannelNumber;

        /// <summary>
        /// A reader for accessing the payload of received data, broadcast, or unconnected messages.
        /// </summary>
        public readonly NetPacketReader DataReader;

        /// <summary>
        /// Initializes a new instance of the <see cref="NetEvent"/> class.
        /// </summary>
        /// <param name="manager">The <see cref="LiteNetManager"/> that owns the packet pool and buffers for this event.</param>
        public NetEvent(LiteNetManager manager)
        {
            DataReader = new NetPacketReader(manager, this);
        }
    }
}
