#if DEBUG
#define STATS_ENABLED
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    /// <summary>
    /// Main class for all network operations. Can be used as client and/or server.
    /// </summary>
    public sealed class NetManager : IEnumerable<NetPeer>
    {
        internal delegate void OnMessageReceived(byte[] data, int length, int errorCode, IPEndPoint remoteEndPoint);

        private enum NetEventType
        {
            Connect,
            Disconnect,
            Receive,
            ReceiveUnconnected,
            Error,
            ConnectionLatencyUpdated,
            DiscoveryRequest,
            DiscoveryResponse,
            ConnectionRequest
        }

        private sealed class NetEvent
        {
            public NetPeer Peer;
            public readonly NetDataReader DataReader = new NetDataReader();
            public NetEventType Type;
            public IPEndPoint RemoteEndPoint;
            public int AdditionalData;
            public DisconnectReason DisconnectReason;
            public ConnectionRequest ConnectionRequest;
            public DeliveryMethod DeliveryMethod;
        }

#if DEBUG
        private struct IncomingData
        {
            public byte[] Data;
            public IPEndPoint EndPoint;
            public DateTime TimeWhenGet;
        }
        private readonly List<IncomingData> _pingSimulationList = new List<IncomingData>(); 
        private readonly Random _randomGenerator = new Random();
        private const int MinLatencyTreshold = 5;
#endif

        private readonly NetSocket _socket;
        private Thread _logicThread;

        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool;
        private readonly INetEventListener _netEventListener;

        private readonly NetPeerCollection _peers;
        private int _connectedPeersCount;
        private readonly List<NetPeer> _connectedPeerListCache;

        internal readonly NetPacketPool NetPacketPool;

        //config section
        /// <summary>
        /// Enable messages receiving without connection. (with SendUnconnectedMessage method)
        /// </summary>
        public bool UnconnectedMessagesEnabled = false;

        /// <summary>
        /// Enable nat punch messages
        /// </summary>
        public bool NatPunchEnabled = false;

        /// <summary>
        /// Library logic update and send period in milliseconds
        /// </summary>
        public int UpdateTime = DefaultUpdateTime;

        /// <summary>
        /// Interval for latency detection and checking connection
        /// </summary>
        public int PingInterval = 1000;

        /// <summary>
        /// If NetManager doesn't receive any packet from remote peer during this time then connection will be closed
        /// (including library internal keepalive packets)
        /// </summary>
        public int DisconnectTimeout = 5000;

        /// <summary>
        /// Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)
        /// </summary>
        public bool SimulatePacketLoss = false;

        /// <summary>
        /// Simulate latency by holding packets for random time. (Works only in DEBUG mode)
        /// </summary>
        public bool SimulateLatency = false;

        /// <summary>
        /// Chance of packet loss when simulation enabled. value in percents (1 - 100).
        /// </summary>
        public int SimulationPacketLossChance = 10;

        /// <summary>
        /// Minimum simulated latency
        /// </summary>
        public int SimulationMinLatency = 30;

        /// <summary>
        /// Maximum simulated latency
        /// </summary>
        public int SimulationMaxLatency = 100;

        /// <summary>
        /// Experimental feature. Events automatically will be called without PollEvents method from another thread
        /// </summary>
        public bool UnsyncedEvents = false;

        /// <summary>
        /// Allows receive DiscoveryRequests
        /// </summary>
        public bool DiscoveryEnabled = false;

        /// <summary>
        /// Merge small packets into one before sending to reduce outgoing packets count. (May increase a bit outgoing data size)
        /// </summary>
        public bool MergeEnabled = false;

        /// <summary>
        /// Delay betwen initial connection attempts
        /// </summary>
        public int ReconnectDelay = 500;

        /// <summary>
        /// Maximum connection attempts before client stops and call disconnect event.
        /// </summary>
        public int MaxConnectAttempts = 10;

        /// <summary>
        /// Enables socket option "ReuseAddress" for specific purposes
        /// </summary>
        public bool ReuseAddress = false;

        private const int DefaultUpdateTime = 15;

        /// <summary>
        /// Statistics of all connections
        /// </summary>
        public readonly NetStatistics Statistics;

        //modules
        /// <summary>
        /// NatPunchModule for NAT hole punching operations
        /// </summary>
        public readonly NatPunchModule NatPunchModule;

        /// <summary>
        /// Returns true if socket listening and update thread is running
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Local EndPoint (host and port)
        /// </summary>
        public int LocalPort { get { return _socket.LocalPort; } }
        
        public List<NetPeer> ConnectedPeerList
        {
            get
            {
                _connectedPeerListCache.Clear();
                for(var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
                {
                    if ((netPeer.ConnectionState & ConnectionState.Connected) != 0)
                        _connectedPeerListCache.Add(netPeer);
                }
                return _connectedPeerListCache;
            }
        }
        
        /// <summary>
        /// Returns connected peers count
        /// </summary>
        public int PeersCount { get { return _connectedPeersCount; } }

        /// <summary>
        /// NetManager constructor
        /// </summary>
        /// <param name="listener">Network events listener</param>
        public NetManager(INetEventListener listener)
        {
            _socket = new NetSocket(ReceiveLogic);
            _netEventListener = listener;
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            NetPacketPool = new NetPacketPool();
            NatPunchModule = new NatPunchModule(_socket);
            Statistics = new NetStatistics();
            _peers = new NetPeerCollection();
            _connectedPeerListCache = new List<NetPeer>();
        }

        internal void ConnectionLatencyUpdated(NetPeer fromPeer, int latency)
        {
            var evt = CreateEvent(NetEventType.ConnectionLatencyUpdated);
            evt.Peer = fromPeer;
            evt.AdditionalData = latency;
            EnqueueEvent(evt);
        }

        internal bool SendRawAndRecycle(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            var result = SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
            NetPacketPool.Recycle(packet);
            return result;
        }

        internal bool SendRaw(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            return SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
        }

        internal bool SendRaw(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
        {
            if (!IsRunning)
                return false;

            int errorCode = 0;
            if (_socket.SendTo(message, start, length, remoteEndPoint, ref errorCode) <= 0)
                return false;

            //10040 message to long... need to check
            //10065 no route to host
            if (errorCode == 10040)
            {
                NetUtils.DebugWrite(ConsoleColor.Red, "[SRD] 10040, datalen: {0}", length);
                return false;
            }
            if (errorCode != 0 && errorCode != 10065)
            {
                //Send error
                NetPeer fromPeer;
                if (_peers.TryGetValue(remoteEndPoint, out fromPeer))
                {
                    DisconnectPeer(fromPeer, DisconnectReason.SocketSendError, errorCode, true, null, 0, 0);
                }
                var netEvent = CreateEvent(NetEventType.Error);
                netEvent.RemoteEndPoint = remoteEndPoint;
                netEvent.AdditionalData = errorCode;
                EnqueueEvent(netEvent);
                return false;
            }
#if STATS_ENABLED
            Statistics.PacketsSent++;
            Statistics.BytesSent += (uint)length;
#endif

            return true;
        }

        internal void DisconnectPeer(
            NetPeer peer, 
            DisconnectReason reason, 
            int socketErrorCode, 
            bool force,
            byte[] data,
            int start,
            int count)
        {
            //if already shutdowned. no need send event
            if (!peer.Shutdown(data, start, count, force))
                return;
            var netEvent = CreateEvent(NetEventType.Disconnect);
            netEvent.Peer = peer;
            netEvent.AdditionalData = socketErrorCode;
            netEvent.DisconnectReason = reason;
            EnqueueEvent(netEvent);
        }

        private NetEvent CreateEvent(NetEventType type)
        {
            NetEvent evt = null;
            lock (_netEventsPool)
            {
                switch (type)
                {
                    case NetEventType.Connect:
                        _connectedPeersCount++;
                        break;
                    case NetEventType.Disconnect:
                        _connectedPeersCount--;
                        break;
                }
                if (_netEventsPool.Count > 0)
                    evt = _netEventsPool.Pop();
            }
            if(evt == null)
                evt = new NetEvent();
            evt.Type = type;
            return evt;
        }

        private void EnqueueEvent(NetEvent evt)
        {
            if (UnsyncedEvents)
            {
                ProcessEvent(evt);
            }
            else
            {
                lock (_netEventsQueue)
                    _netEventsQueue.Enqueue(evt);
            }
        }

        private void ProcessEvent(NetEvent evt)
        {
            NetUtils.DebugWrite("[NM] Processing event: " + evt.Type);
            switch (evt.Type)
            {
                case NetEventType.Connect:
                    _netEventListener.OnPeerConnected(evt.Peer);
                    break;
                case NetEventType.Disconnect:
                    var info = new DisconnectInfo
                    {
                        Reason = evt.DisconnectReason,
                        AdditionalData = evt.DataReader,
                        SocketErrorCode = evt.AdditionalData
                    };
                    _netEventListener.OnPeerDisconnected(evt.Peer, info);
                    break;
                case NetEventType.Receive:
                    _netEventListener.OnNetworkReceive(evt.Peer, evt.DataReader, evt.DeliveryMethod);
                    break;
                case NetEventType.ReceiveUnconnected:
                    _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.BasicMessage);
                    break;
                case NetEventType.DiscoveryRequest:
                    _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.DiscoveryRequest);
                    break;
                case NetEventType.DiscoveryResponse:
                    _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader, UnconnectedMessageType.DiscoveryResponse);
                    break;
                case NetEventType.Error:
                    _netEventListener.OnNetworkError(evt.RemoteEndPoint, evt.AdditionalData);
                    break;
                case NetEventType.ConnectionLatencyUpdated:
                    _netEventListener.OnNetworkLatencyUpdate(evt.Peer, evt.AdditionalData);
                    break;
                case NetEventType.ConnectionRequest:
                    _netEventListener.OnConnectionRequest(evt.ConnectionRequest);
                    break;
            }

            //Recycle
            evt.DataReader.Clear();
            evt.Peer = null;
            evt.AdditionalData = 0;
            evt.RemoteEndPoint = null;
            evt.ConnectionRequest = null;

            lock (_netEventsPool)
                _netEventsPool.Push(evt);
        }

        //Update function
        private void UpdateLogic()
        {
            var peersToRemove = new List<NetPeer>();
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (IsRunning)
            {
#if DEBUG
                if (SimulateLatency)
                {
                    var time = DateTime.UtcNow;
                    lock (_pingSimulationList)
                    {
                        for (int i = 0; i < _pingSimulationList.Count; i++)
                        {
                            var incomingData = _pingSimulationList[i];
                            if (incomingData.TimeWhenGet <= time)
                            {
                                DataReceived(incomingData.Data, incomingData.Data.Length, incomingData.EndPoint);
                                _pingSimulationList.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
#endif

#if STATS_ENABLED
                ulong totalPacketLoss = 0;
#endif
                int elapsed = (int)stopwatch.ElapsedMilliseconds;
                if (elapsed <= 0)
                    elapsed = 1;
                //Process acks
                for(var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
                {
                    if (netPeer.ConnectionState == ConnectionState.Disconnected && netPeer.TimeSinceLastPacket > DisconnectTimeout)
                    {
                        peersToRemove.Add(netPeer);
                    }
                    else
                    {
                        netPeer.Update(elapsed);
#if STATS_ENABLED
                        totalPacketLoss += netPeer.Statistics.PacketLoss;
#endif
                    }
                }
                _peers.RemovePeers(peersToRemove);
                peersToRemove.Clear();
#if STATS_ENABLED
                Statistics.PacketLoss = totalPacketLoss;
#endif
                int sleepTime = UpdateTime - (int)(stopwatch.ElapsedMilliseconds - elapsed);
                stopwatch.Reset();
                stopwatch.Start();
                if (sleepTime > 0)
                    Thread.Sleep(sleepTime);
            }
            stopwatch.Stop();
        }
        
        private void ReceiveLogic(byte[] data, int length, int errorCode, IPEndPoint remoteEndPoint)
        {
            if (errorCode != 0)
            {
                _peers.Clear();
                var netEvent = CreateEvent(NetEventType.Error);
                netEvent.AdditionalData = errorCode;
                EnqueueEvent(netEvent);
                NetUtils.DebugWriteError("[NM] Receive error: {0}" + errorCode);
                return;
            }
#if DEBUG
            if (SimulatePacketLoss && _randomGenerator.NextDouble() * 100 < SimulationPacketLossChance)
            {
                //drop packet
                return;
            }
            if (SimulateLatency)
            {
                int latency = _randomGenerator.Next(SimulationMinLatency, SimulationMaxLatency);
                if (latency > MinLatencyTreshold)
                {
                    byte[] holdedData = new byte[length];
                    Buffer.BlockCopy(data, 0, holdedData, 0, length);

                    lock (_pingSimulationList)
                    {
                        _pingSimulationList.Add(new IncomingData
                        {
                            Data = holdedData,
                            EndPoint = remoteEndPoint,
                            TimeWhenGet = DateTime.UtcNow.AddMilliseconds(latency)
                        });
                    }
                    //hold packet
                    return;
                }
            }
#endif
            try
            {
                //ProcessEvents
                DataReceived(data, length, remoteEndPoint);
            }
            catch(Exception e)
            {
                //protects socket receive thread
                NetUtils.DebugWriteError("[NM] SocketReceiveThread error: " + e );
            }
        }

        private void OnConnectionSolved(ConnectionRequest request)
        {
            if (request.Result == ConnectionRequestResult.Reject)
            {
                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NM] Peer connect reject.");
                //TODO: good reject with info
                _peers.RemovePeer(request.Peer);
            }
            else
            {
                //Accept
                request.Peer.Accept(request.ConnectionId, request.ConnectionNumber);

                //Add event
                var netEvent = CreateEvent(NetEventType.Connect);
                netEvent.Peer = request.Peer;
                EnqueueEvent(netEvent);

                NetUtils.DebugWrite(ConsoleColor.Cyan, "[NM] Received peer connection Id: {0}, EP: {1}",
                    request.Peer.ConnectId, request.Peer.EndPoint);
            }
        }

        private void ProcessConnectRequest(
            IPEndPoint remoteEndPoint, 
            NetPeer netPeer, 
            NetConnectRequestPacket connRequest)
        {
            byte connectionNumber = connRequest.ConnectionNumber;
            NetEvent netEvent;
            NetUtils.DebugWrite("ConnectRequest LastId: {0}, NewId: {1}, EP: {2}", netPeer.ConnectId, connRequest.ConnectionId, remoteEndPoint);

            //if we have peer
            if (netPeer != null)
            {
                var processResult = netPeer.ProcessConnectRequest(connRequest);
                switch (processResult)
                {
                    case ConnectRequestResult.Reconnection:
                        netEvent = CreateEvent(NetEventType.Disconnect);
                        netEvent.Peer = netPeer;
                        netEvent.DisconnectReason = DisconnectReason.RemoteConnectionClose;
                        EnqueueEvent(netEvent);
                        _peers.RemovePeer(netPeer);
                        //go to new connection
                        break;
                    case ConnectRequestResult.NewConnection:
                        _peers.RemovePeer(netPeer);
                        //go to new connection
                        break;
                    case ConnectRequestResult.P2PConnection:
                        netEvent = CreateEvent(NetEventType.ConnectionRequest);
                        netEvent.ConnectionRequest = new ConnectionRequest(
                            netPeer.ConnectId,
                            connectionNumber,
                            ConnectionRequestType.PeerToPeer,
                            connRequest.Data,
                            netPeer,
                            OnConnectionSolved);
                        EnqueueEvent(netEvent);
                        return;
                    default:
                        //no operations needed
                        return;
                }
                //ConnectRequestResult.NewConnection
                //Set next connection number
                connectionNumber = (byte)((netPeer.ConnectionNum + 1) % NetConstants.MaxConnectionNumber);
                //To reconnect peer
            }
            //Add new peer and craete ConnectRequest event
            NetUtils.DebugWrite("[NM] Creating request event: " + connRequest.ConnectionId);
            netPeer = new NetPeer(this, remoteEndPoint);
            if (_peers.TryAdd(netPeer))
            {
                netEvent = CreateEvent(NetEventType.ConnectionRequest);
                netEvent.ConnectionRequest = new ConnectionRequest(
                    connRequest.ConnectionId,
                    connectionNumber,
                    ConnectionRequestType.Incoming,
                    connRequest.Data,
                    netPeer,
                    OnConnectionSolved);
                EnqueueEvent(netEvent);
            }
        }

        private void DataReceived(byte[] reusableBuffer, int count, IPEndPoint remoteEndPoint)
        {
#if STATS_ENABLED
            Statistics.PacketsReceived++;
            Statistics.BytesReceived += (uint) count;
#endif
            //Try read packet
            NetPacket packet = NetPacketPool.GetPacket(count, false);
            if (!packet.FromBytes(reusableBuffer, 0, count))
            {
                NetPacketPool.Recycle(packet);
                NetUtils.DebugWriteError("[NM] DataReceived: bad!");
                return;
            }

            //get peer
            //Check normal packets
            NetPeer netPeer;
            //old packets protection
            NetEvent netEvent;
            bool peerFound = _peers.TryGetValue(remoteEndPoint, out netPeer);

            //Check unconnected
            switch (packet.Property)
            {
                case PacketProperty.DiscoveryRequest:
                    if (!DiscoveryEnabled)
                        break;
                    netEvent = CreateEvent(NetEventType.DiscoveryRequest);
                    netEvent.RemoteEndPoint = remoteEndPoint;
                    netEvent.DataReader.SetSource(packet.RawData, NetConstants.HeaderSize, count);
                    EnqueueEvent(netEvent);
                    break;

                case PacketProperty.DiscoveryResponse:
                    netEvent = CreateEvent(NetEventType.DiscoveryResponse);
                    netEvent.RemoteEndPoint = remoteEndPoint;
                    netEvent.DataReader.SetSource(packet.RawData, NetConstants.HeaderSize, count);
                    EnqueueEvent(netEvent);
                    break;

                case PacketProperty.UnconnectedMessage:
                    if (!UnconnectedMessagesEnabled)
                        break;

                    netEvent = CreateEvent(NetEventType.ReceiveUnconnected);
                    netEvent.RemoteEndPoint = remoteEndPoint;
                    netEvent.DataReader.SetSource(packet.RawData, NetConstants.HeaderSize, count);
                    EnqueueEvent(netEvent);
                    break;

                case PacketProperty.NatIntroduction:
                case PacketProperty.NatIntroductionRequest:
                case PacketProperty.NatPunchMessage:
                    if (NatPunchEnabled)
                        NatPunchModule.ProcessMessage(remoteEndPoint, packet);
                    break;

                case PacketProperty.Disconnect:
                    if (peerFound && netPeer.ConnectionState == ConnectionState.InProgress ||
                        netPeer.ConnectionState == ConnectionState.Connected)
                    {
                        if (BitConverter.ToInt64(packet.RawData, 1) != netPeer.ConnectId)
                        {
                            //Old or incorrect disconnect
                            NetPacketPool.Recycle(packet);
                            break;
                        }
                        netEvent = CreateEvent(NetEventType.Disconnect);
                        netEvent.Peer = netPeer;
                        netEvent.DataReader.SetSource(packet.RawData, 9, packet.Size);
                        netEvent.DisconnectReason = DisconnectReason.RemoteConnectionClose;
                        EnqueueEvent(netEvent);
                        netPeer.ProcessPacket(packet);
                    }
                    //Send shutdown
                    SendRaw(new[] { (byte)PacketProperty.ShutdownOk }, 0, 1, remoteEndPoint);
                    break;

                case PacketProperty.ConnectAccept:
                    var connAccept = NetConnectAcceptPacket.FromData(packet);
                    if (connAccept != null && peerFound && netPeer.ProcessConnectAccept(connAccept))
                    {
                        var connectEvent = CreateEvent(NetEventType.Connect);
                        connectEvent.Peer = netPeer;
                        EnqueueEvent(connectEvent);
                    }
                    break;
                case PacketProperty.ConnectRequest:
                    var connRequest = NetConnectRequestPacket.FromData(packet);
                    if (connRequest != null)
                        ProcessConnectRequest(remoteEndPoint, netPeer, connRequest);
                    break;
                default:
                    if(peerFound)
                        netPeer.ProcessPacket(packet);
                    break;
            }
        }

        internal void ReceiveFromPeer(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            NetPeer fromPeer;
            if (!_peers.TryGetValue(remoteEndPoint, out fromPeer))
                return;

            NetUtils.DebugWrite(ConsoleColor.Cyan, "[NM] Received message");
            var netEvent = CreateEvent(NetEventType.Receive);
            netEvent.Peer = fromPeer;
            netEvent.RemoteEndPoint = fromPeer.EndPoint;
            switch (packet.Property)
            {
                case PacketProperty.Unreliable:
                    netEvent.DeliveryMethod = DeliveryMethod.Unreliable;
                    break;
                case PacketProperty.ReliableUnordered:
                    netEvent.DeliveryMethod = DeliveryMethod.ReliableUnordered;
                    break;
                case PacketProperty.ReliableOrdered:
                    netEvent.DeliveryMethod = DeliveryMethod.ReliableOrdered;
                    break;
                case PacketProperty.Sequenced:
                    netEvent.DeliveryMethod = DeliveryMethod.Sequenced;
                    break;
                case PacketProperty.ReliableSequenced:
                    netEvent.DeliveryMethod = DeliveryMethod.ReliableSequenced;
                    break;
            }
            netEvent.DataReader.SetSource(packet.CopyPacketData());
            EnqueueEvent(netEvent);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(NetDataWriter writer, DeliveryMethod options)
        {
            SendToAll(writer.Data, 0, writer.Length, options);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, DeliveryMethod options)
        {
            SendToAll(data, 0, data.Length, options);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, int start, int length, DeliveryMethod options)
        {
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
                netPeer.Send(data, start, length, options);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(NetDataWriter writer, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(writer.Data, 0, writer.Length, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(data, 0, data.Length, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, int start, int length, DeliveryMethod options, NetPeer excludePeer)
        {
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if (netPeer != excludePeer)
                    netPeer.Send(data, start, length, options);
            }
        }

        /// <summary>
        /// Start logic thread and listening on available port
        /// </summary>
        public bool Start()
        {
            return Start(0);
        }

        /// <summary>
        /// Start logic thread and listening on selected port
        /// </summary>
        /// <param name="addressIPv4">bind to specific ipv4 address</param>
        /// <param name="addressIPv6">bind to specific ipv6 address</param>
        /// <param name="port">port to listen</param>
        public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port)
        {
            if (IsRunning)
                return false;
            if (!_socket.Bind(addressIPv4, addressIPv6, port, ReuseAddress))
                return false;
            IsRunning = true;
            _logicThread = new Thread(UpdateLogic) { Name = "LogicThread", IsBackground = true };
            _logicThread.Start();
            return true;
        }

        /// <summary>
        /// Start logic thread and listening on selected port
        /// </summary>
        /// <param name="addressIPv4">bind to specific ipv4 address</param>
        /// <param name="addressIPv6">bind to specific ipv6 address</param>
        /// <param name="port">port to listen</param>
        public bool Start(string addressIPv4, string addressIPv6, int port)
        {
            IPAddress ipv4 = NetUtils.ResolveAddress(addressIPv4);
            IPAddress ipv6 = NetUtils.ResolveAddress(addressIPv6);
            return Start(ipv4, ipv6, port);
        }

        /// <summary>
        /// Start logic thread and listening on selected port
        /// </summary>
        /// <param name="port">port to listen</param>
        public bool Start(int port)
        {
            return Start(IPAddress.Any, IPAddress.IPv6Any, port);
        }

        /// <summary>
        /// Send message without connection
        /// </summary>
        /// <param name="message">Raw data</param>
        /// <param name="remoteEndPoint">Packet destination</param>
        /// <returns>Operation result</returns>
        public bool SendUnconnectedMessage(byte[] message, IPEndPoint remoteEndPoint)
        {
            return SendUnconnectedMessage(message, 0, message.Length, remoteEndPoint);
        }

        /// <summary>
        /// Send message without connection
        /// </summary>
        /// <param name="writer">Data serializer</param>
        /// <param name="remoteEndPoint">Packet destination</param>
        /// <returns>Operation result</returns>
        public bool SendUnconnectedMessage(NetDataWriter writer, IPEndPoint remoteEndPoint)
        {
            return SendUnconnectedMessage(writer.Data, 0, writer.Length, remoteEndPoint);
        }

        /// <summary>
        /// Send message without connection
        /// </summary>
        /// <param name="message">Raw data</param>
        /// <param name="start">data start</param>
        /// <param name="length">data length</param>
        /// <param name="remoteEndPoint">Packet destination</param>
        /// <returns>Operation result</returns>
        public bool SendUnconnectedMessage(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
        {
            if (!IsRunning)
                return false;
            var packet = NetPacketPool.GetWithData(PacketProperty.UnconnectedMessage, message, start, length);
            bool result = SendRawAndRecycle(packet, remoteEndPoint);
            return result;
        }

        public bool SendDiscoveryRequest(NetDataWriter writer, int port)
        {
            return SendDiscoveryRequest(writer.Data, 0, writer.Length, port);
        }

        public bool SendDiscoveryRequest(byte[] data, int port)
        {
            return SendDiscoveryRequest(data, 0, data.Length, port);
        }

        public bool SendDiscoveryRequest(byte[] data, int start, int length, int port)
        {
            if (!IsRunning)
                return false;
            var packet = NetPacketPool.GetWithData(PacketProperty.DiscoveryRequest, data, start, length);
            bool result = _socket.SendBroadcast(packet.RawData, 0, packet.Size, port);
            NetPacketPool.Recycle(packet);
            return result;
        }

        public bool SendDiscoveryResponse(NetDataWriter writer, IPEndPoint remoteEndPoint)
        {
            return SendDiscoveryResponse(writer.Data, 0, writer.Length, remoteEndPoint);
        }

        public bool SendDiscoveryResponse(byte[] data, IPEndPoint remoteEndPoint)
        {
            return SendDiscoveryResponse(data, 0, data.Length, remoteEndPoint);
        }

        public bool SendDiscoveryResponse(byte[] data, int start, int length, IPEndPoint remoteEndPoint)
        {
            if (!IsRunning)
                return false;
            var packet = NetPacketPool.GetWithData(PacketProperty.DiscoveryResponse, data, start, length);
            bool result = SendRawAndRecycle(packet, remoteEndPoint);
            return result;
        }

        /// <summary>
        /// Flush all queued packets of all peers
        /// </summary>
        public void Flush()
        {
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
                netPeer.Flush();
        }

        /// <summary>
        /// Receive all pending events. Call this in game update code
        /// </summary>
        public void PollEvents()
        {
            if (UnsyncedEvents)
                return;
            while (true)
            {
                NetEvent evt;
                lock (_netEventsQueue)
                {
                    if (_netEventsQueue.Count > 0)
                        evt = _netEventsQueue.Dequeue();
                    else
                        return;
                }
                ProcessEvent(evt);
            }
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        /// <param name="key">Connection key</param>
        /// <returns>Null if connections limit reached, New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(string address, int port, string key)
        {
            var ep = NetUtils.MakeEndPoint(address, port);
            return Connect(ep, key);
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>Null if connections limit reached, New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(string address, int port, NetDataWriter connectionData)
        {
            var ep = NetUtils.MakeEndPoint(address, port);
            return Connect(ep, connectionData);
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="key">Connection key</param>
        /// <returns>Null if connections limit reached, New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(IPEndPoint target, string key)
        {
            return Connect(target, NetDataWriter.FromString(key));
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>Null if connections limit reached, New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(IPEndPoint target, NetDataWriter connectionData)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Client is not running");

            NetPeer peer;
            if (_peers.TryGetValue(target, out peer))
            {
                switch (peer.ConnectionState)
                {
                    //just return already connected peer
                    case ConnectionState.Connected:
                    case ConnectionState.InProgress:
                    case ConnectionState.Incoming:
                        return peer;
                }              
            }

            //clean before add new
            if(peer != null)
                _peers.RemovePeer(peer);

            //TODO: peer connection num

            //Create reliable connection
            //And send connection request
            peer = new NetPeer(this, target, connectionData);
            _peers.TryAdd(peer);
            return peer;
        }

        /// <summary>
        /// Force closes connection and stop all threads.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;
            NetUtils.DebugWrite("[NM] Stop");

            //Send last disconnect
            for(var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
                netPeer.Shutdown(null, 0, 0, false);

            //For working send
            IsRunning = false;

            //Stop
            if (Thread.CurrentThread != _logicThread)
                _logicThread.Join();
            _logicThread = null;
            _socket.Close();
            _peers.Clear();
            _connectedPeersCount = 0;
            lock(_netEventsQueue)
                _netEventsQueue.Clear();
        }

        /// <summary>
        /// Get first peer. Usefull for Client mode
        /// </summary>
        /// <returns></returns>
        public NetPeer GetFirstPeer()
        {
            return _peers.HeadPeer;
        }

        public int GetPeersCount(ConnectionState peerState)
        {
            int count = 0;
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if ((netPeer.ConnectionState & peerState) != 0)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get copy of current connected peers (slow! use GetPeersNonAlloc for best performance)
        /// </summary>
        /// <returns>Array with connected peers</returns>
        [Obsolete("Use GetPeers(ConnectionState peerState)")]
        public NetPeer[] GetPeers()
        {
            return GetPeers(ConnectionState.Connected | ConnectionState.InProgress);
        } 

        /// <summary>
        /// Get copy of current connected peers (slow! use GetPeersNonAlloc for best performance)
        /// </summary>
        /// <returns>Array with connected peers</returns>
        public NetPeer[] GetPeers(ConnectionState peerState)
        {
            List<NetPeer> peersList = new List<NetPeer>();
            GetPeersNonAlloc(peersList, peerState);
            return peersList.ToArray();
        }

        /// <summary>
        /// Get copy of peers (without allocations)
        /// </summary>
        /// <param name="peers">List that will contain result</param>
        /// <param name="peerState">State of peers</param>
        public void GetPeersNonAlloc(List<NetPeer> peers, ConnectionState peerState)
        {
            peers.Clear();
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if ((netPeer.ConnectionState & peerState) != 0)
                    peers.Add(netPeer);
            }
        }

        public void DisconnectAll()
        {
            DisconnectAll(null, 0, 0);
        }

        public void DisconnectAll(byte[] data, int start, int count)
        {
            //Send disconnect packets
            for (var netPeer = _peers.HeadPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                DisconnectPeer(
                    netPeer, 
                    DisconnectReason.DisconnectPeerCalled, 
                    0, 
                    false,
                    data, 
                    start, 
                    count);
            }
        }

        /// <summary>
        /// Immediately disconnect peer from server without additional data
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        public void DisconnectPeerForce(NetPeer peer)
        {
            DisconnectPeer(peer, DisconnectReason.DisconnectPeerCalled, 0, true, null, 0, 0);
        }

        /// <summary>
        /// Disconnect peer from server
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        public void DisconnectPeer(NetPeer peer)
        {
            DisconnectPeer(peer, null, 0, 0);
        }

        /// <summary>
        /// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        /// <param name="data">additional data</param>
        public void DisconnectPeer(NetPeer peer, byte[] data)
        {
            DisconnectPeer(peer, data, 0, data.Length);
        }

        /// <summary>
        /// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        /// <param name="writer">additional data</param>
        public void DisconnectPeer(NetPeer peer, NetDataWriter writer)
        {
            DisconnectPeer(peer, writer.Data, 0, writer.Length);
        }

        /// <summary>
        /// Disconnect peer from server and send additional data (Size must be less or equal MTU - 8)
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        /// <param name="data">additional data</param>
        /// <param name="start">data start</param>
        /// <param name="count">data length</param>
        public void DisconnectPeer(NetPeer peer, byte[] data, int start, int count)
        {
            DisconnectPeer(
                peer, 
                DisconnectReason.DisconnectPeerCalled, 
                0, 
                false,
                data, 
                start, 
                count);
        }

        public IEnumerator<NetPeer> GetEnumerator()
        {
            var peer = _peers.HeadPeer;
            while (peer != null)
            {
                yield return peer;
                peer = peer.NextPeer;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
