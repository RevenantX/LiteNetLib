using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib.Layers;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public sealed class NetPacketReader : NetDataReader
    {
        private NetPacket _packet;
        private readonly NetManager _manager;
        private readonly NetEvent _evt;

        internal NetPacketReader(NetManager manager, NetEvent evt)
        {
            _manager = manager;
            _evt = evt;
        }

        internal void SetSource(NetPacket packet)
        {
            if (packet == null)
                return;
            _packet = packet;
            SetSource(packet.RawData, packet.GetHeaderSize(), packet.Size);
        }

        internal void RecycleInternal()
        {
            Clear();
            if (_packet != null)
                _manager.NetPacketPool.Recycle(_packet);
            _packet = null;
            _manager.RecycleEvent(_evt);
        }

        public void Recycle()
        {
            if(_manager.AutoRecycle)
                throw new Exception("Recycle called with AutoRecycle enabled");
            RecycleInternal();
        }
    }

    internal sealed class NetEvent
    {
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
            MessageDelivered
        }
        public EType Type;

        public NetPeer Peer;
        public IPEndPoint RemoteEndPoint;
        public object UserData;
        public int Latency;
        public SocketError ErrorCode;
        public DisconnectReason DisconnectReason;
        public ConnectionRequest ConnectionRequest;
        public DeliveryMethod DeliveryMethod;
        public readonly NetPacketReader DataReader;

        public NetEvent(NetManager manager)
        {
            DataReader = new NetPacketReader(manager, this);
        }
    }

    /// <summary>
    /// Main class for all network operations. Can be used as client and/or server.
    /// </summary>
    public class NetManager : INetSocketListener, IEnumerable<NetPeer>
    {
        private class IPEndPointComparer : IEqualityComparer<IPEndPoint>
        {
            public bool Equals(IPEndPoint x, IPEndPoint y)
            {
                return x.Address.Equals(y.Address) && x.Port == y.Port;
            }

            public int GetHashCode(IPEndPoint obj)
            {
                return obj.GetHashCode();
            }
        }

        public struct NetPeerEnumerator : IEnumerator<NetPeer>
        {
            private readonly NetPeer _initialPeer;
            private NetPeer _p;

            public NetPeerEnumerator(NetPeer p)
            {
                _initialPeer = p;
                _p = null;
            }

            public void Dispose()
            {

            }

            public bool MoveNext()
            {
                _p = _p == null ? _initialPeer : _p.NextPeer;
                return _p != null;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public NetPeer Current
            {
                get { return _p; }
            }

            object IEnumerator.Current
            {
                get { return _p; }
            }
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
        private const int MinLatencyThreshold = 5;
#endif

        private readonly NetSocket _socket;
        private Thread _logicThread;

        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool;
        private readonly INetEventListener _netEventListener;
        private readonly IDeliveryEventListener _deliveryEventListener;

        private readonly Dictionary<IPEndPoint, NetPeer> _peersDict;
        private readonly Dictionary<IPEndPoint, ConnectionRequest> _requestsDict;
        private readonly ReaderWriterLockSlim _peersLock;
        private volatile NetPeer _headPeer;
        private volatile int _connectedPeersCount;
        private readonly List<NetPeer> _connectedPeerListCache;
        private NetPeer[] _peersArray;
        internal readonly PacketLayerBase _extraPacketLayer;
        private int _lastPeerId;
        private readonly Queue<int> _peerIds;
        private byte _channelsCount = 1;

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
        public int UpdateTime = 15;

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
        /// If true - delivery event will be called from "receive" thread otherwise on PollEvents call
        /// </summary>
        public bool UnsyncedDeliveryEvent = false;

        /// <summary>
        /// Allows receive broadcast packets
        /// </summary>
        public bool BroadcastReceiveEnabled = false;

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

        /// <summary>
        /// Statistics of all connections
        /// </summary>
        public readonly NetStatistics Statistics;

        /// <summary>
        /// Toggles the collection of network statistics for the instance and all known peers
        /// </summary>
        public bool EnableStatistics = false;

        //modules
        /// <summary>
        /// NatPunchModule for NAT hole punching operations
        /// </summary>
        public readonly NatPunchModule NatPunchModule;

        /// <summary>
        /// Returns true if socket listening and update thread is running
        /// </summary>
        public bool IsRunning { get { return _socket.IsRunning; } }

        /// <summary>
        /// Local EndPoint (host and port)
        /// </summary>
        public int LocalPort { get { return _socket.LocalPort; } }

        /// <summary>
        /// Automatically recycle NetPacketReader after OnReceive event
        /// </summary>
        public bool AutoRecycle;

        /// <summary>
        /// IPv6 support
        /// </summary>
        public bool IPv6Enabled = true;

        /// <summary>
        /// First peer. Useful for Client mode
        /// </summary>
        public NetPeer FirstPeer
        {
            get { return _headPeer; }
        }

        /// <summary>
        /// QoS channel count per message type (value must be between 1 and 64 channels)
        /// </summary>
        public byte ChannelsCount
        {
            get { return _channelsCount; }
            set
            {
                if (value < 1 || value > 64)
                    throw new ArgumentException("Channels count must be between 1 and 64");
                _channelsCount = value;
            }
        }

        /// <summary>
        /// Returns connected peers list (with internal cached list)
        /// </summary>
        public List<NetPeer> ConnectedPeerList
        {
            get
            {
                GetPeersNonAlloc(_connectedPeerListCache, ConnectionState.Connected);
                return _connectedPeerListCache;
            }
        }

        /// <summary>
        /// Gets peer by peer id
        /// </summary>
        /// <param name="id">id of peer</param>
        /// <returns>Peer if peer with id exist, otherwise null</returns>
        public NetPeer GetPeerById(int id)
        {
            return _peersArray[id];
        }

        /// <summary>
        /// Returns connected peers count
        /// </summary>
        public int ConnectedPeersCount { get { return _connectedPeersCount; } }

        private bool TryGetPeer(IPEndPoint endPoint, out NetPeer peer)
        {
            _peersLock.EnterReadLock();
            bool result = _peersDict.TryGetValue(endPoint, out peer);
            _peersLock.ExitReadLock();
            return result;
        }

        private void AddPeer(NetPeer peer)
        {
            _peersLock.EnterWriteLock();
            if (_headPeer != null)
            {
                peer.NextPeer = _headPeer;
                _headPeer.PrevPeer = peer;
            }
            _headPeer = peer;
            _peersDict.Add(peer.EndPoint, peer);
            if (peer.Id >= _peersArray.Length)
            {
                int newSize = _peersArray.Length * 2;
                while (peer.Id >= newSize)
                    newSize *= 2;
                Array.Resize(ref _peersArray, newSize);
            }
            _peersArray[peer.Id] = peer;
            _peersLock.ExitWriteLock();
        }

        private void RemovePeer(NetPeer peer)
        {
            _peersLock.EnterWriteLock();
            RemovePeerInternal(peer);
            _peersLock.ExitWriteLock();
        }

        private void RemovePeerInternal(NetPeer peer)
        {
            if (!_peersDict.Remove(peer.EndPoint))
                return;
            if (peer == _headPeer)
                _headPeer = peer.NextPeer;

            if (peer.PrevPeer != null)
                peer.PrevPeer.NextPeer = peer.NextPeer;
            if (peer.NextPeer != null)
                peer.NextPeer.PrevPeer = peer.PrevPeer;
            peer.PrevPeer = null;

            _peersArray[peer.Id] = null;
            lock (_peerIds)
                _peerIds.Enqueue(peer.Id);
        }

        /// <summary>
        /// NetManager constructor
        /// </summary>
        /// <param name="listener">Network events listener (also can implement IDeliveryEventListener)</param>
        /// <param name="extraPacketLayer">Extra processing of packages, like CRC checksum or encryption. All connected NetManagers must have same layer.</param>
        public NetManager(INetEventListener listener, PacketLayerBase extraPacketLayer = null)
        {
            _socket = new NetSocket(this);
            _netEventListener = listener;
            _deliveryEventListener = listener as IDeliveryEventListener;
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            NetPacketPool = new NetPacketPool();
            NatPunchModule = new NatPunchModule(_socket);
            Statistics = new NetStatistics();
            _connectedPeerListCache = new List<NetPeer>();
            _peersDict = new Dictionary<IPEndPoint, NetPeer>(new IPEndPointComparer());
            _requestsDict = new Dictionary<IPEndPoint, ConnectionRequest>(new IPEndPointComparer());
            _peersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _peerIds = new Queue<int>();
            _peersArray = new NetPeer[32];
            _extraPacketLayer = extraPacketLayer;
        }

        internal void ConnectionLatencyUpdated(NetPeer fromPeer, int latency)
        {
            CreateEvent(NetEvent.EType.ConnectionLatencyUpdated, fromPeer, latency: latency);
        }

        internal void MessageDelivered(NetPeer fromPeer, object userData)
        {
            if(_deliveryEventListener != null)
                CreateEvent(NetEvent.EType.MessageDelivered, fromPeer, userData: userData);
        }

        internal int SendRawAndRecycle(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            var result = SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
            NetPacketPool.Recycle(packet);
            return result;
        }

        internal int SendRaw(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            return SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
        }

        internal int SendRaw(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
        {
            if (!_socket.IsRunning)
                return 0;

            SocketError errorCode = 0;
            int result;
            if (_extraPacketLayer != null)
            {
                var expandedPacket = NetPacketPool.GetPacket(length + _extraPacketLayer.ExtraPacketSizeForLayer);
                Buffer.BlockCopy(message, start, expandedPacket.RawData, 0, length);
                int newStart = 0;
                _extraPacketLayer.ProcessOutBoundPacket(ref expandedPacket.RawData, ref newStart, ref length);
                result = _socket.SendTo(expandedPacket.RawData, newStart, length, remoteEndPoint, ref errorCode);
                NetPacketPool.Recycle(expandedPacket);
            }
            else
            {
                result = _socket.SendTo(message, start, length, remoteEndPoint, ref errorCode);
            }

            NetPeer fromPeer;
            switch (errorCode)
            {
                case SocketError.MessageSize:
                    NetDebug.Write(NetLogLevel.Trace, "[SRD] 10040, datalen: {0}", length);
                    return -1;
                case SocketError.HostUnreachable:
                    if (TryGetPeer(remoteEndPoint, out fromPeer))
                        DisconnectPeerForce(fromPeer, DisconnectReason.HostUnreachable, errorCode, null);
                    CreateEvent(NetEvent.EType.Error, remoteEndPoint: remoteEndPoint, errorCode: errorCode);
                    return -1;
                case SocketError.NetworkUnreachable:
                    if (TryGetPeer(remoteEndPoint, out fromPeer))
                        DisconnectPeerForce(fromPeer, DisconnectReason.NetworkUnreachable, errorCode, null);
                    CreateEvent(NetEvent.EType.Error, remoteEndPoint: remoteEndPoint, errorCode: errorCode);
                    return -1;
            }
            if (result <= 0)
                return 0;

            if (EnableStatistics)
            {
                Statistics.PacketsSent++;
                Statistics.BytesSent += (uint)length;
            }

            return result;
        }

        internal void DisconnectPeerForce(NetPeer peer,
            DisconnectReason reason,
            SocketError socketErrorCode,
            NetPacket eventData)
        {
            DisconnectPeer(peer, reason, socketErrorCode, true, null, 0, 0, eventData);
        }

        private void DisconnectPeer(
            NetPeer peer, 
            DisconnectReason reason,
            SocketError socketErrorCode, 
            bool force,
            byte[] data,
            int start,
            int count,
            NetPacket eventData)
        {
            var shutdownResult = peer.Shutdown(data, start, count, force);
            if (shutdownResult == ShutdownResult.None)
                return;
            if(shutdownResult == ShutdownResult.WasConnected)
                _connectedPeersCount--;
            CreateEvent(
                NetEvent.EType.Disconnect,
                peer,
                errorCode: socketErrorCode,
                disconnectReason: reason,
                readerSource: eventData);
        }

        private void CreateEvent(
            NetEvent.EType type,
            NetPeer peer = null,
            IPEndPoint remoteEndPoint = null,
            SocketError errorCode = 0,
            int latency = 0,
            DisconnectReason disconnectReason = DisconnectReason.ConnectionFailed,
            ConnectionRequest connectionRequest = null,
            DeliveryMethod deliveryMethod = DeliveryMethod.Unreliable,
            NetPacket readerSource = null,
            object userData = null)
        {
            NetEvent evt;
            bool unsyncEvent = UnsyncedEvents;
            
            if (type == NetEvent.EType.Connect)
                _connectedPeersCount++;
            else if (type == NetEvent.EType.MessageDelivered)
                unsyncEvent = UnsyncedDeliveryEvent;

            lock (_netEventsPool)
            {
                evt = _netEventsPool.Count > 0 ? _netEventsPool.Pop() : new NetEvent(this);
            }
            evt.Type = type;
            evt.DataReader.SetSource(readerSource);
            evt.Peer = peer;
            evt.RemoteEndPoint = remoteEndPoint;
            evt.Latency = latency;
            evt.ErrorCode = errorCode;
            evt.DisconnectReason = disconnectReason;
            evt.ConnectionRequest = connectionRequest;
            evt.DeliveryMethod = deliveryMethod;
            evt.UserData = userData;

            if (unsyncEvent)
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
            NetDebug.Write("[NM] Processing event: " + evt.Type);
            bool emptyData = evt.DataReader.IsNull;
            switch (evt.Type)
            {
                case NetEvent.EType.Connect:
                    _netEventListener.OnPeerConnected(evt.Peer);
                    break;
                case NetEvent.EType.Disconnect:
                    var info = new DisconnectInfo
                    {
                        Reason = evt.DisconnectReason,
                        AdditionalData = evt.DataReader,
                        SocketErrorCode = evt.ErrorCode
                    };
                    _netEventListener.OnPeerDisconnected(evt.Peer, info);
                    break;
                case NetEvent.EType.Receive:
                    _netEventListener.OnNetworkReceive(evt.Peer, evt.DataReader, evt.DeliveryMethod);
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
                    _netEventListener.OnNetworkLatencyUpdate(evt.Peer, evt.Latency);
                    break;
                case NetEvent.EType.ConnectionRequest:
                    _netEventListener.OnConnectionRequest(evt.ConnectionRequest);
                    break;
                case NetEvent.EType.MessageDelivered:
                    _deliveryEventListener.OnMessageDelivered(evt.Peer, evt.UserData);
                    break;
            }
            //Recycle if not message
            if (emptyData)
                RecycleEvent(evt);
            else if (AutoRecycle)
                evt.DataReader.RecycleInternal();
        }

        internal void RecycleEvent(NetEvent evt)
        {
            evt.Peer = null;
            evt.ErrorCode = 0;
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

            while (_socket.IsRunning)
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

                ulong totalPacketLoss = 0;

                int elapsed = (int)stopwatch.ElapsedMilliseconds;
                elapsed = elapsed <= 0 ? 1 : elapsed;
                stopwatch.Reset();
                stopwatch.Start();

                for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
                {
                    if (netPeer.ConnectionState == ConnectionState.Disconnected && netPeer.TimeSinceLastPacket > DisconnectTimeout)
                    {
                        peersToRemove.Add(netPeer);
                    }
                    else
                    {
                        netPeer.Update(elapsed);

                        if (EnableStatistics)
                        {
                            totalPacketLoss += netPeer.Statistics.PacketLoss;
                        }
                    }
                }
                if (peersToRemove.Count > 0)
                {
                    _peersLock.EnterWriteLock();
                    for (int i = 0; i < peersToRemove.Count; i++)
                        RemovePeerInternal(peersToRemove[i]);
                    _peersLock.ExitWriteLock();
                    peersToRemove.Clear();
                }

                if (EnableStatistics)
                {
                    Statistics.PacketLoss = totalPacketLoss;
                }

                int sleepTime = UpdateTime - (int)stopwatch.ElapsedMilliseconds;
                if (sleepTime > 0)
                    Thread.Sleep(sleepTime);
            }
            stopwatch.Stop();
        }
        
        void INetSocketListener.OnMessageReceived(byte[] data, int length, SocketError errorCode, IPEndPoint remoteEndPoint)
        {
            if (errorCode != 0)
            {
                CreateEvent(NetEvent.EType.Error, errorCode: errorCode);
                NetDebug.WriteError("[NM] Receive error: {0}", errorCode);
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
                if (latency > MinLatencyThreshold)
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
                NetDebug.WriteError("[NM] SocketReceiveThread error: " + e );
            }
        }

        internal NetPeer OnConnectionSolved(ConnectionRequest request, byte[] rejectData, int start, int length)
        {
            NetPeer netPeer = null;

            if (request.Result == ConnectionRequestResult.RejectForce)
            {
                NetDebug.Write(NetLogLevel.Trace, "[NM] Peer connect reject force.");
                if (rejectData != null && length > 0)
                {
                    var shutdownPacket = NetPacketPool.GetWithProperty(PacketProperty.Disconnect, length);
                    shutdownPacket.ConnectionNumber = request.ConnectionNumber;
                    FastBitConverter.GetBytes(shutdownPacket.RawData, 1, request.ConnectionTime);
                    if (shutdownPacket.Size >= NetConstants.PossibleMtu[0])
                        NetDebug.WriteError("[Peer] Disconnect additional data size more than MTU!");
                    else
                        Buffer.BlockCopy(rejectData, start, shutdownPacket.RawData, 9, length);
                    SendRawAndRecycle(shutdownPacket, request.RemoteEndPoint);
                }
            }
            else
            {
                _peersLock.EnterUpgradeableReadLock();
                if (_peersDict.TryGetValue(request.RemoteEndPoint, out netPeer))
                {
                    //already have peer
                    _peersLock.ExitUpgradeableReadLock();
                }
                else if (request.Result == ConnectionRequestResult.Reject)
                {
                    netPeer = new NetPeer(this, request.RemoteEndPoint, GetNextPeerId());
                    netPeer.Reject(request.ConnectionTime, request.ConnectionNumber, rejectData, start, length);
                    AddPeer(netPeer);
                    _peersLock.ExitUpgradeableReadLock();
                    NetDebug.Write(NetLogLevel.Trace, "[NM] Peer connect reject.");
                }
                else //Accept
                {
                    netPeer = new NetPeer(this, request.RemoteEndPoint, GetNextPeerId(), request.ConnectionTime, request.ConnectionNumber);
                    AddPeer(netPeer);
                    _peersLock.ExitUpgradeableReadLock();
                    CreateEvent(NetEvent.EType.Connect, netPeer);
                    NetDebug.Write(NetLogLevel.Trace, "[NM] Received peer connection Id: {0}, EP: {1}",
                        netPeer.ConnectTime, netPeer.EndPoint);
                }
            }

            lock(_requestsDict)
                _requestsDict.Remove(request.RemoteEndPoint);

            return netPeer;
        }

        private int GetNextPeerId()
        {
            lock (_peerIds)
                return _peerIds.Count == 0 ? _lastPeerId++ : _peerIds.Dequeue();
        }

        private void ProcessConnectRequest(
            IPEndPoint remoteEndPoint, 
            NetPeer netPeer, 
            NetConnectRequestPacket connRequest)
        {
            byte connectionNumber = connRequest.ConnectionNumber;
            ConnectionRequest req;

            //if we have peer
            if (netPeer != null)
            {
                var processResult = netPeer.ProcessConnectRequest(connRequest);
                NetDebug.Write("ConnectRequest LastId: {0}, NewId: {1}, EP: {2}, Result: {3}", 
                    netPeer.ConnectTime, 
                    connRequest.ConnectionTime, 
                    remoteEndPoint,
                    processResult);

                switch (processResult)
                {
                    case ConnectRequestResult.Reconnection:
                        DisconnectPeerForce(netPeer, DisconnectReason.Reconnect, 0, null);
                        RemovePeer(netPeer);
                        //go to new connection
                        break;
                    case ConnectRequestResult.NewConnection:
                        RemovePeer(netPeer);
                        //go to new connection
                        break;
                    case ConnectRequestResult.P2PConnection:
                        lock (_requestsDict)
                        {
                            req = new ConnectionRequest(
                                netPeer.ConnectTime,
                                connectionNumber,
                                ConnectionRequestType.PeerToPeer,
                                connRequest.Data,
                                remoteEndPoint,
                                this);
                            _requestsDict.Add(remoteEndPoint, req);
                        }
                        CreateEvent(NetEvent.EType.ConnectionRequest, connectionRequest: req);
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
            else
            {
                NetDebug.Write("ConnectRequest Id: {0}, EP: {1}", connRequest.ConnectionTime, remoteEndPoint);
            }

            lock (_requestsDict)
            {
                if (_requestsDict.TryGetValue(remoteEndPoint, out req))
                {
                    req.UpdateRequest(connRequest);
                    return;
                }
                req = new ConnectionRequest(
                    connRequest.ConnectionTime,
                    connectionNumber,
                    ConnectionRequestType.Incoming,
                    connRequest.Data,
                    remoteEndPoint,
                    this);
                _requestsDict.Add(remoteEndPoint, req);
            }
            NetDebug.Write("[NM] Creating request event: " + connRequest.ConnectionTime);
            CreateEvent(NetEvent.EType.ConnectionRequest, connectionRequest: req);
        }

        private void DataReceived(byte[] reusableBuffer, int count, IPEndPoint remoteEndPoint)
        {
            if (EnableStatistics)
            {
                Statistics.PacketsReceived++;
                Statistics.BytesReceived += (uint)count;
            }

            if (_extraPacketLayer != null)
            {
                _extraPacketLayer.ProcessInboundPacket(ref reusableBuffer, ref count);
                if (count == 0)
                    return;
            }

            //Try read packet
            NetPacket packet = NetPacketPool.GetPacket(count);
            if (!packet.FromBytes(reusableBuffer, 0, count))
            {
                NetPacketPool.Recycle(packet);
                NetDebug.WriteError("[NM] DataReceived: bad!");
                return;
            }

            NetPeer netPeer;

            //special case connect request
            if (packet.Property == PacketProperty.ConnectRequest)
            {
                if (NetConnectRequestPacket.GetProtocolId(packet) != NetConstants.ProtocolId)
                {
                    SendRawAndRecycle(NetPacketPool.GetWithProperty(PacketProperty.InvalidProtocol), remoteEndPoint);
                    return;
                }
                var connRequest = NetConnectRequestPacket.FromData(packet);
                if (connRequest != null)
                {
                    _peersLock.EnterUpgradeableReadLock();
                    _peersDict.TryGetValue(remoteEndPoint, out netPeer);
                    //here new peer can be created
                    ProcessConnectRequest(remoteEndPoint, netPeer, connRequest);
                    _peersLock.ExitUpgradeableReadLock();
                }
                return;
            }

            _peersLock.EnterReadLock();
            bool peerFound = _peersDict.TryGetValue(remoteEndPoint, out netPeer);
            _peersLock.ExitReadLock();

            //Check normal packets
            switch (packet.Property)
            {
                case PacketProperty.PeerNotFound:
                    if (peerFound)
                    {
                        if (netPeer.ConnectionState != ConnectionState.Connected)
                            return;
                        if (packet.Size == 1) 
                        {
                            //first reply
                            var p = NetPacketPool.GetWithProperty(PacketProperty.PeerNotFound, 9);
                            p.RawData[1] = 0;
                            FastBitConverter.GetBytes(p.RawData, 2, netPeer.ConnectTime);
                            SendRawAndRecycle(p, remoteEndPoint);
                            NetDebug.Write("PeerNotFound sending connectTime: {0}", netPeer.ConnectTime);
                        }
                        else if (packet.Size == 10 && packet.RawData[1] == 1 && BitConverter.ToInt64(packet.RawData, 2) == netPeer.ConnectTime) 
                        {
                            //second reply
                            NetDebug.Write("PeerNotFound received our connectTime: {0}", netPeer.ConnectTime);
                            DisconnectPeerForce(netPeer, DisconnectReason.RemoteConnectionClose, 0, null);
                        }
                    }
                    else if (packet.Size == 10 && packet.RawData[1] == 0)
                    {
                        //send reply back
                        packet.RawData[1] = 1;
                        SendRawAndRecycle(packet, remoteEndPoint);
                    }
                    break;
                case PacketProperty.Broadcast:
                    if (!BroadcastReceiveEnabled)
                        break;
                    CreateEvent(NetEvent.EType.Broadcast, remoteEndPoint: remoteEndPoint, readerSource: packet);
                    break;

                case PacketProperty.UnconnectedMessage:
                    if (!UnconnectedMessagesEnabled)
                        break;
                    CreateEvent(NetEvent.EType.ReceiveUnconnected, remoteEndPoint: remoteEndPoint, readerSource: packet);
                    break;

                case PacketProperty.NatIntroduction:
                case PacketProperty.NatIntroductionRequest:
                case PacketProperty.NatPunchMessage:
                    if (NatPunchEnabled)
                        NatPunchModule.ProcessMessage(remoteEndPoint, packet);
                    break;
                case PacketProperty.InvalidProtocol:
                    if (peerFound && netPeer.ConnectionState == ConnectionState.Outgoing)
                        DisconnectPeerForce(netPeer, DisconnectReason.InvalidProtocol, 0, null);
                    break;
                case PacketProperty.Disconnect:
                    if (peerFound)
                    {
                        var disconnectResult = netPeer.ProcessDisconnect(packet);
                        if (disconnectResult == DisconnectResult.None)
                        {
                            NetPacketPool.Recycle(packet);
                            return;
                        }
                        DisconnectPeerForce(
                            netPeer, 
                            disconnectResult == DisconnectResult.Disconnect
                            ? DisconnectReason.RemoteConnectionClose
                            : DisconnectReason.ConnectionRejected,
                            0, packet);
                    }
                    else
                    {
                        NetPacketPool.Recycle(packet);
                    }
                    //Send shutdown
                    SendRawAndRecycle(NetPacketPool.GetWithProperty(PacketProperty.ShutdownOk), remoteEndPoint);
                    break;

                case PacketProperty.ConnectAccept:
                    var connAccept = NetConnectAcceptPacket.FromData(packet);
                    if (connAccept != null && peerFound && netPeer.ProcessConnectAccept(connAccept))
                        CreateEvent(NetEvent.EType.Connect, netPeer);
                    break;
                default:
                    if(peerFound)
                        netPeer.ProcessPacket(packet);
                    else
                        SendRawAndRecycle(NetPacketPool.GetWithProperty(PacketProperty.PeerNotFound), remoteEndPoint);
                    break;
            }
        }

        internal void CreateReceiveEvent(NetPacket packet, DeliveryMethod method, NetPeer fromPeer)
        {
            NetEvent evt;
            lock (_netEventsPool)
            {
                evt = _netEventsPool.Count > 0 ? _netEventsPool.Pop() : new NetEvent(this);
            }
            evt.Type = NetEvent.EType.Receive;
            evt.DataReader.SetSource(packet);
            evt.Peer = fromPeer;
            evt.DeliveryMethod = method;
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

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(NetDataWriter writer, DeliveryMethod options)
        {
            SendToAll(writer.Data, 0, writer.Length, options);
        }

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, DeliveryMethod options)
        {
            SendToAll(data, 0, data.Length, options);
        }

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, int start, int length, DeliveryMethod options)
        {
            SendToAll(data, start, length, 0, options);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options)
        {
            SendToAll(writer.Data, 0, writer.Length, channelNumber, options);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options)
        {
            SendToAll(data, 0, data.Length, channelNumber, options);
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
                    netPeer.Send(data, start, length, channelNumber, options);
            }
            finally
            {
                _peersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(NetDataWriter writer, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(writer.Data, 0, writer.Length, 0, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(data, 0, data.Length, 0, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, int start, int length, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(data, start, length, 0, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="writer">DataWriter with data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(NetDataWriter writer, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(writer.Data, 0, writer.Length, channelNumber, options, excludePeer);
        }

        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
        {
            SendToAll(data, 0, data.Length, channelNumber, options, excludePeer);
        }


        /// <summary>
        /// Send data to all connected peers
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <param name="excludePeer">Excluded peer</param>
        public void SendToAll(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options, NetPeer excludePeer)
        {
            try
            {
                _peersLock.EnterReadLock();
                for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
                {
                    if (netPeer != excludePeer)
                        netPeer.Send(data, start, length, channelNumber, options);
                }
            }
            finally
            {
                _peersLock.ExitReadLock();
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
            if (!_socket.Bind(addressIPv4, addressIPv6, port, ReuseAddress, IPv6Enabled))
                return false;
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
            //No need for CRC here, SendRaw does that
            NetPacket packet = NetPacketPool.GetWithData(PacketProperty.UnconnectedMessage, message, start, length);
            return SendRawAndRecycle(packet, remoteEndPoint) > 0;
        }

        public bool SendBroadcast(NetDataWriter writer, int port)
        {
            return SendBroadcast(writer.Data, 0, writer.Length, port);
        }

        public bool SendBroadcast(byte[] data, int port)
        {
            return SendBroadcast(data, 0, data.Length, port);
        }

        public bool SendBroadcast(byte[] data, int start, int length, int port)
        {
            NetPacket packet;
            if (_extraPacketLayer != null)
            {
                var headerSize = NetPacket.GetHeaderSize(PacketProperty.Broadcast);
                packet = NetPacketPool.GetPacket(headerSize + length + _extraPacketLayer.ExtraPacketSizeForLayer);
                packet.Property = PacketProperty.Broadcast;
                Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
                var checksumComputeStart = 0;
                int preCrcLength = length + headerSize;
                _extraPacketLayer.ProcessOutBoundPacket(ref packet.RawData, ref checksumComputeStart, ref preCrcLength);
            }
            else
            {
                packet = NetPacketPool.GetWithData(PacketProperty.Broadcast, data, start, length);
            }

            bool result = _socket.SendBroadcast(packet.RawData, 0, packet.Size, port);
            NetPacketPool.Recycle(packet);
            return result;
        }

        /// <summary>
        /// Flush all queued packets of all peers
        /// </summary>
        public void Flush()
        {
            for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
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
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(string address, int port, string key)
        {
            return Connect(address, port, NetDataWriter.FromString(key));
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="address">Server IP or hostname</param>
        /// <param name="port">Server Port</param>
        /// <param name="connectionData">Additional data for remote peer</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(string address, int port, NetDataWriter connectionData)
        {
            IPEndPoint ep;
            try
            {
                ep = NetUtils.MakeEndPoint(address, port);
            }
            catch
            {
                CreateEvent(NetEvent.EType.Disconnect, disconnectReason: DisconnectReason.UnknownHost);
                return null;
            }
            return Connect(ep, connectionData);
        }

        /// <summary>
        /// Connect to remote host
        /// </summary>
        /// <param name="target">Server end point (ip and port)</param>
        /// <param name="key">Connection key</param>
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected</returns>
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
        /// <returns>New NetPeer if new connection, Old NetPeer if already connected, null peer if there is ConnectionRequest awaiting</returns>
        /// <exception cref="InvalidOperationException">Manager is not running. Call <see cref="Start()"/></exception>
        public NetPeer Connect(IPEndPoint target, NetDataWriter connectionData)
        {
            if (!_socket.IsRunning)
                throw new InvalidOperationException("Client is not running");

            NetPeer peer;
            byte connectionNumber = 0;

            if (_requestsDict.ContainsKey(target))
                return null;

            _peersLock.EnterUpgradeableReadLock();
            if (_peersDict.TryGetValue(target, out peer))
            {
                switch (peer.ConnectionState)
                {
                    //just return already connected peer
                    case ConnectionState.Connected:
                    case ConnectionState.Outgoing:
                        _peersLock.ExitUpgradeableReadLock();
                        return peer;
                }
                //else reconnect
                connectionNumber = (byte)((peer.ConnectionNum + 1) % NetConstants.MaxConnectionNumber);
                RemovePeer(peer);
            }

            //Create reliable connection
            //And send connection request
            peer = new NetPeer(this, target, GetNextPeerId(), connectionNumber, connectionData);
            AddPeer(peer);
            _peersLock.ExitUpgradeableReadLock();

            return peer;
        }

        /// <summary>
        /// Force closes connection and stop all threads.
        /// </summary>
        public void Stop()
        {
            Stop(true);
        }

        /// <summary>
        /// Force closes connection and stop all threads.
        /// </summary>
        /// <param name="sendDisconnectMessages">Send disconnect messages</param>
        public void Stop(bool sendDisconnectMessages)
        {
            if (!_socket.IsRunning)
                return;
            NetDebug.Write("[NM] Stop");

            //Send last disconnect
            for(var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
                netPeer.Shutdown(null, 0, 0, !sendDisconnectMessages);

            //Stop
            _socket.Close(false);
            _logicThread.Join();
            _logicThread = null;

            //clear peers
            _peersLock.EnterWriteLock();
            _headPeer = null;
            _peersDict.Clear();
            _peersArray = new NetPeer[32];
            _peersLock.ExitWriteLock();
            lock(_peerIds)
                _peerIds.Clear();
#if DEBUG
            lock (_pingSimulationList)
                _pingSimulationList.Clear();
#endif
            _connectedPeersCount = 0;
            lock(_netEventsQueue)
                _netEventsQueue.Clear();
        }

        /// <summary>
        /// Return peers count with connection state
        /// </summary>
        /// <param name="peerState">peer connection state (you can use as bit flags)</param>
        /// <returns>peers count</returns>
        public int GetPeersCount(ConnectionState peerState)
        {
            int count = 0;
            _peersLock.EnterReadLock();
            for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if ((netPeer.ConnectionState & peerState) != 0)
                    count++;
            }
            _peersLock.ExitReadLock();
            return count;
        }

        /// <summary>
        /// Get copy of peers (without allocations)
        /// </summary>
        /// <param name="peers">List that will contain result</param>
        /// <param name="peerState">State of peers</param>
        public void GetPeersNonAlloc(List<NetPeer> peers, ConnectionState peerState)
        {
            peers.Clear();
            _peersLock.EnterReadLock();
            for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                if ((netPeer.ConnectionState & peerState) != 0)
                    peers.Add(netPeer);
            }
            _peersLock.ExitReadLock();
        }

        /// <summary>
        /// Disconnect all peers without any additional data
        /// </summary>
        public void DisconnectAll()
        {
            DisconnectAll(null, 0, 0);
        }

        /// <summary>
        /// Disconnect all peers with shutdown message
        /// </summary>
        /// <param name="data">Data to send (must be less or equal MTU)</param>
        /// <param name="start">Data start</param>
        /// <param name="count">Data count</param>
        public void DisconnectAll(byte[] data, int start, int count)
        {
            //Send disconnect packets
            _peersLock.EnterReadLock();
            for (var netPeer = _headPeer; netPeer != null; netPeer = netPeer.NextPeer)
            {
                DisconnectPeer(
                    netPeer, 
                    DisconnectReason.DisconnectPeerCalled, 
                    0, 
                    false,
                    data, 
                    start, 
                    count,
                    null);
            }
            _peersLock.ExitReadLock();
        }

        /// <summary>
        /// Immediately disconnect peer from server without additional data
        /// </summary>
        /// <param name="peer">peer to disconnect</param>
        public void DisconnectPeerForce(NetPeer peer)
        {
            DisconnectPeerForce(peer, DisconnectReason.DisconnectPeerCalled, 0, null);
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
                count,
                null);
        }

        public NetPeerEnumerator GetEnumerator()
        {
            return new NetPeerEnumerator(_headPeer);
        }

        IEnumerator<NetPeer> IEnumerable<NetPeer>.GetEnumerator()
        {
            return new NetPeerEnumerator(_headPeer);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new NetPeerEnumerator(_headPeer);
        }
    }
}
