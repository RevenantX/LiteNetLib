using System;
using System.Collections.Generic;

namespace LiteNetLib
{
    public class ReliableOrderedChannel : INetChannel
    {
        //For reliable inOrder
        private ushort _localReliableInOrderSequence;
        private ushort _remoteReliableInOrderSequence;

        private Queue<ushort> _ackReliableInOrderQueue; //Queue for acks (reliable in order packets)
        private NetPacket[] _pendingAckPackets; //Queue for unacked packets
        private bool[] _receivedPackets; //Queue for drop duplicates
 
        private int _windowStart;
        private int _windowSize;
        private NetPeer _peer;

        //Socket constructor
        public ReliableOrderedChannel(NetPeer peer)
        {
            _peer = peer;

            _ackReliableInOrderQueue = new Queue<ushort>();

            _pendingAckPackets = new NetPacket[_windowSize];
            _receivedPackets = new bool[_windowSize];

            _windowStart = 0;
            _localReliableInOrderSequence = 0;
            _remoteReliableInOrderSequence = 0;
        }

        //ProcessAck in packet
        public void ProcessAck(byte[] acksData)
        {
            int offset = 2;
            ushort reliableAcks = BitConverter.ToUInt16(acksData, 0);
            int reliableInOrderAcks = (acksData.Length - offset) / 2 - reliableAcks;

            NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Length: {0}\n[PA]RLB Acks: {1}\n[PA]RLB_INO Acks: {2}", acksData.Length, reliableAcks, reliableInOrderAcks);

            for (ushort i = 0; i < reliableAcks; i++)
            {
                ushort ack = BitConverter.ToUInt16(acksData, offset);
                int storeIdx = ack % _windowSize;

                if (_pendingAckPackets[storeIdx] != null)
                {
                    //Ack received
                    NetPacket removed = _pendingAckPackets[storeIdx];
                    _pendingAckPackets[storeIdx] = null;
                    _peer.UpdateFlowMode(removed.timeStamp);
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliable ack: {0} - true", ack);
                }
                else
                {
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliable ack: {0} - false", ack);
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
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliableInOrder ack: {0} - true", ack);
                }
                else
                {
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PA]Removing reliableInOrder ack: {0} - false", ack);
                }

                offset += 2;
            }
        }

        public void AddToQueue(NetPacket packet)
        {
            
        }

        private void SendPacket(NetPacket packet)
        {
            _localReliableInOrderSequence++;
            NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RS]Packet RLB_INO, localReliableInOrderSequence increased: {0}", _localReliableInOrderSequence);

        }

        //Process incoming packet
        public bool ProcessPacket(NetPacket packet)
        {
            NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]PacketProperty: {0}", packet.property);
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
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[PP]Ping: {0}", _ping);
                    return false;

                //Process ack
                case PacketProperty.Ack:
                    ProcessAck(packet.data);
                    return false;

                //Process in order packets
                case PacketProperty.Sequenced:
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
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Duplicate: {0}", packet.sequence);
                    return false;
                }

                //Setting remote sequence
                if (SequenceMoreRecent(packet.sequence, _remoteSequence))
                {
                    _remoteSequence = packet.sequence;
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Set remoteSequence to: {0}", _remoteSequence);
                }

                //Adding to received queue
                NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Add to received Queue");
                _receivedPackets[packet.sequence % _windowSize] = true;

                return true;
            }
            else
            {
                if(!SequenceMoreRecent(packet.sequence, _remoteReliableInOrderSequence))
                {
                    //Too old packet
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]ReliableInOrder too old");
                    return false;
                }
                //Send ack
                _ackReliableInOrderQueue.Enqueue(packet.sequence);

                if (_remoteReliableInOrderSequence == packet.sequence - 1)
                {
                    //Got our packet!
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]ReliableInOrder packet succes");
                    _remoteReliableInOrderSequence = packet.sequence;
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]Set ReliableInOrder remoteSequence to: {0}", _remoteReliableInOrderSequence);

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
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]ReliableInOrder duplicate, dropped");
                    }
                    else
                    {
                        //Too new packet
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RR]ReliableInOrder too new, placed to queue");
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

            NetUtils.DebugWrite(NetPeer.DebugTextColor, "[UPDATE]Delta: {0}ms, MaxSend: {1}", deltaTime, currentMaxSend);

            if (currentMaxSend > 0)
            {
                //Pending ack
                for(int i = 0; i < _pendingAckPackets.Length; i++)
                {
                    var packet = _pendingAckPackets[i];
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RESEND] RLB {0} to {1}", packet.sequence, _remoteEndPoint);
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
                    NetUtils.DebugWrite(NetPeer.DebugTextColor, "[RESEND] RLB_INO {0} to {1}", packet.sequence, _remoteEndPoint);
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
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[SEND] RLB {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else if (packet.property == PacketProperty.ReliableInOrder)
                    {
                        _pendingAckReliableInOrderPackets[packet.sequence % _windowSize] = packet;
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[SEND] RLB_INO {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else if (packet.property == PacketProperty.Sequenced)
                    {
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[SEND] INO {0} to {1}", packet.sequence, _remoteEndPoint);
                    }
                    else
                    {
                        NetUtils.DebugWrite(NetPeer.DebugTextColor, "[SEND] simple {0} to {1}", packet.sequence, _remoteEndPoint);
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
