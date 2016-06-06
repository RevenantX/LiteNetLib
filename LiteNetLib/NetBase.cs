using System;
using System.Collections.Generic;
using System.Threading;
using LiteNetLib.Utils;

#if WINRT && !UNITY_EDITOR
using Windows.System.Threading;
#endif

namespace LiteNetLib
{
    public enum ConnectionAddressType
    {
        IPv4,
        IPv6
    }

    internal sealed class FlowMode
    {
        public int PacketsPerSecond;
        public int StartRtt;
    }

    public abstract class NetBase
    {
        protected enum NetEventType
        {
            Connect,
            Disconnect,
            Receive,
            ReceiveUnconnected,
            Error,
            ConnectionLatencyUpdated
        }

        protected sealed class NetEvent
        {
            public NetPeer Peer { get; internal set; }
            public NetDataReader DataReader { get; internal set; }
            public NetEventType Type { get; internal set; }
            public NetEndPoint RemoteEndPoint { get; internal set; }
            public string AdditionalInfo { get; internal set; }
            public int Latency { get; internal set; }
        }

#if DEBUG
        private struct IncomingData
        {
            public byte[] Data;
            public NetEndPoint EndPoint;
            public DateTime TimeWhenGet;
        }
        private readonly LinkedList<IncomingData> _pingSimulationList = new LinkedList<IncomingData>(); 
#endif

        private readonly NetSocket _socket;
        private readonly List<FlowMode> _flowModes;
        private NetEndPoint _localEndPoint;
        private readonly ConnectionAddressType _addressType;

#if WINRT && !UNITY_EDITOR
        private readonly ManualResetEvent _updateWaiter = new ManualResetEvent(false);
#else
        private Thread _logicThread;
        private Thread _receiveThread;
#endif
        private NetEndPoint _remoteEndPoint;

        private bool _running;
        private readonly Queue<NetEvent> _netEventsQueue;
        private readonly Stack<NetEvent> _netEventsPool;
        private readonly Random _randomGenerator = new Random();
        private readonly INetEventListener _netEventListener;

        //config section
        public bool UnconnectedMessagesEnabled = false;
        public bool NatPunchEnabled = false;
        public int UpdateTime = 100;
        public int ReliableResendTime = 500;
        public int PingInterval = NetConstants.DefaultPingInterval;
        public long DisconnectTimeout = 5000;
        public bool SimulatePacketLoss = false;
        public bool SimulateLatency = false;
        public int SimulationPacketLossChance = 10;
        public int SimulationMaxLatency = 100;

        //modules
        public readonly NatPunchModule NatPunchModule;

        public ConnectionAddressType AddressType
        {
            get { return _addressType; }
        }

        public void AddFlowMode(int startRtt, int packetsPerSecond)
        {
            var fm = new FlowMode {PacketsPerSecond = packetsPerSecond, StartRtt = startRtt};

            if (_flowModes.Count > 0 && startRtt < _flowModes[0].StartRtt)
            {
                _flowModes.Insert(0, fm);
            }
            else
            {
                _flowModes.Add(fm);
            }
        }

        internal int GetPacketsPerSecond(int flowMode)
        {
            if (flowMode < 0 || _flowModes.Count == 0)
                return NetConstants.PacketsPerSecondMax;
            return _flowModes[flowMode].PacketsPerSecond;
        }

        internal int GetMaxFlowMode()
        {
            return _flowModes.Count - 1;
        }

        internal int GetStartRtt(int flowMode)
        {
            if (flowMode < 0 || _flowModes.Count == 0)
                return 0;
            return _flowModes[flowMode].StartRtt;
        }

        protected NetBase(INetEventListener listener) : this(listener, ConnectionAddressType.IPv4)
        {
        }

        protected NetBase(INetEventListener listener, ConnectionAddressType addressType)
        {
            _socket = new NetSocket(addressType);
            _addressType = addressType;
            _netEventListener = listener;
            _flowModes = new List<FlowMode>();
            _netEventsQueue = new Queue<NetEvent>();
            _netEventsPool = new Stack<NetEvent>();
            _remoteEndPoint = new NetEndPoint(_addressType, 0);
            NatPunchModule = new NatPunchModule(this, _socket);
        }

        protected void SocketClearPeers()
        {
#if WINRT && !UNITY_EDITOR
            _socket.ClearPeers();
#endif
        }

        protected void SocketRemovePeer(NetEndPoint ep)
        {
#if WINRT && !UNITY_EDITOR
            _socket.RemovePeer(ep);
#endif
        }

        protected NetPeer CreatePeer(NetEndPoint remoteEndPoint)
        {
            var peer = new NetPeer(this, _socket, remoteEndPoint);
            peer.PingInterval = PingInterval;
            return peer;
        }

        internal void ConnectionLatencyUpdated(NetPeer fromPeer, int latency)
        {
            var evt = CreateEvent(NetEventType.ConnectionLatencyUpdated);
            evt.Peer = fromPeer;
            evt.Latency = latency;
            EnqueueEvent(evt);
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
        /// <param name="port">port to listen</param>
        public virtual bool Start(int port)
        {
            if (_running)
            {
                return false;
            }

            _netEventsQueue.Clear();
            _localEndPoint = new NetEndPoint(_addressType, port);
            if (!_socket.Bind(ref _localEndPoint))
                return false;

            _running = true;
#if WINRT && !UNITY_EDITOR
            ThreadPool.RunAsync(
                a => UpdateLogic(), 
                WorkItemPriority.Normal, 
                WorkItemOptions.TimeSliced).AsTask();

            ThreadPool.RunAsync(
                a => ReceiveLogic(), 
                WorkItemPriority.Normal,
                WorkItemOptions.TimeSliced).AsTask();
#else
            _logicThread = new Thread(UpdateLogic);
            _receiveThread = new Thread(ReceiveLogic);
            _logicThread.IsBackground = true;
            _logicThread.Start();
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
#endif
            return true;
        }

        /// <summary>
        /// Send message without connection
        /// </summary>
        /// <param name="message">Raw data</param>
        /// <param name="remoteEndPoint">Packet destination</param>
        /// <returns>Operation result</returns>
        public bool SendUnconnectedMessage(byte[] message, NetEndPoint remoteEndPoint)
        {
            return SendUnconnectedMessage(message, 0, message.Length, remoteEndPoint);
        }

        /// <summary>
        /// Send message without connection
        /// </summary>
        /// <param name="message">Raw data</param>
        /// <param name="start">data start</param>
        /// <param name="length">data length</param>
        /// <param name="remoteEndPoint">Packet destination</param>
        /// <returns>Operation result</returns>
        public bool SendUnconnectedMessage(byte[] message, int start, int length, NetEndPoint remoteEndPoint)
        {
            if (!_running)
                return false;
            NetPacket p = new NetPacket();
            p.Init(PacketProperty.UnconnectedMessage, length);
            p.PutData(message, start, length);
            return _socket.SendTo(p.RawData, remoteEndPoint) > 0;
        }

        /// <summary>
        /// Stop updating thread and listening
        /// </summary>
        public virtual void Stop()
        {
            if (_running)
            {
                _running = false;
#if !WINRT || UNITY_EDITOR
                if(Thread.CurrentThread != _logicThread)
                    _logicThread.Join();
                if(Thread.CurrentThread != _receiveThread)
                    _receiveThread.Join();
                _logicThread = null;
                _receiveThread = null;
#endif
                _socket.Close();
            }
        }

        /// <summary>
        /// Returns true if socket listening and update thread is running
        /// </summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>
        /// Returns local EndPoint (host and port)
        /// </summary>
        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        protected NetEvent CreateEvent(NetEventType type)
        {
            NetEvent evt;
            if (_netEventsPool.Count > 0)
            {
                lock (_netEventsPool)
                {
                    evt = _netEventsPool.Pop();
                }
            }
            else
            {
                evt = new NetEvent();
                evt.DataReader = new NetDataReader();
            }
            evt.Type = type;
            return evt;
        }

        protected void EnqueueEvent(NetEvent evt)
        {
            lock (_netEventsQueue)
            {
                _netEventsQueue.Enqueue(evt);
            }
        }

        public void PollEvents()
        {
            while (_netEventsQueue.Count > 0)
            {
                NetEvent evt;
                lock (_netEventsQueue)
                {
                    evt = _netEventsQueue.Dequeue();
                }
                switch (evt.Type)
                {
                    case NetEventType.Connect:
                        _netEventListener.OnPeerConnected(evt.Peer);
                        break;
                    case NetEventType.Disconnect:
                        _netEventListener.OnPeerDisconnected(evt.Peer, evt.AdditionalInfo);
                        break;
                    case NetEventType.Receive:
                        _netEventListener.OnNetworkReceive(evt.Peer, evt.DataReader);
                        break;
                    case NetEventType.ReceiveUnconnected:
                        _netEventListener.OnNetworkReceiveUnconnected(evt.RemoteEndPoint, evt.DataReader);
                        break;
                    case NetEventType.Error:
                        _netEventListener.OnNetworkError(evt.RemoteEndPoint, evt.AdditionalInfo);
                        break;
                    case NetEventType.ConnectionLatencyUpdated:
                        _netEventListener.OnNetworkLatencyUpdate(evt.Peer, evt.Latency);
                        break;
                }

                //Recycle
                evt.DataReader.Clear();
                evt.Peer = null;
                evt.AdditionalInfo = string.Empty;
                evt.RemoteEndPoint = null;

                lock (_netEventsPool)
                {
                    _netEventsPool.Push(evt);
                }
            }
        }

        //Update function
        private void UpdateLogic()
        {
            while (_running)
            {
                PostProcessEvent(UpdateTime);
#if WINRT && !UNITY_EDITOR
                _updateWaiter.WaitOne(UpdateTime);
#else
                Thread.Sleep(UpdateTime);
#endif
            }
        }

        private void ReceiveLogic()
        {
            while (_running)
            {
                int errorCode = 0;

                //Receive some info
                byte[] reusableBuffer = null;
                int result = _socket.ReceiveFrom(ref reusableBuffer, ref _remoteEndPoint, ref errorCode);

                if (result > 0)
                {
#if DEBUG
                    bool receivePacket = true;

                    if (SimulatePacketLoss && _randomGenerator.Next(100/SimulationPacketLossChance) == 0)
                    {
                        receivePacket = false;
                    }
                    else if (SimulateLatency)
                    {
                        int latency = _randomGenerator.Next(SimulationMaxLatency);
                        if (latency > 5)
                        {
                            byte[] holdedData = new byte[result];
                            Buffer.BlockCopy(reusableBuffer, 0, holdedData, 0, result);
                            _pingSimulationList.AddFirst(new IncomingData
                            {
                                Data = holdedData, EndPoint = _remoteEndPoint, TimeWhenGet = DateTime.UtcNow.AddMilliseconds(latency)
                            });
                            receivePacket = false;
                        }
                    }

                    if (receivePacket) //DataReceived
#endif
                        //ProcessEvents
                        DataReceived(reusableBuffer, result, _remoteEndPoint);
                }
                else if (result < 0)
                {
                    //10054 - remote close (not error)
                    //10040 - message too long (just for protection)
                    if (errorCode != 10054 && errorCode != 10040)
                    {
                        NetUtils.DebugWrite(ConsoleColor.Red, "(NB)Socket error: " + errorCode);
                        ProcessError("Receive socket error: " + errorCode);
                        Stop();
                        return;
                    }
                }
#if DEBUG
                if (SimulateLatency)
                {
                    var node = _pingSimulationList.First;
                    var time = DateTime.UtcNow;
                    while (node != null)
                    {
                        var incomingData = node.Value;
                        if (incomingData.TimeWhenGet <= time)
                        {
                            DataReceived(incomingData.Data, incomingData.Data.Length, incomingData.EndPoint);
                            var nodeToRemove = node;
                            node = node.Next;
                            _pingSimulationList.Remove(nodeToRemove);
                        }
                        else
                        {
                            node = node.Next;
                        }
                    }
                }
#endif
            }
        }

        private void DataReceived(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint)
        {
            //Try get packet property
            PacketProperty property;
            if (!NetPacket.GetPacketProperty(reusableBuffer, out property))
                return;

            //Check unconnected
            switch (property)
            {
                case PacketProperty.UnconnectedMessage:
                    if (UnconnectedMessagesEnabled)
                    {
                        var netEvent = CreateEvent(NetEventType.ReceiveUnconnected);
                        netEvent.RemoteEndPoint = remoteEndPoint;
                        netEvent.DataReader.SetSource(NetPacket.GetUnconnectedData(reusableBuffer, count));
                        EnqueueEvent(netEvent);
                    }
                    return;
                case PacketProperty.NatIntroduction:
                case PacketProperty.NatIntroductionRequest:
                case PacketProperty.NatPunchMessage:
                    if (NatPunchEnabled)
                        NatPunchModule.ProcessMessage(remoteEndPoint, property, NetPacket.GetUnconnectedData(reusableBuffer, count));
                    return;
            }

            //other
            ReceiveFromSocket(reusableBuffer, count, remoteEndPoint);
        }

        protected virtual void ProcessError(string errorMessage)
        {
            var netEvent = CreateEvent(NetEventType.Error);
            netEvent.AdditionalInfo = errorMessage;
            EnqueueEvent(netEvent);
        }

        protected abstract void ReceiveFromSocket(byte[] reusableBuffer, int count, NetEndPoint remoteEndPoint);
        protected abstract void PostProcessEvent(int deltaTime);
        internal abstract void ReceiveFromPeer(NetPacket packet, NetEndPoint endPoint);

        internal virtual void ProcessSendError(NetEndPoint endPoint, string errorMessage)
        {
            var netEvent = CreateEvent(NetEventType.Error);
            netEvent.RemoteEndPoint = endPoint;
            netEvent.AdditionalInfo = string.Format("Send error to {0}: {1}", endPoint, errorMessage);
            EnqueueEvent(netEvent);
        }
    }
}
