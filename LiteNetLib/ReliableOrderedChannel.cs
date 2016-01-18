using System;
using System.Collections.Generic;
using System.Net;
using System.Diagnostics;

namespace LiteNetLib
{
    public class ReliableOrderedChannel
    {
        private enum FlowMode
        {
            Bad,
            Good
        }

        public const int DefaultWindowSize = 64;
        public const ushort MaxSequence = ushort.MaxValue;
        public const ushort HalfMaxSequence = MaxSequence / 2;

        //Flow control
        private FlowMode _currentFlowMode;
        private int[] _flowModes;
        private int _sendedPacketsCount;
        private int _flowTimer;

        //For reliable packets
        private ushort _localSequence;   //Local packet number
        private ushort _remoteSequence;  //Last remote packet number

        //For in order packets
        private ushort _localInOrderSequence;
        private ushort _remoteInOrderSequence;

        //For reliable inOrder
        private ushort _localReliableInOrderSequence;
        private ushort _remoteReliableInOrderSequence;

        private NetSocket _socket;              //Udp socket
        private Queue<NetPacket> _sentQueue;    //Queue for sending packets
        private Queue<ushort> _ackReliableQueue;        //Queue for acks (reliable packets)
        private Queue<ushort> _ackReliableInOrderQueue; //Queue for acks (reliable in order packets)
        private NetPacket[] _pendingAckPackets; //Queue for unacked packets
        private NetPacket[] _pendingAckReliableInOrderPackets;
        private bool[] _receivedPackets; //Queue for drop duplicates
        private bool[] _receivedReliableInOrderPackets;
 
        private int _windowStart;
        private int _windowSize;

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

        public delegate void ReliableInOrderPacket(NetPacket packet, EndPoint remoteEndPoint);
        public delegate void SendError(EndPoint remoteEndPoint);
        public ReliableInOrderPacket OnReliableInOrderPacket;
        public SendError OnSendError;

        //Socket constructor
        public ReliableOrderedChannel(NetSocket socket, EndPoint remoteEndPoint)
        {
            _socket = socket;
            _remoteEndPoint = remoteEndPoint;

            _sentQueue = new Queue<NetPacket>();
            _ackReliableQueue = new Queue<ushort>();
            _ackReliableInOrderQueue = new Queue<ushort>();

            _windowSize = DefaultWindowSize;

            _pendingAckPackets = new NetPacket[_windowSize];
            _pendingAckReliableInOrderPackets = new NetPacket[_windowSize];
            _receivedPackets = new bool[_windowSize];
            _receivedReliableInOrderPackets = new bool[_windowSize];

            _flowModes = new int[2];
            _flowModes[0] = _windowSize / 4; //bad
            _flowModes[1] = _windowSize;     //good

            _windowStart = 0;
            _localSequence = 0;
            _remoteSequence = 0;
            _localInOrderSequence = 0;
            _remoteInOrderSequence = 0;
            _localReliableInOrderSequence = 0;
            _remoteReliableInOrderSequence = 0;
            _roundTripTime = 0;
            _maxReceivedTime = 0;
            _badRoundTripTime = 650;

            _ping = 0;
            _pingUpdateDelay = 3000;
            _pingUpdateTimer = 0;
            _pingStopwatch = new Stopwatch();
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

        //ProcessAck in packet
        private void ProcessAck(byte[] acksData)
        {
            int offset = 2;
            ushort reliableAcks = BitConverter.ToUInt16(acksData, 0);
            int reliableInOrderAcks = (acksData.Length - offset) / 2 - reliableAcks;

            NetUtils.DebugWrite(textColor, "[PA]Length: {0}\n[PA]RLB Acks: {1}\n[PA]RLB_INO Acks: {2}", acksData.Length, reliableAcks, reliableInOrderAcks);

            for (ushort i = 0; i < reliableAcks; i++)
            {
                ushort ack = BitConverter.ToUInt16(acksData, offset);
                int storeIdx = ack % _windowSize;

                if (_pendingAckPackets[storeIdx] != null)
                {
                    //Ack received
                    NetPacket removed = _pendingAckPackets[storeIdx];
                    _pendingAckPackets[storeIdx] = null;
                    UpdateFlowMode(removed.timeStamp);
                    NetUtils.DebugWrite(textColor, "[PA]Removing reliable ack: {0} - true", ack);
                }
                else
                {
                    NetUtils.DebugWrite(textColor, "[PA]Removing reliable ack: {0} - false", ack);
                }

                offset += 2;
            }
            for (int i = 0; i < reliableInOrderAcks; i++)
            {
                ushort ack = BitConverter.ToUInt16(acksData, offset);
                int storeIdx = ack % _windowSize;

                if (_pendingAckReliableInOrderPackets[storeIdx] != null)
                {
                    NetPacket removed = _pendingAckReliableInOrderPackets[storeIdx];
                    _pendingAckReliableInOrderPackets[storeIdx] = null;
                    UpdateFlowMode(removed.timeStamp);
                    NetUtils.DebugWrite(textColor, "[PA]Removing reliableInOrder ack: {0} - true", ack);
                }
                else
                {
                    NetUtils.DebugWrite(textColor, "[PA]Removing reliableInOrder ack: {0} - false", ack);
                }

                offset += 2;
            }
        }

        private void SendPacket(NetPacket packet)
        {
            switch (packet.property)
            {
                case PacketProperty.Reliable:
                    _localSequence++;
                    packet.sequence = _localSequence;
                    NetUtils.DebugWrite(textColor, "[RS]Packet RLB, localSequence increased: {0}", _localSequence);
                    break;
                case PacketProperty.InOrder:
                    _localInOrderSequence++;
                    packet.sequence = _localInOrderSequence;
                    NetUtils.DebugWrite(textColor, "[RS]Packet INO, localInOrderSequence increased: {0}", _localInOrderSequence);
                    break;
                case PacketProperty.ReliableInOrder:
                    _localReliableInOrderSequence++;
                    packet.sequence = _localReliableInOrderSequence;
                    NetUtils.DebugWrite(textColor, "[RS]Packet RLB_INO, localReliableInOrderSequence increased: {0}", _localReliableInOrderSequence);
                    break;
                default:
                    NetUtils.DebugWrite(textColor, "[RS]Packet simple");
                    break;
            }

            _sentQueue.Enqueue(packet);
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

        private static int RelativeSequenceNumber(int number, int expected)
        {
            return (number - expected + MaxSequence + HalfMaxSequence) % MaxSequence - HalfMaxSequence;
        }

        private static bool SequenceMoreRecent(uint s1, uint s2)
        {
            return (s1 > s2) && (s1 - s2 <= HalfMaxSequence) ||
                   (s2 > s1) && (s2 - s1 > HalfMaxSequence);
        }

        //Process incoming packet
        public bool ProcessPacket(NetPacket packet)
        {
            NetUtils.DebugWrite(textColor, "[RR]PacketProperty: {0}", packet.property);
            switch (packet.property)
            {
                //If we get ping, send pong
                case PacketProperty.Ping:
                    NetPacket outPacket = new NetPacket();
                    outPacket.property = PacketProperty.Pong;
                    _socket.SendTo(outPacket, _remoteEndPoint);
                    return false;

                //If we get pong, calculate ping time
                case PacketProperty.Pong:
                    _ping = (int)_pingStopwatch.ElapsedMilliseconds;
                    _pingStopwatch.Reset();
                    NetUtils.DebugWrite(textColor, "[PP]Ping: {0}", _ping);
                    return false;

                //Process ack
                case PacketProperty.Ack:
                    ProcessAck(packet.data);
                    return false;

                //Process in order packets
                case PacketProperty.InOrder:
                    if (SequenceMoreRecent(packet.sequence, _remoteInOrderSequence))
                    {
                        _remoteInOrderSequence = packet.sequence;
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                //Simple packet without acks
                case PacketProperty.None:
                    return true;
            }
            
            //Reliable and ReliableInOrder
            if (packet.property == PacketProperty.Reliable)
            {
                //Send ack
                _ackReliableQueue.Enqueue(packet.sequence);

                //Drop duplicate
                if (_receivedPackets[packet.sequence % _windowSize])
                {
                    NetUtils.DebugWrite(textColor, "[RR]Duplicate: {0}", packet.sequence);
                    return false;
                }

                //Setting remote sequence
                if (SequenceMoreRecent(packet.sequence, _remoteSequence))
                {
                    _remoteSequence = packet.sequence;
                    NetUtils.DebugWrite(textColor, "[RR]Set remoteSequence to: {0}", _remoteSequence);
                }

                //Adding to received queue
                NetUtils.DebugWrite(textColor, "[RR]Add to received Queue");
                _receivedPackets[packet.sequence % _windowSize] = true;

                return true;
            }
            else
            {
                if(!SequenceMoreRecent(packet.sequence, _remoteReliableInOrderSequence))
                {
                    //Too old packet
                    NetUtils.DebugWrite(textColor, "[RR]ReliableInOrder too old");
                    return false;
                }
                //Send ack
                _ackReliableInOrderQueue.Enqueue(packet.sequence);

                if (_remoteReliableInOrderSequence == packet.sequence - 1)
                {
                    //Got our packet!
                    NetUtils.DebugWrite(textColor, "[RR]ReliableInOrder packet succes");
                    _remoteReliableInOrderSequence = packet.sequence;
                    NetUtils.DebugWrite(textColor, "[RR]Set ReliableInOrder remoteSequence to: {0}", _remoteReliableInOrderSequence);

                    OnReliableInOrderPacket(packet, _remoteEndPoint);
                    while (_receivedReliableInOrderPackets.ContainsKey( (ushort)(_remoteReliableInOrderSequence + 1) ))
                    {
                        _remoteReliableInOrderSequence++;
                        OnReliableInOrderPacket(_receivedReliableInOrderPackets[_remoteReliableInOrderSequence], _remoteEndPoint);
                        _receivedReliableInOrderPackets.Remove(_remoteReliableInOrderSequence);
                    }

                    return false;
                }
                else
                {
                    if (_receivedReliableInOrderPackets.ContainsKey(packet.sequence))
                    {
                        NetUtils.DebugWrite(textColor, "[RR]ReliableInOrder duplicate, dropped");
                    }
                    else
                    {
                        //Too new packet
                        NetUtils.DebugWrite(textColor, "[RR]ReliableInOrder too new, placed to queue");
                        _receivedReliableInOrderPackets.Add(packet.sequence, packet);
                    }
                    return false;
                }
            }
        }

        private void SendAcks()
        {
            if (_ackReliableQueue.Count > 0 || _ackReliableInOrderQueue.Count > 0)
            {
                NetPacket packet = new NetPacket();

                byte[] acksData = new byte[(_ackReliableQueue.Count + _ackReliableInOrderQueue.Count) * 2 + 2];
                Buffer.BlockCopy(BitConverter.GetBytes(_ackReliableQueue.Count), 0, acksData, 0, 2);

                int offset = 2;

                while (_ackReliableQueue.Count > 0)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(_ackReliableQueue.Dequeue()), 0, acksData, offset, 2);
                    offset += 2;
                }
                while (_ackReliableInOrderQueue.Count > 0)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(_ackReliableInOrderQueue.Dequeue()), 0, acksData, offset, 2);
                    offset += 2;
                }

                packet.data = acksData;
                packet.property = PacketProperty.Ack;
                _socket.SendTo(packet, _remoteEndPoint);
            }
        }

        public void Update(int deltaTime)
        {
            SendAcks();

            int currentSended = 0;
            //Get current flow mode
            int maxSendPacketsCount = _flowModes[(int)_currentFlowMode];
            int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
            int currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount * deltaTime) / 1000);

            NetUtils.DebugWrite(textColor, "[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            if (currentMaxSend > 0)
            {
                //Pending ack
                for(int i = 0; i < _pendingAckPackets.Length; i++)
                {
                    var packet = _pendingAckPackets[i];
                    NetUtils.DebugWrite(textColor, "[RESEND] RLB {0} to {1}", packet.sequence, _remoteEndPoint);
                    _socket.SendTo(packet, _remoteEndPoint);
                    currentSended++;
                    if (currentSended == currentMaxSend)
                    {
                        break;
                    }
                }
                //Pending reliableInOrder ack
                for(int i = 0; i < _pendingAckReliableInOrderPackets.Length; i++)
                {
                    var packet = _pendingAckReliableInOrderPackets[i];
                    NetUtils.DebugWrite(textColor, "[RESEND] RLB_INO {0} to {1}", packet.sequence, _remoteEndPoint);
                    if (_socket.SendTo(packet, _remoteEndPoint) == -1)
                    {
                        OnSendError(_remoteEndPoint);
                        return;
                    }
                    currentSended++;
                    if (currentSended == currentMaxSend)
                    {
                        break;
                    }
                }

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
                        OnSendError(_remoteEndPoint);
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
