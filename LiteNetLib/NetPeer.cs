using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace LiteNetLib
{
    public interface IPeerListener
    {
        void ProcessPacket(NetPacket packet, EndPoint endPoint);
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

        private NetSocket _socket;              //Udp socket
        private Queue<NetPacket> _sentQueue;    //Queue for sending packets

        private int _roundTripTime;             //RTT, Ping
        private int _maxReceivedTime;           //Max RTT
        private int _badRoundTripTime;

        private int _ping;
        private Stopwatch _pingStopwatch;
        private int _pingUpdateDelay;
        private int _pingUpdateTimer;

        private EndPoint _remoteEndPoint;

        //DEBUG
        public ConsoleColor textColor = ConsoleColor.DarkGreen;

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
        }

        private ReliableOrderedChannel _reliableOrderedChannel;
        private ReliableUnorderedChannel _reliableUnorderedChannel;
        private SequencedChannel _sequencedChannel;
        private int _id;

        private IPeerListener _peerListener;

        public NetPeer(IPeerListener peerListener, NetSocket socket, EndPoint remoteEndPoint, int id)
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
        }

        public void Send(byte[] data, SendOptions options)
        {
            switch (options)
            {
                case SendOptions.Reliable:
                    _reliableUnorderedChannel.SendPacket(packet);
                    break;
                case SendOptions.InOrder:
                    _sequencedChannel.SendPacket(packet);
                    break;
                case SendOptions.ReliableInOrder:
                    _reliableOrderedChannel.SendPacket(packet);
                    break;
                default:
                    NetUtils.DebugWrite(textColor, "[RS]Packet simple");
                    break;
            }
        }

        private void UpdateFlowMode(int packetTimeStamp)
        {
            _roundTripTime = (Environment.TickCount - packetTimeStamp);
            if (_roundTripTime < _badRoundTripTime)
            {
                if (_currentFlowMode != FlowMode.Good)
                    NetUtils.DebugWrite(textColor, "[PA]Enabled good flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Good;
            }
            else
            {
                if (_currentFlowMode != FlowMode.Bad)
                    NetUtils.DebugWrite(textColor, "[PA]Enabled bad flow mode, RTT: {0}", _roundTripTime);

                _currentFlowMode = FlowMode.Bad;
            }
        }

        public void SendInfo(PacketInfo info, byte[] data)
        {
            NetPacket packet = new NetPacket();
            packet.info = info;
            packet.data = data;
            packet.property = PacketProperty.Reliable;
            SendPacket(packet);
        }

        public void SendInfo(PacketInfo info)
        {
            SendInfo(info, null);
        }

        //Send to
        public void Send(byte[] data, PacketProperty property)
        {
            //Creating packet
            NetPacket packet = new NetPacket();
            packet.property = property;
            packet.data = data;

            SendPacket(packet);
        }

        //Process incoming packet
        public void ProcessPacket(NetPacket packet)
        {
            NetUtils.DebugWrite(textColor, "[RR]PacketProperty: {0}", packet.property);
            switch (packet.property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    NetPacket outPacket = new NetPacket();
                    outPacket.property = PacketProperty.Pong;
                    _socket.SendTo(outPacket, _remoteEndPoint);
                    break;

                //If we get pong, calculate ping time
                case PacketProperty.Pong:
                    _ping = (int)_pingStopwatch.ElapsedMilliseconds;
                    _pingStopwatch.Reset();
                    NetUtils.DebugWrite(textColor, "[PP]Ping: {0}", _ping);
                    break;

                //Process ack
                case PacketProperty.Ack:
                    break;

                //Process in order packets
                case PacketProperty.InOrder:
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
            int maxSendPacketsCount = _flowModes[(int)_currentFlowMode];
            int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
            int currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount * deltaTime) / 1000);

            NetUtils.DebugWrite(textColor, "[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            if (currentMaxSend > 0)
            {
                //Pending send
                while (_sentQueue.Count > 0 && currentSended < currentMaxSend)
                {
                    NetPacket packet = _sentQueue.Dequeue();

                    if (packet.property == PacketProperty.Reliable)
                    {
                        _pendingAckPackets[packet.sequence % _windowSize] = packet;
                        NetUtils.DebugWrite(textColor, "[SEND] RLB {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else if (packet.property == PacketProperty.ReliableInOrder)
                    {
                        _pendingAckReliableInOrderPackets[packet.sequence % _windowSize] = packet;
                        NetUtils.DebugWrite(textColor, "[SEND] RLB_INO {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else if (packet.property == PacketProperty.InOrder)
                    {
                        NetUtils.DebugWrite(textColor, "[SEND] INO {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else
                    {
                        NetUtils.DebugWrite(textColor, "[SEND] simple {0} to {1}", packet.sequence, _remoteEndPoint);
                    }

                    packet.timeStamp = Environment.TickCount;
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
                NetUtils.DebugWrite(textColor, "[UPDATE]Reset flow timer, _sendedPackets - {0}", _sendedPacketsCount);
                _sendedPacketsCount = 0;
                _flowTimer = 0;
            }

            //ResetPingTimer
            _pingUpdateTimer += deltaTime;
            if (_pingUpdateTimer >= _pingUpdateDelay)
            {
                _pingUpdateTimer = 0;
                //_pingStopwatch.Reset();
                NetPacket packet = new NetPacket();
                packet.property = PacketProperty.Ping;
                _pingStopwatch.Start();
                _socket.SendTo(packet, _remoteEndPoint);
            }
        }
    }
}
