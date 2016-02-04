using System;
using System.Collections.Generic;
using System.Diagnostics;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public sealed class NetPeer
    {
        //Flow control
        private int _currentFlowMode;
        private int _sendedPacketsCount;                    
        private int _flowTimer;

        //Ping and RTT
        private int _rtt;                                //round trip time
        private int _avgRtt;
        private int _rttCount;
        private int _goodRttCount;
        private int _ping;
        private ushort _pingSequence;
        private ushort _remotePingSequence;

        private int _pingSendDelay;
        private int _pingSendTimer;
        private const int RttResetDelay = 1000;
        private int _rttResetTimer;

        private readonly Stopwatch _pingStopwatch;
        private readonly Stopwatch _lastPacketStopwatch;

        //Common
        private readonly NetSocket _socket;              
        private readonly Stack<NetPacket> _packetPool;
        private readonly NetEndPoint _remoteEndPoint;
        private readonly long _id;
        private readonly NetBase _peerListener;

        //Channels
        private readonly ReliableChannel _reliableOrderedChannel;
        private readonly ReliableChannel _reliableUnorderedChannel;
        private readonly SequencedChannel _sequencedChannel;
        private readonly SimpleChannel _simpleChannel;

        private int _windowSize = NetConstants.DefaultWindowSize;

        //DEBUG
        internal ConsoleColor DebugTextColor = ConsoleColor.DarkGreen;

        public NetEndPoint EndPoint
        {
            get { return _remoteEndPoint; }
        }

        public int Ping
        {
            get { return _ping; }
        }

        public int PingSendDelay
        {
            get { return _pingSendDelay; }
            set { _pingSendDelay = value; }
        }

        public int CurrentFlowMode
        {
            get { return _currentFlowMode; }
        }

        public long Id
        {
            get { return _id; }
        }

        public long TimeSinceLastPacket
        {
            get { return _lastPacketStopwatch.ElapsedMilliseconds; }
        }

        internal NetPeer(NetBase peerListener, NetSocket socket, NetEndPoint remoteEndPoint)
        {
            _id = remoteEndPoint.GetId();
            _peerListener = peerListener;
            
            _socket = socket;
            _remoteEndPoint = remoteEndPoint;

            _avgRtt = 0;
            _rtt = 0;
            _pingSendDelay = NetConstants.DefaultPingSendDelay;
            _pingSendTimer = 0;

            _pingStopwatch = new Stopwatch();
            _lastPacketStopwatch = new Stopwatch();
            _lastPacketStopwatch.Start();

            _reliableOrderedChannel = new ReliableChannel(this, true, _windowSize);
            _reliableUnorderedChannel = new ReliableChannel(this, false, _windowSize);
            _sequencedChannel = new SequencedChannel(this);
            _simpleChannel = new SimpleChannel(this);

            _packetPool = new Stack<NetPacket>();
        }

        public void Send(byte[] data, SendOptions options)
        {
            Send(data, data.Length, options);
        }

        public void Send(NetDataWriter dataWriter, SendOptions options)
        {
            Send(dataWriter.Data, dataWriter.Length, options);
        }

        public void Send(byte[] data, int length, SendOptions options)
        {
            NetPacket packet;
            switch (options)
            {
                case SendOptions.ReliableUnordered:
                    packet = GetPacketFromPool(PacketProperty.Reliable, length);
                    break;
                case SendOptions.Sequenced:
                    packet = GetPacketFromPool(PacketProperty.Sequenced, length);
                    break;
                case SendOptions.ReliableOrdered:
                    packet = GetPacketFromPool(PacketProperty.ReliableOrdered, length);
                    break;
                default:
                    packet = GetPacketFromPool(PacketProperty.None, length);
                    break;
            }
            packet.PutData(data, length);
            SendPacket(packet);
        }

        internal void CreateAndSend(PacketProperty property)
        {
            NetPacket packet = GetPacketFromPool(property);
            SendPacket(packet);
        }

        internal void CreateAndSend(PacketProperty property, ushort sequence)
        {
            NetPacket packet = GetPacketFromPool(property);
            packet.Sequence = sequence;
            SendPacket(packet);
        }

        internal void SendConnect(long id)
        {
            var connectPacket = GetPacketFromPool(PacketProperty.Connect, 8);
            FastBitConverter.GetBytes(connectPacket.RawData, 1, id);
            SendRawData(connectPacket.RawData);
        }

        internal void SendDisconnect(long id)
        {
            var disconnectPacket = GetPacketFromPool(PacketProperty.Disconnect, 8);
            FastBitConverter.GetBytes(disconnectPacket.RawData, 1, id);
            SendRawData(disconnectPacket.RawData);
        }

        internal static long GetConnectId(NetPacket connectPacket)
        {
            return BitConverter.ToInt64(connectPacket.RawData, 1);
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
                    _simpleChannel.AddToQueue(packet);
                    break;
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.Connect:
                case PacketProperty.Disconnect:
                    SendRawData(packet.RawData);
                    Recycle(packet);
                    break;
                default:
                    throw new Exception("Unknown packet property: " + packet.Property);
            }
        }

        private void UpdateRoundTripTime(int roundTripTime)
        {
            //Calc average round trip time
            _rtt += roundTripTime;
            _rttCount++;
            _avgRtt = _rtt/_rttCount;

            //flowmode 0 = fastest
            //flowmode max = lowest

            if (_avgRtt < _peerListener.GetStartRtt(_currentFlowMode - 1))
            {
                if (_currentFlowMode <= 0)
                {
                    //Already maxed
                    return;
                }

                _goodRttCount++;
                if (_goodRttCount > NetConstants.FlowIncreaseThreshold)
                {
                    _goodRttCount = 0;
                    _currentFlowMode--;

                    DebugWrite("[PA]Increased flow speed, RTT: {0}, PPS: {1}", _avgRtt, _peerListener.GetPacketsPerSecond(_currentFlowMode));
                }
            }
            else if(_avgRtt > _peerListener.GetStartRtt(_currentFlowMode))
            {
                _goodRttCount = 0;
                if (_currentFlowMode < _peerListener.GetMaxFlowMode())
                {
                    _currentFlowMode++;
                    DebugWrite("[PA]Decreased flow speed, RTT: {0}, PPS: {1}", _avgRtt, _peerListener.GetPacketsPerSecond(_currentFlowMode));
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

        internal NetPacket GetPacketFromPool(PacketProperty property = PacketProperty.None, int size=0, bool init=true)
        {
            NetPacket packet = null;
            lock (_packetPool)
            {
                if (_packetPool.Count > 0)
                {
                    packet = _packetPool.Pop();
                }
            }
            if(packet == null)
            {
                packet = new NetPacket();
            }
            if(init)
                packet.Init(property, size);
            return packet;
        }

        internal void Recycle(NetPacket packet)
        {
            lock (_packetPool)
            {
                packet.RawData = null;
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
            _lastPacketStopwatch.Reset();
            _lastPacketStopwatch.Start();

            DebugWrite("[RR]PacketProperty: {0}", packet.Property);
            switch (packet.Property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    if (NetUtils.RelativeSequenceNumber(packet.Sequence, _remotePingSequence) < 0)
                    {
                        Recycle(packet);
                        break;
                    }
                    DebugWrite("[PP]Ping receive, send pong");
                    _remotePingSequence = packet.Sequence;
                    Recycle(packet);

                    //send
                    CreateAndSend(PacketProperty.Pong, _remotePingSequence);
                    break;

                //If we get pong, calculate ping time and rtt
                case PacketProperty.Pong:
                    if (NetUtils.RelativeSequenceNumber(packet.Sequence, _pingSequence) < 0)
                    {
                        Recycle(packet);
                        break;
                    }
                    _pingSequence = packet.Sequence;
                    int rtt = (int) _pingStopwatch.ElapsedMilliseconds;
                    _pingStopwatch.Reset();
                    UpdateRoundTripTime(rtt);
                    DebugWrite("[PP]Ping: {0}", rtt);
                    Recycle(packet);
                    break;

                //Process ack
                case PacketProperty.AckReliable:
                    _reliableUnorderedChannel.ProcessAck(packet);
                    Recycle(packet);
                    break;

                case PacketProperty.AckReliableOrdered:
                    _reliableOrderedChannel.ProcessAck(packet);
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
                    AddIncomingPacket(packet);
                    return;

                default:
                    DebugWriteForce("Error! Unexpected packet type: " + packet.Property);
                    break;
            }
        }

        internal bool SendRawData(byte[] data)
        {
            int errorCode = 0;
            if (_socket.SendTo(data, _remoteEndPoint, ref errorCode) == -1)
            {
                lock (_peerListener)
                {
                    _peerListener.ProcessSendError(_remoteEndPoint, errorCode.ToString());
                }
                return false;
            }
            return true;
        }

        internal void Update(int deltaTime)
        {
            //Get current flow mode
            int maxSendPacketsCount = _peerListener.GetPacketsPerSecond(_currentFlowMode);
            int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
            int currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount*deltaTime) / NetConstants.FlowUpdateTime);

            DebugWrite("[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            //Pending acks
            _reliableOrderedChannel.SendAcks();
            _reliableUnorderedChannel.SendAcks();

            //Pending send
            int currentSended = 0;
            while (currentSended < currentMaxSend)
            {
                //Get one of packets
                if (_reliableOrderedChannel.SendNextPacket() ||
                    _reliableUnorderedChannel.SendNextPacket() ||
                    _sequencedChannel.SendNextPacket() ||
                    _simpleChannel.SendNextPacket())
                {
                    currentSended++;
                }
                else
                {
                    //no outgoing packets
                    break;
                }
            }

            //Increase counter
            _sendedPacketsCount += currentSended;

            //ResetFlowTimer
            _flowTimer += deltaTime;
            if (_flowTimer >= NetConstants.FlowUpdateTime)
            {
                DebugWrite("[UPDATE]Reset flow timer, _sendedPackets - {0}", _sendedPacketsCount);
                _sendedPacketsCount = 0;
                _flowTimer = 0;
            }

            //Send ping
            _pingSendTimer += deltaTime;
            if (_pingSendTimer >= _pingSendDelay)
            {
                DebugWrite("[PP] Send ping...");

                //reset timer
                _pingSendTimer = 0;

                //send ping
                CreateAndSend(PacketProperty.Ping, _pingSequence);

                //reset timer
                _pingStopwatch.Reset();
                _pingStopwatch.Start();
            }

            //reset rtt
            _rttResetTimer += deltaTime;
            if (_rttResetTimer >= RttResetDelay)
            {
                //Ping update
                _rtt = _avgRtt;
                _rttCount = 1;
                _ping = _avgRtt / 2;
            }
        }
    }
}
