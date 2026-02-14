using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using LiteNetLib.Layers;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    /// <summary>
    /// More feature rich network manager with adjustable channels count
    /// </summary>
    public class NetManager : LiteNetManager, IEnumerable<NetPeer>
    {
        private readonly INetEventListener _netEventListener;
        private byte _channelsCount = 1;
        private readonly ConcurrentDictionary<IPEndPoint, NtpRequest> _ntpRequests = new ConcurrentDictionary<IPEndPoint, NtpRequest>();

        /// <summary>
        /// QoS channel count per message type (value must be between 1 and 64 channels)
        /// </summary>
        public byte ChannelsCount
        {
            get => _channelsCount;
            set
            {
                if (value < 1 || value > 64)
                    throw new ArgumentException("Channels count must be between 1 and 64");
                _channelsCount = value;
            }
        }

        /// <summary>
        /// First peer. Useful for Client mode
        /// </summary>
        public new NetPeer FirstPeer => (NetPeer)_headPeer;

        /// <summary>
        /// Get copy of peers (without allocations)
        /// </summary>
        /// <param name="peers">List that will contain result</param>
        /// <param name="peerState">State of peers</param>
        public void GetPeers(List<NetPeer> peers, ConnectionState peerState)
        {
            peers.Clear();
            _peersLock.EnterReadLock();
            for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if ((netPeer.ConnectionState & peerState) != 0)
                    peers.Add((NetPeer)netPeer);
            }
            _peersLock.ExitReadLock();
        }

        /// <summary>
        /// Get copy of connected peers (without allocations)
        /// </summary>
        /// <param name="peers">List that will contain result</param>
        public void GetConnectedPeers(List<NetPeer> peers) =>
            GetPeers(peers, ConnectionState.Connected);

        public NetManager(INetEventListener listener, PacketLayerBase extraPacketLayer = null) : base(null, extraPacketLayer) =>
            _netEventListener = listener;

        /// <summary>
        /// Create the requests for NTP server
        /// </summary>
        /// <param name="endPoint">NTP Server address.</param>
        public void CreateNtpRequest(IPEndPoint endPoint) =>
            _ntpRequests.TryAdd(endPoint, new NtpRequest(endPoint));

        /// <summary>
        /// Create the requests for NTP server
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        /// <param name="port">port</param>
        public void CreateNtpRequest(string ntpServerAddress, int port)
        {
            var endPoint = NetUtils.MakeEndPoint(ntpServerAddress, port);
            _ntpRequests.TryAdd(endPoint, new NtpRequest(endPoint));
        }

        /// <summary>
        /// Create the requests for NTP server (default port)
        /// </summary>
        /// <param name="ntpServerAddress">NTP Server address.</param>
        public void CreateNtpRequest(string ntpServerAddress)
        {
            var endPoint = NetUtils.MakeEndPoint(ntpServerAddress, NtpRequest.DefaultPort);
            _ntpRequests.TryAdd(endPoint, new NtpRequest(endPoint));
        }

        internal override bool CustomMessageHandle(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            if (_ntpRequests.Count > 0 && _ntpRequests.TryGetValue(remoteEndPoint, out _))
            {
                if (packet.Size < 48)
                {
                    NetDebug.Write(NetLogLevel.Trace, $"NTP response too short: {packet.Size}");
                    return true;
                }

                byte[] copiedData = new byte[packet.Size];
                Buffer.BlockCopy(packet.RawData, 0, copiedData, 0, packet.Size);
                NtpPacket ntpPacket = NtpPacket.FromServerResponse(copiedData, DateTime.UtcNow);
                try
                {
                    ntpPacket.ValidateReply();
                }
                catch (InvalidOperationException ex)
                {
                    NetDebug.Write(NetLogLevel.Trace, $"NTP response error: {ex.Message}");
                    ntpPacket = null;
                }

                if (ntpPacket != null)
                {
                    _ntpRequests.TryRemove(remoteEndPoint, out _);
                    _netEventListener.OnNtpResponse(ntpPacket);
                }
                return true;
            }

            return false;
        }

        //connect to
        protected override LiteNetPeer CreateOutgoingPeer(IPEndPoint remoteEndPoint, int id, byte connectNum, ReadOnlySpan<byte> connectData) =>
            new NetPeer(this, remoteEndPoint, id, connectNum, connectData);

        //accept
        protected override LiteNetPeer CreateIncomingPeer(ConnectionRequest request, int id) =>
            new NetPeer(this, request, id);

        //reject
        protected override LiteNetPeer CreateRejectPeer(IPEndPoint remoteEndPoint, int id) =>
            new NetPeer(this, remoteEndPoint, id);

        protected override void ProcessEvent(NetEvent evt)
        {
            NetDebug.Write("[NM] Processing event: " + evt.Type);
            bool emptyData = evt.DataReader.IsNull;
            var netPeer = evt.Peer as NetPeer;
            switch (evt.Type)
            {
                case NetEvent.EType.Connect:
                    _netEventListener.OnPeerConnected(netPeer);
                    break;
                case NetEvent.EType.Disconnect:
                    var info = new DisconnectInfo
                    {
                        Reason = evt.DisconnectReason,
                        AdditionalData = evt.DataReader,
                        SocketErrorCode = evt.ErrorCode
                    };
                    _netEventListener.OnPeerDisconnected(netPeer, info);
                    break;
                case NetEvent.EType.Receive:
                    _netEventListener.OnNetworkReceive(netPeer, evt.DataReader, evt.ChannelNumber, evt.DeliveryMethod);
                    break;
                case NetEvent.EType.ReceiveUnconnected:
                    _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.BasicMessage);
                    break;
                case NetEvent.EType.Broadcast:
                    _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.Broadcast);
                    break;
                case NetEvent.EType.Error:
                    _netEventListener.OnNetworkError(evt.RemoteEndPoint, evt.ErrorCode);
                    break;
                case NetEvent.EType.ConnectionLatencyUpdated:
                    _netEventListener.OnNetworkLatencyUpdate(netPeer, evt.Latency);
                    break;
                case NetEvent.EType.ConnectionRequest:
                    _netEventListener.OnConnectionRequest(evt.ConnectionRequest);
                    break;
                case NetEvent.EType.MessageDelivered:
                    _netEventListener.OnMessageDelivered(netPeer, evt.UserData);
                    break;
                case NetEvent.EType.PeerAddressChanged:
                    _peersLock.EnterUpgradeableReadLock();
                    IPEndPoint previousAddress = null;
                    if (ContainsPeer(evt.Peer))
                    {
                        _peersLock.EnterWriteLock();
                        RemovePeerFromSet(evt.Peer);
                        previousAddress = new IPEndPoint(evt.Peer.Address, evt.Peer.Port);
                        evt.Peer.FinishEndPointChange(evt.RemoteEndPoint);
                        AddPeerToSet(evt.Peer);
                        _peersLock.ExitWriteLock();
                    }
                    _peersLock.ExitUpgradeableReadLock();
                    if (previousAddress != null)
                        _netEventListener.OnPeerAddressChanged(netPeer, previousAddress);
                    break;
            }
            //Recycle if not message
            if (emptyData)
                RecycleEvent(evt);
            else if (AutoRecycle)
                evt.DataReader.RecycleInternal();
        }

        protected override void ProcessNtpRequests(float elapsedMilliseconds)
        {
            if (_ntpRequests.IsEmpty)
                return;

            List<IPEndPoint> requestsToRemove = null;
            foreach (var ntpRequest in _ntpRequests)
            {
                ntpRequest.Value.Send(_udpSocketv4, elapsedMilliseconds);
                if (ntpRequest.Value.NeedToKill)
                {
                    if (requestsToRemove == null)
                        requestsToRemove = new List<IPEndPoint>();
                    requestsToRemove.Add(ntpRequest.Key);
                }
            }

            if (requestsToRemove != null)
            {
                foreach (var ipEndPoint in requestsToRemove)
                    _ntpRequests.TryRemove(ipEndPoint, out _);
            }
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options) =>
            SendToAll(writer.Data, 0, writer.Length, channelNumber, options);

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options) =>
            SendToAll(data, 0, data.Length, channelNumber, options);

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer) =>
            SendToAll(writer.Data, 0, writer.Length, channelNumber, options, excludePeer);

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer) =>
            SendToAll(data, 0, data.Length, channelNumber, options, excludePeer);

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options, LiteNetPeer excludePeer)
        {
            try
            {
                _peersLock.EnterReadLock();
                for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
                {
                    if (netPeer != excludePeer)
                        ((NetPeer)netPeer).Send(data, start, length, channelNumber, options);
                }
            }
            finally
            {
                _peersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options)
        {
            try
            {
                _peersLock.EnterReadLock();
                for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
                    ((NetPeer)netPeer).Send(data, start, length, channelNumber, options);
            }
            finally
            {
                _peersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        /// <param name="key">Connection key</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="LiteNetManager.Start()"/></exception>
        public new NetPeer Connect(string address, int port, string key) =>
            Connect(address, port, NetDataWriter.FromString(key));

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="LiteNetManager.Start()"/></exception>
        public new NetPeer Connect(string address, int port, NetDataWriter connectionData) =>
            (NetPeer)base.Connect(address, port, connectionData);

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="key">Connection key</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="LiteNetManager.Start()"/></exception>
        public new NetPeer Connect(IPEndPoint target, string key) =>
            (NetPeer)base.Connect(target, key);

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="LiteNetManager.Start()"/></exception>
        public new NetPeer Connect(IPEndPoint target, NetDataWriter connectionData) =>
            (NetPeer)base.Connect(target, connectionData);

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="LiteNetManager.Start()"/></exception>
        public new NetPeer Connect(IPEndPoint target, ReadOnlySpan<byte> connectionData) =>
            (NetPeer)base.Connect(target, connectionData);

        public new NetPeerEnumerator<NetPeer> GetEnumerator() =>
            new NetPeerEnumerator<NetPeer>((NetPeer)_headPeer);

        IEnumerator<NetPeer> IEnumerable<NetPeer>.GetEnumerator() =>
            new NetPeerEnumerator<NetPeer>((NetPeer)_headPeer);
    }
}
