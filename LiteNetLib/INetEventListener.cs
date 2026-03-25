using System.Net;
using System.Net.Sockets;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    /// <summary>
    /// Type of message that you receive in OnNetworkReceiveUnconnected event
    /// </summary>
    public enum UnconnectedMessageType
    {
        BasicMessage,
        Broadcast
    }

    /// <summary>
    /// Disconnect reason that you receive in OnPeerDisconnected event
    /// </summary>
    public enum DisconnectReason
    {
        /// <summary>
        /// Connection to host failed
        /// </summary>
        ConnectionFailed,

        /// <summary>
        /// Timeout
        /// </summary>
        Timeout,

        HostUnreachable,
        NetworkUnreachable,

        /// <summary>
        /// Remote host disconnected peer
        /// </summary>
        RemoteConnectionClose,

        /// <summary>
        /// Disconnect called locally
        /// </summary>
        DisconnectPeerCalled,

        /// <summary>
        /// Connection rejected by remote host
        /// </summary>
        ConnectionRejected,

        InvalidProtocol,
        UnknownHost,
        Reconnect,
        PeerToPeerConnection,
        PeerNotFound
    }

    /// <summary>
    /// Additional information about disconnection
    /// </summary>
    public struct DisconnectInfo
    {
        /// <summary>
        /// Additional info why peer disconnected
        /// </summary>
        public DisconnectReason Reason;

        /// <summary>
        /// Error code (if reason is SocketSendError or SocketReceiveError)
        /// </summary>
        public SocketError SocketErrorCode;

        /// <summary>
        /// Additional data that can be accessed (only if reason is RemoteConnectionClose)
        /// </summary>
        public NetPacketReader AdditionalData;
    }

    /// <summary>
    /// Interface for implementing own INetEventListener. This is a bit faster than use EventBasedListener
    /// </summary>
    public interface INetEventListener
    {
        /// <summary>
        /// New remote peer connected to host, or client connected to remote host
        /// </summary>
        /// <param name="peer">Connected peer object</param>
        void OnPeerConnected(NetPeer peer);

        /// <summary>
        /// Peer disconnected
        /// </summary>
        /// <param name="peer">disconnected peer</param>
        /// <param name="disconnectInfo">additional info about reason, errorCode or data received with disconnect message</param>
        void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);

        /// <summary>
        /// Network error (on send or receive)
        /// </summary>
        /// <param name="endPoint">From endPoint (can be null)</param>
        /// <param name="socketError">Socket error</param>
        void OnNetworkError(IPEndPoint endPoint, SocketError socketError);

        /// <summary>
        /// Received some data
        /// </summary>
        /// <param name="peer">From peer</param>
        /// <param name="reader">DataReader containing all received data</param>
        /// <param name="channelNumber">Number of channel at which packet arrived</param>
        /// <param name="deliveryMethod">Type of received packet</param>
        void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Received unconnected message
        /// </summary>
        /// <param name="remoteEndPoint">From address (IP and Port)</param>
        /// <param name="reader">Message data</param>
        /// <param name="messageType">Message type (simple, discovery request or response)</param>
        void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);

        /// <summary>
        /// Latency information updated
        /// </summary>
        /// <param name="peer">Peer with updated latency</param>
        /// <param name="latency">latency value in milliseconds</param>
        void OnNetworkLatencyUpdate(NetPeer peer, int latency);

        /// <summary>
        /// On peer connection requested
        /// </summary>
        /// <param name="request">Request information (EndPoint, internal id, additional data)</param>
        void OnConnectionRequest(ConnectionRequest request);

        /// <summary>
        /// On reliable message delivered
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userData"></param>
        void OnMessageDelivered(NetPeer peer, object userData) { }

        /// <summary>
        /// Ntp response
        /// </summary>
        /// <param name="packet"></param>
        void OnNtpResponse(NtpPacket packet) { }

        /// <summary>
        /// Called when peer address changed (when AllowPeerAddressChange is enabled)
        /// </summary>
        /// <param name="peer">Peer that changed address (with new address)</param>
        /// <param name="previousAddress">previous IP</param>
        void OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) { }
    }

    /// <summary>
    /// Interface for implementing own ILiteNetEventListener. This is a bit faster than use EventBasedListener
    /// </summary>
    public interface ILiteNetEventListener
    {
        /// <summary>
        /// New remote peer connected to host, or client connected to remote host
        /// </summary>
        /// <param name="peer">Connected peer object</param>
        void OnPeerConnected(LiteNetPeer peer);

        /// <summary>
        /// Peer disconnected
        /// </summary>
        /// <param name="peer">disconnected peer</param>
        /// <param name="disconnectInfo">additional info about reason, errorCode or data received with disconnect message</param>
        void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo);

        /// <summary>
        /// Network error (on send or receive)
        /// </summary>
        /// <param name="endPoint">From endPoint (can be null)</param>
        /// <param name="socketError">Socket error</param>
        void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

        /// <summary>
        /// Received some data
        /// </summary>
        /// <param name="peer">From peer</param>
        /// <param name="reader">DataReader containing all received data</param>
        /// <param name="deliveryMethod">Type of received packet</param>
        void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Received unconnected message
        /// </summary>
        /// <param name="remoteEndPoint">From address (IP and Port)</param>
        /// <param name="reader">Message data</param>
        /// <param name="messageType">Message type (simple, discovery request or response)</param>
        void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }

        /// <summary>
        /// Latency information updated
        /// </summary>
        /// <param name="peer">Peer with updated latency</param>
        /// <param name="latency">latency value in milliseconds</param>
        void OnNetworkLatencyUpdate(LiteNetPeer peer, int latency) { }

        /// <summary>
        /// On peer connection requested
        /// </summary>
        /// <param name="request">Request information (EndPoint, internal id, additional data)</param>
        void OnConnectionRequest(LiteConnectionRequest request);

        /// <summary>
        /// Called when peer address changed (when AllowPeerAddressChange is enabled)
        /// </summary>
        /// <param name="peer">Peer that changed address (with new address)</param>
        /// <param name="previousAddress">previous IP</param>
        void OnPeerAddressChanged(LiteNetPeer peer, IPEndPoint previousAddress) { }

        /// <summary>
        /// On reliable message delivered
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userData"></param>
        void OnMessageDelivered(LiteNetPeer peer, object userData) { }
    }

    /// <summary>
    /// Simple event based listener for simple setups and benchmarks
    /// </summary>
    public class EventBasedNetListener : INetEventListener
    {
        /// <summary>
        /// Delegate for the event that occurs when a new peer has successfully connected.
        /// </summary>
        /// <param name="peer">The connected peer.</param>
        public delegate void OnPeerConnected(NetPeer peer);
        /// <summary>
        /// Delegate for the event that occurs when a peer disconnects or the connection is lost.
        /// </summary>
        /// <param name="peer">The disconnected peer.</param>
        /// <param name="disconnectInfo">Information regarding the reason and data associated with the disconnection.</param>
        public delegate void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
        /// <summary>
        /// Delegate for the event that occurs when a network error is detected in the underlying socket.
        /// </summary>
        /// <param name="endPoint">The endpoint associated with the error.</param>
        /// <param name="socketError">The specific socket error code.</param>
        public delegate void OnNetworkError(IPEndPoint endPoint, SocketError socketError);
        /// <summary>
        /// Delegate for the event that occurs when data is received from a connected peer.
        /// </summary>
        /// <param name="peer">The peer that sent the data.</param>
        /// <param name="reader">The reader containing the received payload.</param>
        /// <param name="channel">The channel on which the data was received.</param>
        /// <param name="deliveryMethod">The delivery method used for this packet.</param>
        public delegate void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod);
        /// <summary>
        /// Delegate for the event that occurs when a message is received from an unconnected endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The endpoint that sent the message.</param>
        /// <param name="reader">The reader containing the received payload.</param>
        /// <param name="messageType">The type of unconnected message (e.g., Discovery or UnconnectedData).</param>
        public delegate void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);
        /// <summary>
        /// Delegate for the event that occurs when the round-trip time (RTT) to a peer is updated.
        /// </summary>
        /// <param name="peer">The peer whose latency was updated.</param>
        /// <param name="latency">The new latency value in milliseconds.</param>
        public delegate void OnNetworkLatencyUpdate(NetPeer peer, int latency);
        /// <summary>
        /// Delegate for the event that occurs when a new connection request is received.
        /// </summary>
        /// <param name="request">The connection request object used to accept or reject the connection.</param>
        public delegate void OnConnectionRequest(ConnectionRequest request);
        /// <summary>
        /// Delegate for the event that occurs when a reliable packet is successfully delivered or acknowledged.
        /// </summary>
        /// <param name="peer">The peer that received the packet.</param>
        /// <param name="userData">The custom user data that was attached to the sent packet.</param>
        public delegate void OnDeliveryEvent(NetPeer peer, object userData);
        /// <summary>
        /// Delegate for the event that occurs when an NTP response is received from a time server.
        /// </summary>
        /// <param name="packet">The NTP packet containing time information.</param>
        public delegate void OnNtpResponseEvent(NtpPacket packet);
        /// <summary>
        /// Delegate for the event that occurs when a peer's remote address changes (roaming).
        /// </summary>
        /// <param name="peer">The peer whose address changed.</param>
        /// <param name="previousAddress">The previous IP endpoint of the peer.</param>
        public delegate void OnPeerAddressChangedEvent(NetPeer peer, IPEndPoint previousAddress);

        /// <summary>
        /// Occurs when a new peer has successfully connected.
        /// </summary>
        public event OnPeerConnected PeerConnectedEvent;
        /// <summary>
        /// Occurs when a peer disconnects or the connection is lost.
        /// </summary>
        public event OnPeerDisconnected PeerDisconnectedEvent;
        /// <summary>
        /// Occurs when a network error is detected in the underlying socket.
        /// </summary>
        public event OnNetworkError NetworkErrorEvent;
        /// <summary>
        /// Occurs when data is received from a connected peer.
        /// </summary>
        public event OnNetworkReceive NetworkReceiveEvent;
        /// <summary>
        /// Occurs when a message is received from an unconnected endpoint.
        /// </summary>
        public event OnNetworkReceiveUnconnected NetworkReceiveUnconnectedEvent;
        /// <summary>
        /// Occurs when the round-trip time (RTT) to a peer is updated.
        /// </summary>
        public event OnNetworkLatencyUpdate NetworkLatencyUpdateEvent;
        /// <summary>
        /// Occurs when a new connection request is received.
        /// </summary>
        public event OnConnectionRequest ConnectionRequestEvent;
        /// <summary>
        /// Occurs when a reliable packet is successfully delivered or acknowledged.
        /// </summary>
        public event OnDeliveryEvent DeliveryEvent;
        /// <summary>
        /// Occurs when an NTP response is received.
        /// </summary>
        public event OnNtpResponseEvent NtpResponseEvent;
        /// <summary>
        /// Occurs when a peer's remote address changes.
        /// </summary>
        public event OnPeerAddressChangedEvent PeerAddressChangedEvent;

        /// <summary> Clears all subscribers from <see cref="PeerConnectedEvent"/>. </summary>
        public void ClearPeerConnectedEvent() => PeerConnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="PeerDisconnectedEvent"/>. </summary>
        public void ClearPeerDisconnectedEvent() => PeerDisconnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkErrorEvent"/>. </summary>
        public void ClearNetworkErrorEvent() => NetworkErrorEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkReceiveEvent"/>. </summary>
        public void ClearNetworkReceiveEvent() => NetworkReceiveEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkReceiveUnconnectedEvent"/>. </summary>
        public void ClearNetworkReceiveUnconnectedEvent() => NetworkReceiveUnconnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkLatencyUpdateEvent"/>. </summary>
        public void ClearNetworkLatencyUpdateEvent() => NetworkLatencyUpdateEvent = null;
        /// <summary> Clears all subscribers from <see cref="ConnectionRequestEvent"/>. </summary>
        public void ClearConnectionRequestEvent() => ConnectionRequestEvent = null;
        /// <summary> Clears all subscribers from <see cref="DeliveryEvent"/>. </summary>
        public void ClearDeliveryEvent() => DeliveryEvent = null;
        /// <summary> Clears all subscribers from <see cref="NtpResponseEvent"/>. </summary>
        public void ClearNtpResponseEvent() => NtpResponseEvent = null;
        /// <summary> Clears all subscribers from <see cref="PeerAddressChangedEvent"/>. </summary>
        public void ClearPeerAddressChangedEvent() => PeerAddressChangedEvent = null;

        void INetEventListener.OnPeerConnected(NetPeer peer) =>
            PeerConnectedEvent?.Invoke(peer);

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) =>
            PeerDisconnectedEvent?.Invoke(peer, disconnectInfo);

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode) =>
            NetworkErrorEvent?.Invoke(endPoint, socketErrorCode);

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) =>
            NetworkReceiveEvent?.Invoke(peer, reader, channelNumber, deliveryMethod);

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) =>
            NetworkReceiveUnconnectedEvent?.Invoke(remoteEndPoint, reader, messageType);

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) =>
            NetworkLatencyUpdateEvent?.Invoke(peer, latency);

        void INetEventListener.OnConnectionRequest(ConnectionRequest request) =>
            ConnectionRequestEvent?.Invoke(request);

        void INetEventListener.OnMessageDelivered(NetPeer peer, object userData) =>
            DeliveryEvent?.Invoke(peer, userData);

        void INetEventListener.OnNtpResponse(NtpPacket packet) =>
            NtpResponseEvent?.Invoke(packet);

        void INetEventListener.OnPeerAddressChanged(NetPeer peer, IPEndPoint previousAddress) =>
            PeerAddressChangedEvent?.Invoke(peer, previousAddress);
    }

        /// <summary>
    /// Simple event based listener for simple setups and benchmarks
    /// </summary>
    public class EventBasedLiteNetListener : ILiteNetEventListener
    {
        /// <summary>
        /// Delegate for the event that occurs when a new peer has successfully connected.
        /// </summary>
        /// <param name="peer">The connected peer.</param>
        public delegate void OnPeerConnected(LiteNetPeer peer);
        /// <summary>
        /// Delegate for the event that occurs when a peer disconnects or the connection is lost.
        /// </summary>
        /// <param name="peer">The disconnected peer.</param>
        /// <param name="disconnectInfo">Information regarding the reason and data associated with the disconnection.</param>
        public delegate void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo);
        /// <summary>
        /// Delegate for the event that occurs when a network error is detected in the underlying socket.
        /// </summary>
        /// <param name="endPoint">The endpoint associated with the error.</param>
        /// <param name="socketError">The specific socket error code.</param>
        public delegate void OnNetworkError(IPEndPoint endPoint, SocketError socketError);
        /// <summary>
        /// Delegate for the event that occurs when data is received from a connected peer.
        /// </summary>
        /// <param name="peer">The peer that sent the data.</param>
        /// <param name="reader">The reader containing the received payload.</param>
        /// <param name="deliveryMethod">The delivery method used for this packet.</param>
        public delegate void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod);
        /// <summary>
        /// Delegate for the event that occurs when a message is received from an unconnected endpoint.
        /// </summary>
        /// <param name="remoteEndPoint">The endpoint that sent the message.</param>
        /// <param name="reader">The reader containing the received payload.</param>
        /// <param name="messageType">The type of unconnected message (e.g., Discovery or UnconnectedData).</param>
        public delegate void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);
        /// <summary>
        /// Delegate for the event that occurs when the round-trip time (RTT) to a peer is updated.
        /// </summary>
        /// <param name="peer">The peer whose latency was updated.</param>
        /// <param name="latency">The new latency value in milliseconds.</param>
        public delegate void OnNetworkLatencyUpdate(LiteNetPeer peer, int latency);
        /// <summary>
        /// Delegate for the event that occurs when a new connection request is received.
        /// </summary>
        /// <param name="request">The connection request object used to accept or reject the connection.</param>
        public delegate void OnConnectionRequest(LiteConnectionRequest request);
        /// <summary>
        /// Delegate for the event that occurs when a reliable packet is successfully delivered or acknowledged.
        /// </summary>
        /// <param name="peer">The peer that received the packet.</param>
        /// <param name="userData">The custom user data that was attached to the sent packet.</param>
        public delegate void OnDeliveryEvent(LiteNetPeer peer, object userData);
        /// <summary>
        /// Delegate for the event that occurs when a peer's remote address changes (roaming).
        /// </summary>
        /// <param name="peer">The peer whose address changed.</param>
        /// <param name="previousAddress">The previous IP endpoint of the peer.</param>
        public delegate void OnPeerAddressChangedEvent(LiteNetPeer peer, IPEndPoint previousAddress);

        /// <summary>
        /// Occurs when a new peer has successfully connected.
        /// </summary>
        public event OnPeerConnected PeerConnectedEvent;
        /// <summary>
        /// Occurs when a peer disconnects or the connection is lost.
        /// </summary>
        public event OnPeerDisconnected PeerDisconnectedEvent;
        /// <summary>
        /// Occurs when a network error is detected in the underlying socket.
        /// </summary>
        public event OnNetworkError NetworkErrorEvent;
        /// <summary>
        /// Occurs when data is received from a connected peer.
        /// </summary>
        public event OnNetworkReceive NetworkReceiveEvent;
        /// <summary>
        /// Occurs when a message is received from an unconnected endpoint.
        /// </summary>
        public event OnNetworkReceiveUnconnected NetworkReceiveUnconnectedEvent;
        /// <summary>
        /// Occurs when the round-trip time (RTT) to a peer is updated.
        /// </summary>
        public event OnNetworkLatencyUpdate NetworkLatencyUpdateEvent;
        /// <summary>
        /// Occurs when a new connection request is received.
        /// </summary>
        public event OnConnectionRequest ConnectionRequestEvent;
        /// <summary>
        /// Occurs when a reliable packet is successfully delivered or acknowledged.
        /// </summary>
        public event OnDeliveryEvent DeliveryEvent;
        /// <summary>
        /// Occurs when a peer's remote address changes.
        /// </summary>
        public event OnPeerAddressChangedEvent PeerAddressChangedEvent;

        /// <summary> Clears all subscribers from <see cref="PeerConnectedEvent"/>. </summary>
        public void ClearPeerConnectedEvent() => PeerConnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="PeerDisconnectedEvent"/>. </summary>
        public void ClearPeerDisconnectedEvent() => PeerDisconnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkErrorEvent"/>. </summary>
        public void ClearNetworkErrorEvent() => NetworkErrorEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkReceiveEvent"/>. </summary>
        public void ClearNetworkReceiveEvent() => NetworkReceiveEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkReceiveUnconnectedEvent"/>. </summary>
        public void ClearNetworkReceiveUnconnectedEvent() => NetworkReceiveUnconnectedEvent = null;
        /// <summary> Clears all subscribers from <see cref="NetworkLatencyUpdateEvent"/>. </summary>
        public void ClearNetworkLatencyUpdateEvent() => NetworkLatencyUpdateEvent = null;
        /// <summary> Clears all subscribers from <see cref="ConnectionRequestEvent"/>. </summary>
        public void ClearConnectionRequestEvent() => ConnectionRequestEvent = null;
        /// <summary> Clears all subscribers from <see cref="DeliveryEvent"/>. </summary>
        public void ClearDeliveryEvent() => DeliveryEvent = null;
        /// <summary> Clears all subscribers from <see cref="PeerAddressChangedEvent"/>. </summary>
        public void ClearPeerAddressChangedEvent() => PeerAddressChangedEvent = null;

        void ILiteNetEventListener.OnPeerConnected(LiteNetPeer peer) =>
            PeerConnectedEvent?.Invoke(peer);

        void ILiteNetEventListener.OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo) =>
            PeerDisconnectedEvent?.Invoke(peer, disconnectInfo);

        void ILiteNetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode) =>
            NetworkErrorEvent?.Invoke(endPoint, socketErrorCode);

        void ILiteNetEventListener.OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) =>
            NetworkReceiveEvent?.Invoke(peer, reader, deliveryMethod);

        void ILiteNetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) =>
            NetworkReceiveUnconnectedEvent?.Invoke(remoteEndPoint, reader, messageType);

        void ILiteNetEventListener.OnNetworkLatencyUpdate(LiteNetPeer peer, int latency) =>
            NetworkLatencyUpdateEvent?.Invoke(peer, latency);

        void ILiteNetEventListener.OnConnectionRequest(LiteConnectionRequest request) =>
            ConnectionRequestEvent?.Invoke(request);

        void ILiteNetEventListener.OnMessageDelivered(LiteNetPeer peer, object userData) =>
            DeliveryEvent?.Invoke(peer, userData);

        void ILiteNetEventListener.OnPeerAddressChanged(LiteNetPeer peer, IPEndPoint previousAddress) =>
            PeerAddressChangedEvent?.Invoke(peer, previousAddress);
    }
}
