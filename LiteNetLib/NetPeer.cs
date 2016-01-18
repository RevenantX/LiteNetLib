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

        private INetSocket _socket;             //Udp socket
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

        public NetPeer(IPeerListener peerListener, INetSocket socket, IPEndPoint remoteEndPoint)
        {
            _id = NetConstants.GetIdFromEndPoint(remoteEndPoint);
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
            _pingUpdateDelay = 3000;
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
            packet.data = data;

            switch (options)
            {
                case SendOptions.Reliable:
                    packet.property = PacketProperty.Reliable;
                    break;
                case SendOptions.InOrder:
                    packet.property = PacketProperty.Sequenced;
                    break;
                case SendOptions.ReliableInOrder:
                    packet.property = PacketProperty.ReliableOrdered;
                    break;
                default:
                    packet.property = PacketProperty.None;
                    break;
            }

            SendPacket(packet);
        }

        public void Send(byte[] data, PacketProperty property)
        {
            NetPacket packet = CreatePacket(property);
            packet.property = property;
            packet.data = data;
            SendPacket(packet);
        }

        public void Send(PacketProperty property)
        {
            NetPacket packet = CreatePacket(property);
            packet.property = property;
            SendPacket(packet);
        }

        public void SendPacket(NetPacket packet)
        {
            switch (packet.property)
            {
                case PacketProperty.Reliable:
                    _reliableUnorderedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.Sequenced:
                    _sequencedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.ReliableOrdered:
                    _reliableOrderedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.AckReliable:
                case PacketProperty.AckReliableOrdered:
                case PacketProperty.Connect:
                case PacketProperty.Disconnect:
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.None:
                    NetUtils.DebugWrite(DebugTextColor, "[RS]Packet simple");
                    _outgoingQueue.Enqueue(packet);
                    break;
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
            var packet = _packetPool.Count > 0 
                ? _packetPool.Pop() 
                : new NetPacket();

            packet.property = property;
            return packet;
        }

        public void Recycle(NetPacket packet)
        {
            _packetPool.Push(packet);
        }

        public void AddIncomingPacket(NetPacket packet)
        {
            _peerListener.ReceiveFromPeer(packet, _remoteEndPoint);
        }

        //Process incoming packet
        public void ProcessPacket(NetPacket packet)
        {
            DebugWrite("[RR]PacketProperty: {0}", packet.property);
            switch (packet.property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    Send(PacketProperty.Pong);
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
                    _reliableUnorderedChannel.ProcessAck(packet.data);
                    break;

                case PacketProperty.AckReliableOrdered:
                    _reliableOrderedChannel.ProcessAck(packet.data);
                    break;

                //Process in order packets
                case PacketProperty.Sequenced:
                    if (!_sequencedChannel.ProcessPacket(packet))
                    {
                        Recycle(packet);
                    }
                    break;

                case PacketProperty.Reliable:
                    if (!_reliableUnorderedChannel.ProcessPacket(packet))
                    {
                        Recycle(packet);
                    }
                    break;

                case PacketProperty.ReliableOrdered:
                    if (!_reliableOrderedChannel.ProcessPacket(packet))
                    {
                        Recycle(packet);
                    }
                    break;

                //Simple packet without acks
                case PacketProperty.None:
                    _peerListener.ReceiveFromPeer(packet, _remoteEndPoint);
                    break;
            }
        }

        public void Update(int deltaTime)
        {
            int currentSended = 0;
            //Get current flow mode
            int maxSendPacketsCount = _flowModes[(int) _currentFlowMode];
            int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
            int currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount*deltaTime)/1000);

            DebugWrite("[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            //Reset queue index
            _reliableOrderedChannel.ResetQueueIndex();
            _reliableUnorderedChannel.ResetQueueIndex();

            if (currentMaxSend > 0)
            {
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
            }

            //ResetFlowTimer
            _flowTimer += deltaTime;
            if (_flowTimer >= 1000)
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
