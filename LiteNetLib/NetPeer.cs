using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiteNetLib
{
    public sealed class NetPeer
    {
        private enum FlowMode
        {
            Bad,
            Good
        }

        //Flow control
        private FlowMode _currentFlowMode;
        private int _sendedPacketsCount;
        private int _flowTimer;

        private readonly NetSocket _socket;              //Udp socket
        private readonly Queue<NetPacket> _outgoingQueue;//Queue for sending packets
        private readonly Stack<NetPacket> _packetPool;   //Pool for packets

        private int _rtt;                                //round trip time
        private int _avgRtt;
        private int _rttCount;
        private int _badRoundTripTime;
        private int _goodRttCount;
        private int _ping;
        private ushort _pingSequence;
        private ushort _remotePingSequence;
        private ushort _pongSequence;

        private int _pingSendDelay;
        private int _pingSendTimer;
        private const int RttResetDelay = 1000;
        private int _rttResetTimer;

        private const int FlowUpdateTime = 100;
        private const int ThrottleIncreaseThreshold = 32;

        private readonly int[] _flowModes;
        private readonly Stopwatch _pingStopwatch;
        private readonly NetEndPoint _remoteEndPoint;
        private readonly ReliableChannel _reliableOrderedChannel;
        private readonly ReliableChannel _reliableUnorderedChannel;
        private readonly SequencedChannel _sequencedChannel;
        private readonly long _id;
        private readonly NetBase _peerListener;

        //DEBUG
        internal ConsoleColor DebugTextColor = ConsoleColor.DarkGreen;

        public NetEndPoint EndPoint
        {
            get { return _remoteEndPoint; }
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

        public long Id
        {
            get { return _id; }
        }

        internal NetPeer(NetBase peerListener, NetSocket socket, NetEndPoint remoteEndPoint)
        {
            _id = remoteEndPoint.GetId();
            _peerListener = peerListener;
            
            _socket = socket;
            _remoteEndPoint = remoteEndPoint;

            _outgoingQueue = new Queue<NetPacket>();
            _flowModes = new int[2];
            _flowModes[0] = 64 / 4; //bad
            _flowModes[1] = 64;     //good

            _avgRtt = 0;
            _rtt = 0;
            _badRoundTripTime = 650;
            _pingSendDelay = 1000;
            _pingSendTimer = 0;

            _pingStopwatch = new Stopwatch();

            _reliableOrderedChannel = new ReliableChannel(this, true);
            _reliableUnorderedChannel = new ReliableChannel(this, false);
            _sequencedChannel = new SequencedChannel(this);

            _packetPool = new Stack<NetPacket>();
        }

        public void Send(byte[] data, SendOptions options)
        {
            NetPacket packet = GetOrCreatePacket();
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

        internal void Send(PacketProperty property, int sequence = 0)
        {
            NetPacket packet = GetOrCreatePacket(property);
            packet.Property = property;
            packet.Sequence = 0;
            SendPacket(packet);
        }

        //from user thread, our thread, or recv?
        private void SendPacket(NetPacket packet)
        {
            switch (packet.Property)
            {
                case PacketProperty.Reliable:
                    DebugWrite("[RS]Packet reliable");
                    _reliableUnorderedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.Sequenced:
                    DebugWrite("[RS]Packet sequenced");
                    _sequencedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.ReliableOrdered:
                    DebugWrite("[RS]Packet reliable ordered");
                    _reliableOrderedChannel.AddToQueue(packet);
                    break;
                case PacketProperty.AckReliable:
                case PacketProperty.AckReliableOrdered:
                case PacketProperty.None:
                    DebugWrite("[RS]Packet simple");
                    lock (_outgoingQueue)
                    {
                        _outgoingQueue.Enqueue(packet);
                    }
                    break;
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.Connect:
                case PacketProperty.Disconnect:
                    if (_socket.SendTo(packet.ToByteArray(), _remoteEndPoint) == -1)
                    {
                        lock (_peerListener)
                        {
                            _peerListener.ProcessSendError(_remoteEndPoint);
                        }
                    }
                    break;
                default:
                    throw new Exception("Unknown packet property: " + packet.Property);
            }
        }

        internal void UpdateRoundTripTime(int roundTripTime)
        {
            //Calc average round trip time
            _rtt += roundTripTime;
            _rttCount++;
            _avgRtt = _rtt/_rttCount;

            if (_avgRtt < _badRoundTripTime)
            {
                _goodRttCount++;
                if (_goodRttCount > ThrottleIncreaseThreshold && _currentFlowMode != FlowMode.Good)
                {
                    _goodRttCount = 0;
                    DebugWrite("[PA]Enabled good flow mode, RTT: {0}", _avgRtt);
                    _currentFlowMode = FlowMode.Good;
                }
            }
            else
            {
                _goodRttCount = 0;
                if (_currentFlowMode != FlowMode.Bad)
                {
                    DebugWrite("[PA]Enabled bad flow mode, RTT: {0}", _avgRtt);
                    _currentFlowMode = FlowMode.Bad;
                }
            }
        }

        [Conditional("DEBUG_MESSAGES")]
        internal void DebugWrite(string str, params object[] args)
        {
            NetUtils.DebugWrite(DebugTextColor, str, args);
        }

        [Conditional("DEBUG_MESSAGES"), Conditional("DEBUG")]
        internal void DebugWriteForce(string str, params object[] args)
        {
            NetUtils.DebugWriteForce(DebugTextColor, str, args);
        }

        internal NetPacket GetOrCreatePacket(PacketProperty property = PacketProperty.None)
        {
            NetPacket packet;
            if (_packetPool.Count > 0)
            {
                lock (_packetPool)
                {
                    packet = _packetPool.Pop();
                }
            }
            else
            {
                packet = new NetPacket();
            }
            packet.Property = property;
            return packet;
        }

        internal void Recycle(NetPacket packet)
        {
            lock (_packetPool)
            {
                packet.Data = null;
                _packetPool.Push(packet);
            }
        }

        internal void AddIncomingPacket(NetPacket packet)
        {
            _peerListener.ReceiveFromPeer(packet, _remoteEndPoint);
            Recycle(packet);
        }

        //Process incoming packet
        internal void ProcessPacket(NetPacket packet)
        {
            DebugWrite("[RR]PacketProperty: {0}", packet.Property);
            switch (packet.Property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    if (NetUtils.RelativeSequenceNumber(packet.Sequence, _remotePingSequence) < 0)
                    {
                        break;
                    }
                    _remotePingSequence = packet.Sequence;
                    Recycle(packet);
                    Send(PacketProperty.Pong, _remotePingSequence);
                    break;

                //If we get pong, calculate ping time and rtt
                case PacketProperty.Pong:
                    if (NetUtils.RelativeSequenceNumber(packet.Sequence, _pongSequence) < 0)
                    {
                        break;
                    }
                    _pongSequence = packet.Sequence;
                    int rtt = (int) _pingStopwatch.ElapsedMilliseconds;
                    _pingStopwatch.Reset();
                    UpdateRoundTripTime(rtt);
                    DebugWrite("[PP]Ping: {0}", rtt);
                    Recycle(packet);
                    break;

                //Process ack
                case PacketProperty.AckReliable:
                    _reliableUnorderedChannel.ProcessAck(packet.Data);
                    Recycle(packet);
                    break;

                case PacketProperty.AckReliableOrdered:
                    _reliableOrderedChannel.ProcessAck(packet.Data);
                    Recycle(packet);
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
                case PacketProperty.Connect:
                case PacketProperty.Disconnect:
                    AddIncomingPacket(packet);
                    return;
            }
        }

        internal void Update(int deltaTime)
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
                    {
                        lock (_outgoingQueue)
                        {
                            packet = _outgoingQueue.Dequeue();
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                    
                if (_socket.SendTo(packet.ToByteArray(), _remoteEndPoint) == -1)
                {
                    lock (_peerListener)
                    {
                        _peerListener.ProcessSendError(_remoteEndPoint);
                    }
                    return;
                }
                if (packet.Property != PacketProperty.Reliable && 
                    packet.Property != PacketProperty.ReliableOrdered)
                {
                    Recycle(packet);
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
            _pingSendTimer += deltaTime;
            if (_pingSendTimer >= _pingSendDelay)
            {
                //reset timer
                _pingSendTimer = 0;

                //create packet
                NetPacket packet = GetOrCreatePacket(PacketProperty.Ping);
                packet.Sequence = _pingSequence;
                _pingSequence++;

                //send
                SendPacket(packet);
                Recycle(packet);

                //reset timer
                _pingStopwatch.Restart();
            }

            //reset rtt
            _rttResetTimer += deltaTime;
            if (_rttResetTimer >= RttResetDelay)
            {
                //Ping update
                _rtt = 0;
                _rttCount = 0;
                _ping = _avgRtt / 2;
            }
        }
    }
}
