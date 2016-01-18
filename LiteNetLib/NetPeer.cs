using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace LiteNetLib
{
    public interface IPeerListener
    {
        void ProcessReceivedPacket(NetPacket packet, EndPoint endPoint);
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
        private Queue<NetPacket> _sentQueue;    //Queue for sending packets
        private Stack<NetPacket> _packetPool; 

        private int _roundTripTime;             //RTT, Ping
        private int _maxReceivedTime;           //Max RTT
        private int _badRoundTripTime;

        private int _ping;
        private Stopwatch _pingStopwatch;
        private int _pingUpdateDelay;
        private int _pingUpdateTimer;

        private EndPoint _remoteEndPoint;

        //DEBUG
        public static ConsoleColor DebugTextColor = ConsoleColor.DarkGreen;

        public EndPoint EndPoint
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

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        private ReliableOrderedChannel _reliableOrderedChannel;
        private ReliableUnorderedChannel _reliableUnorderedChannel;
        private SequencedChannel _sequencedChannel;
        private int _id;
        private IPeerListener _peerListener;

        public NetPeer(IPeerListener peerListener, INetSocket socket, EndPoint remoteEndPoint, int id = 0)
        {
            _peerListener = peerListener;
            _id = id;
            _socket = socket;
            _remoteEndPoint = remoteEndPoint;

            _sentQueue = new Queue<NetPacket>();
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
                    _sentQueue.Enqueue(packet);
                    break;
            }
        }

        public void UpdateFlowMode(int roundTripTime)
        {
            _roundTripTime = roundTripTime;
            if (_roundTripTime < _badRoundTripTime)
            {
                if (_currentFlowMode != FlowMode.Good)
                    NetUtils.DebugWrite(DebugTextColor, "[PA]Enabled good flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Good;
            }
            else
            {
                if (_currentFlowMode != FlowMode.Bad)
                    NetUtils.DebugWrite(DebugTextColor, "[PA]Enabled bad flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Bad;
            }
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

        //Process incoming packet
        public void ProcessPacket(NetPacket packet)
        {
            NetUtils.DebugWrite(DebugTextColor, "[RR]PacketProperty: {0}", packet.property);
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
                    NetUtils.DebugWrite(DebugTextColor, "[PP]Ping: {0}", _ping);
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
                    _sequencedChannel.ProcessPacket(packet);
                    break;

                case PacketProperty.Reliable:
                    _reliableUnorderedChannel.ProcessPacket(packet);
                    break;

                case PacketProperty.ReliableOrdered:
                    _reliableOrderedChannel.ProcessPacket(packet);
                    break;

                //Simple packet without acks
                case PacketProperty.None:
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

            NetUtils.DebugWrite(DebugTextColor, "[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            if (currentMaxSend > 0)
            {
                //Pending send
                while (currentSended < currentMaxSend)
                {
                    NetPacket packet;
                    
                    _reliableOrderedChannel

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
                NetUtils.DebugWrite(DebugTextColor, "[UPDATE]Reset flow timer, _sendedPackets - {0}", _sendedPacketsCount);
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
