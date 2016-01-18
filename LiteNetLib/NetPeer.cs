using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace LiteNetLib
{
    public interface IPeerListener
    {
        void ReceiveFromPeer(NetPacket packet, EndPoint endPoint);
        void ProcessSendError(EndPoint endPoint);
    }

    public class NetPeer
    {
        private enum FlowMode
        {
            Bad,
            Good
        }

        //Flow control
        private FlowMode _currentFlowMode;
        private int[] _flowModes;
        private int _sendedPacketsCount;
        private int _flowTimer;

        private NetSocket _socket;             //Udp socket
        private Queue<NetPacket> _outgoingQueue;//Queue for sending packets
        private Stack<NetPacket> _packetPool; 

        private int _roundTripTime;             //RTT, Ping
        private int _maxReceivedTime;           //Max RTT
        private int _badRoundTripTime;

        private int _ping;
        private Stopwatch _pingStopwatch;
        private int _pingUpdateDelay;
        private int _pingUpdateTimer;

        private IPEndPoint _remoteEndPoint;

        //DEBUG
        public ConsoleColor DebugTextColor = ConsoleColor.DarkGreen;

        public IPEndPoint EndPoint
        {
            get { return _remoteEndPoint; }
        }

        public int MaxReceivedTime
        {
            get { return _maxReceivedTime; }
            set { _maxReceivedTime = value; }
        }

        public int BadRoundTripTime
        {
            get { return _badRoundTripTime; }
            set { _badRoundTripTime = value; }
        }

        public int Ping
        {
            get { return _ping; }
        }

        public long LastPing
        {
            get { return _pingStopwatch.ElapsedMilliseconds; }
        }

        public long Id
        {
            get { return _id; }
        }

        private ReliableOrderedChannel _reliableOrderedChannel;
        private ReliableUnorderedChannel _reliableUnorderedChannel;
        private SequencedChannel _sequencedChannel;
        private long _id;
        private IPeerListener _peerListener;

        public NetPeer(IPeerListener peerListener, NetSocket socket, IPEndPoint remoteEndPoint)
        {
            _id = NetUtils.GetIdFromEndPoint(remoteEndPoint);
            _peerListener = peerListener;
            
            _socket = socket;
            _remoteEndPoint = remoteEndPoint;

            _outgoingQueue = new Queue<NetPacket>();
            _flowModes = new int[2];
            _flowModes[0] = 64 / 4; //bad
            _flowModes[1] = 64;     //good

            _roundTripTime = 0;
            _maxReceivedTime = 0;
            _badRoundTripTime = 650;

            _ping = 0;
            _pingUpdateDelay = 1000;
            _pingUpdateTimer = 0;
            _pingStopwatch = new Stopwatch();

            _reliableOrderedChannel = new ReliableOrderedChannel(this);
            _reliableUnorderedChannel = new ReliableUnorderedChannel(this);
            _sequencedChannel = new SequencedChannel(this);

            _packetPool = new Stack<NetPacket>();
        }

        public void Send(byte[] data, SendOptions options)
        {
            NetPacket packet = CreatePacket();
            packet.Data = data;

            switch (options)
            {
                case SendOptions.Reliable:
                    packet.Property = PacketProperty.Reliable;
                    break;
                case SendOptions.Sequenced:
                    packet.Property = PacketProperty.Sequenced;
                    break;
                case SendOptions.ReliableOrdered:
                    packet.Property = PacketProperty.ReliableOrdered;
                    break;
                default:
                    packet.Property = PacketProperty.None;
                    break;
            }

            SendPacket(packet);
        }

        public void Send(byte[] data, PacketProperty property)
        {
            NetPacket packet = CreatePacket(property);
            packet.Property = property;
            packet.Data = data;
            SendPacket(packet);
        }

        public void Send(PacketProperty property)
        {
            NetPacket packet = CreatePacket(property);
            packet.Property = property;
            SendPacket(packet);
        }

        private readonly object sendLock = new object();
        public void SendPacket(NetPacket packet)
        {
            lock (sendLock)
            {
                switch (packet.Property)
                {
                    case PacketProperty.Reliable:
                        //DebugWrite("[RS]Packet reliable");
                        _reliableUnorderedChannel.AddToQueue(packet);
                        break;
                    case PacketProperty.Sequenced:
                        //DebugWrite("[RS]Packet sequenced");
                        _sequencedChannel.AddToQueue(packet);
                        break;
                    case PacketProperty.ReliableOrdered:
                        //DebugWrite("[RS]Packet reliable ordered");
                        _reliableOrderedChannel.AddToQueue(packet);
                        break;
                    case PacketProperty.AckReliable:
                    case PacketProperty.AckReliableOrdered:
                    case PacketProperty.Connect:
                    case PacketProperty.Disconnect:
                    case PacketProperty.None:
                        DebugWrite("[RS]Packet simple");
                        _outgoingQueue.Enqueue(packet);
                        break;
                }
            }
        }

        public void UpdateFlowMode(int roundTripTime)
        {
            _roundTripTime = roundTripTime;
            if (_roundTripTime < _badRoundTripTime)
            {
                if (_currentFlowMode != FlowMode.Good)
                    DebugWrite("[PA]Enabled good flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Good;
            }
            else
            {
                if (_currentFlowMode != FlowMode.Bad)
                    DebugWrite("[PA]Enabled bad flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Bad;
            }
        }

        public void DebugWrite(string str, params object[] args)
        {
            NetUtils.DebugWrite(DebugTextColor, str, args);
        }

        public NetPacket CreatePacket(PacketProperty property = PacketProperty.None)
        {
            lock (_packetPool)
            {
                var packet = _packetPool.Count > 0
                   ? _packetPool.Pop()
                   : new NetPacket();

                packet.Property = property;
                return packet;
            }
        }

        public void Recycle(NetPacket packet)
        {
            packet.Data = null;
            _packetPool.Push(packet);
        }

        public void AddIncomingPacket(NetPacket packet)
        {
            _peerListener.ReceiveFromPeer(packet, _remoteEndPoint);
            Recycle(packet);
        }

        //Process incoming packet
        public void ProcessPacket(NetPacket packet)
        {
            DebugWrite("[RR]PacketProperty: {0}", packet.Property);
            switch (packet.Property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    NetPacket pongPacket = CreatePacket(PacketProperty.Pong);
                    _socket.SendTo(pongPacket, _remoteEndPoint);
                    break;

                //If we get pong, calculate ping time and rtt
                case PacketProperty.Pong:
                    _ping = (int) _pingStopwatch.ElapsedMilliseconds;
                    _pingStopwatch.Reset();
                    UpdateFlowMode(_ping);
                    DebugWrite("[PP]Ping: {0}", _ping);
                    break;

                //Process ack
                case PacketProperty.AckReliable:
                    _reliableUnorderedChannel.ProcessAck(packet.Data);
                    break;

                case PacketProperty.AckReliableOrdered:
                    _reliableOrderedChannel.ProcessAck(packet.Data);
                    break;

                //Process in order packets
                case PacketProperty.Sequenced:
                    if (!_sequencedChannel.ProcessPacket(packet))
                    {
                        //Recycle(packet);
                    }
                    break;

                case PacketProperty.Reliable:
                    if (!_reliableUnorderedChannel.ProcessPacket(packet))
                    {
                        //Recycle(packet);
                    }
                    break;

                case PacketProperty.ReliableOrdered:
                    if (!_reliableOrderedChannel.ProcessPacket(packet))
                    {
                        //Recycle(packet);
                    }
                    break;

                //Simple packet without acks
                case PacketProperty.None:
                case PacketProperty.Connect:
                case PacketProperty.Disconnect:
                    _peerListener.ReceiveFromPeer(packet, _remoteEndPoint);
                    break;
            }
        }

        private const int FlowUpdateTime = 100;

        public void Update(int deltaTime)
        {
            int currentSended = 0;
            //Get current flow mode
            int maxSendPacketsCount = _flowModes[(int) _currentFlowMode];
            int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
            int currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount*deltaTime)/ FlowUpdateTime);

            DebugWrite("[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            //Pending send
            while (currentSended < currentMaxSend)
            {
                //Get one of packets
                NetPacket packet = _reliableOrderedChannel.GetQueuedPacket();
                if (packet == null)
                    packet = _reliableUnorderedChannel.GetQueuedPacket();
                if (packet == null)
                    packet = _sequencedChannel.GetQueuedPacket();
                if (packet == null)
                {
                    if (_outgoingQueue.Count > 0)
                        packet = _outgoingQueue.Dequeue();
                    else
                        break;
                }
                    
                //packet.timeStamp = Environment.TickCount;
                if (_socket.SendTo(packet, _remoteEndPoint) == -1)
                {
                    _peerListener.ProcessSendError(_remoteEndPoint);
                    return;
                }
                currentSended++;
            }

            //Increase counter
            _sendedPacketsCount += currentSended;

            //ResetFlowTimer
            _flowTimer += deltaTime;
            if (_flowTimer >= FlowUpdateTime)
            {
                DebugWrite("[UPDATE]Reset flow timer, _sendedPackets - {0}", _sendedPacketsCount);
                _sendedPacketsCount = 0;
                _flowTimer = 0;
            }

            //Send ping
            _pingUpdateTimer += deltaTime;
            if (_pingUpdateTimer >= _pingUpdateDelay)
            {
                _pingUpdateTimer = 0;
                //_pingStopwatch.Reset();
                NetPacket packet = CreatePacket(PacketProperty.Ping);
                _pingStopwatch.Start();
                _socket.SendTo(packet, _remoteEndPoint);
                Recycle(packet);
            }
        }
    }
}
