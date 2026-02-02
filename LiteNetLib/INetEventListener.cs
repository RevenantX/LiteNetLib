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
        void OnConnectionRequest(ConnectionRequest request);

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
        public delegate void OnPeerConnected(NetPeer peer);
        public delegate void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
        public delegate void OnNetworkError(IPEndPoint endPoint, SocketError socketError);
        public delegate void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod);
        public delegate void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);
        public delegate void OnNetworkLatencyUpdate(NetPeer peer, int latency);
        public delegate void OnConnectionRequest(ConnectionRequest request);
        public delegate void OnDeliveryEvent(NetPeer peer, object userData);
        public delegate void OnNtpResponseEvent(NtpPacket packet);
        public delegate void OnPeerAddressChangedEvent(NetPeer peer, IPEndPoint previousAddress);

        public event OnPeerConnected PeerConnectedEvent;
        public event OnPeerDisconnected PeerDisconnectedEvent;
        public event OnNetworkError NetworkErrorEvent;
        public event OnNetworkReceive NetworkReceiveEvent;
        public event OnNetworkReceiveUnconnected NetworkReceiveUnconnectedEvent;
        public event OnNetworkLatencyUpdate NetworkLatencyUpdateEvent;
        public event OnConnectionRequest ConnectionRequestEvent;
        public event OnDeliveryEvent DeliveryEvent;
        public event OnNtpResponseEvent NtpResponseEvent;
        public event OnPeerAddressChangedEvent PeerAddressChangedEvent;

        public void ClearPeerConnectedEvent() =>  PeerConnectedEvent = null;
        public void ClearPeerDisconnectedEvent() => PeerDisconnectedEvent = null;
        public void ClearNetworkErrorEvent() => NetworkErrorEvent = null;
        public void ClearNetworkReceiveEvent() => NetworkReceiveEvent = null;
        public void ClearNetworkReceiveUnconnectedEvent() => NetworkReceiveUnconnectedEvent = null;
        public void ClearNetworkLatencyUpdateEvent() => NetworkLatencyUpdateEvent = null;
        public void ClearConnectionRequestEvent() => ConnectionRequestEvent = null;
        public void ClearDeliveryEvent() => DeliveryEvent = null;
        public void ClearNtpResponseEvent() => NtpResponseEvent = null;
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
        public delegate void OnPeerConnected(LiteNetPeer peer);
        public delegate void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo);
        public delegate void OnNetworkError(IPEndPoint endPoint, SocketError socketError);
        public delegate void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod);
        public delegate void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType);
        public delegate void OnNetworkLatencyUpdate(LiteNetPeer peer, int latency);
        public delegate void OnConnectionRequest(ConnectionRequest request);
        public delegate void OnDeliveryEvent(LiteNetPeer peer, object userData);
        public delegate void OnPeerAddressChangedEvent(LiteNetPeer peer, IPEndPoint previousAddress);

        public event OnPeerConnected PeerConnectedEvent;
        public event OnPeerDisconnected PeerDisconnectedEvent;
        public event OnNetworkError NetworkErrorEvent;
        public event OnNetworkReceive NetworkReceiveEvent;
        public event OnNetworkReceiveUnconnected NetworkReceiveUnconnectedEvent;
        public event OnNetworkLatencyUpdate NetworkLatencyUpdateEvent;
        public event OnConnectionRequest ConnectionRequestEvent;
        public event OnDeliveryEvent DeliveryEvent;
        public event OnPeerAddressChangedEvent PeerAddressChangedEvent;

        public void ClearPeerConnectedEvent() =>  PeerConnectedEvent = null;
        public void ClearPeerDisconnectedEvent() => PeerDisconnectedEvent = null;
        public void ClearNetworkErrorEvent() => NetworkErrorEvent = null;
        public void ClearNetworkReceiveEvent() => NetworkReceiveEvent = null;
        public void ClearNetworkReceiveUnconnectedEvent() => NetworkReceiveUnconnectedEvent = null;
        public void ClearNetworkLatencyUpdateEvent() => NetworkLatencyUpdateEvent = null;
        public void ClearConnectionRequestEvent() => ConnectionRequestEvent = null;
        public void ClearDeliveryEvent() => DeliveryEvent = null;
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

        void ILiteNetEventListener.OnConnectionRequest(ConnectionRequest request) =>
            ConnectionRequestEvent?.Invoke(request);

        void ILiteNetEventListener.OnMessageDelivered(LiteNetPeer peer, object userData) =>
            DeliveryEvent?.Invoke(peer, userData);

        void ILiteNetEventListener.OnPeerAddressChanged(LiteNetPeer peer, IPEndPoint previousAddress) =>
            PeerAddressChangedEvent?.Invoke(peer, previousAddress);
    }
}
