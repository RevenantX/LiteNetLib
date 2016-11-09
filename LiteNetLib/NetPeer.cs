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
        private int _ping;
        private int _rtt;
        private int _avgRtt;
        private int _rttCount;
        private int _goodRttCount;
        private ushort _pingSequence;
        private ushort _remotePingSequence;

        private int _pingInterval;
        private int _pingSendTimer;
        private const int RttResetDelay = 1000;
        private int _rttResetTimer;

        private DateTime _pingTimeStart;
        private DateTime _lastPacketReceivedStart;

        //Common            
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

        //MTU
        private int _mtu = NetConstants.PossibleMtu[0];
        private int _mtuIdx;
        private bool _finishMtu;
        private int _mtuCheckTimer;
        private int _mtuCheckAttempts;
        private const int MtuCheckDelay = 1000;
        private const int MaxMtuCheckAttempts = 4;
        private readonly object _mtuMutex = new object();

        //Fragment
        private class IncomingFragments
        {
            public NetPacket[] Fragments;
            public int ReceivedCount;
            public int TotalSize;
        }
        private ushort _fragmentId;
        private readonly Dictionary<ushort, IncomingFragments> _holdedFragments;

        //Merging
        private readonly NetPacket _mergeData = new NetPacket();
        private int _mergePos;
        private int _mergeCount;

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

        public int PingInterval
        {
            get { return _pingInterval; }
            set { _pingInterval = value; }
        }

        public int CurrentFlowMode
        {
            get { return _currentFlowMode; }
        }

        public long Id
        {
            get { return _id; }
        }

        public int Mtu
        {
            get { return _mtu; }
        }

        public int TimeSinceLastPacket
        {
            get { return (int)(DateTime.UtcNow - _lastPacketReceivedStart).TotalMilliseconds; }
        }

        public NetBase Handler
        {
            get { return _peerListener; }
        }

        public int PacketsCountInReliableQueue
        {
            get { return _reliableUnorderedChannel.PacketsInQueue; }
        }

        public int PacketsCountInReliableOrderedQueue
        {
            get { return _reliableOrderedChannel.PacketsInQueue; }
        }

        internal NetPeer(NetBase peerListener, NetEndPoint remoteEndPoint)
        {
            _peerListener = peerListener;
            _id = remoteEndPoint.GetId();
            _remoteEndPoint = remoteEndPoint;

            _avgRtt = 0;
            _rtt = 0;
            _pingSendTimer = 0;

            _reliableOrderedChannel = new ReliableChannel(this, true, _windowSize);
            _reliableUnorderedChannel = new ReliableChannel(this, false, _windowSize);
            _sequencedChannel = new SequencedChannel(this);
            _simpleChannel = new SimpleChannel(this);

            _packetPool = new Stack<NetPacket>();
            _holdedFragments = new Dictionary<ushort, IncomingFragments>();

            _mergeData.Init(PacketProperty.Merged, NetConstants.PossibleMtu[NetConstants.PossibleMtu.Length - 1]);
        }

        private static PacketProperty SendOptionsToProperty(SendOptions options)
        {
            switch (options)
            {
                case SendOptions.ReliableUnordered:
                    return PacketProperty.Reliable;
                case SendOptions.Sequenced:
                    return PacketProperty.Sequenced;
                case SendOptions.ReliableOrdered:
                    return PacketProperty.ReliableOrdered;
                default:
                    return PacketProperty.Unreliable;
            }
        }

        public int GetMaxSinglePacketSize(SendOptions options)
        {
            return _mtu - NetPacket.GetHeaderSize(SendOptionsToProperty(options));
        }

        public void Send(byte[] data, SendOptions options)
        {
            Send(data, 0, data.Length, options);
        }

        public void Send(NetDataWriter dataWriter, SendOptions options)
        {
            Send(dataWriter.Data, 0, dataWriter.Length, options);
        }

        public void Send(byte[] data, int start, int length, SendOptions options)
        {
            //Prepare
            PacketProperty property = SendOptionsToProperty(options);
            int headerSize = NetPacket.GetHeaderSize(property);

            //Check fragmentation
            if (length + headerSize > _mtu)
            {
                if (options == SendOptions.Sequenced || options == SendOptions.Unreliable)
                {
                    throw new Exception("Unreliable packet size > allowed (" + (_mtu - headerSize) + ")");
                }
                
                int packetFullSize = _mtu - headerSize;
                int packetDataSize = packetFullSize - NetConstants.FragmentHeaderSize;

                int fullPacketsCount = length / packetDataSize;
                int lastPacketSize = length % packetDataSize;
                int totalPackets = fullPacketsCount + (lastPacketSize == 0 ? 0 : 1);

                DebugWrite("MTU: {0}, HDR: {1}, PFS: {2}, PDS: {3}, FPC: {4}, LPS: {5}, TP: {6}", 
                    _mtu, headerSize, packetFullSize, packetDataSize, fullPacketsCount, lastPacketSize, totalPackets);

                if (totalPackets > ushort.MaxValue)
                {
                    throw new Exception("Too many fragments: " + totalPackets + " > " + ushort.MaxValue);
                }

                for (ushort i = 0; i < fullPacketsCount; i++)
                {
                    NetPacket p = GetPacketFromPool(property, packetFullSize);
                    p.FragmentId = _fragmentId;
                    p.FragmentPart = i;
                    p.FragmentsTotal = (ushort)totalPackets;
                    p.IsFragmented = true;
                    p.PutData(data, i * packetDataSize, packetDataSize);
                    SendPacket(p);
                }
                
                if (lastPacketSize > 0)
                {
                    NetPacket p = GetPacketFromPool(property, lastPacketSize + NetConstants.FragmentHeaderSize);
                    p.FragmentId = _fragmentId;
                    p.FragmentPart = (ushort)fullPacketsCount; //last
                    p.FragmentsTotal = (ushort)totalPackets;
                    p.IsFragmented = true;
                    p.PutData(data, fullPacketsCount * packetDataSize, lastPacketSize);
                    SendPacket(p);
                }

                _fragmentId++;             
                return;
            }

            //Else just send
            NetPacket packet = GetPacketFromPool(property, length);
            packet.PutData(data, start, length);
            SendPacket(packet);
        }

        internal void CreateAndSend(PacketProperty property)
        {
            NetPacket packet = GetPacketFromPool(property);
            SendPacket(packet);
        }

        private void CreateAndSend(PacketProperty property, ushort sequence)
        {
            NetPacket packet = GetPacketFromPool(property);
            packet.Sequence = sequence;
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
                case PacketProperty.Unreliable:
                    DebugWrite("[RS]Packet simple");
                    _simpleChannel.AddToQueue(packet);
                    break;
                case PacketProperty.MtuCheck:
                    //Must check result for MTU fix
                    if (!_peerListener.SendRaw(packet.RawData, 0, packet.RawData.Length, _remoteEndPoint))
                    {
                        _finishMtu = true;
                    }
                    Recycle(packet);
                    break;
                case PacketProperty.AckReliable:
                case PacketProperty.AckReliableOrdered:
                case PacketProperty.Ping:
                case PacketProperty.Pong:
                case PacketProperty.Disconnect:
                case PacketProperty.MtuOk:
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

        internal NetPacket GetPacketFromPool(PacketProperty property = PacketProperty.Unreliable, int size=0, bool init=true)
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
            packet.RawData = null;
            lock (_packetPool)
            {
                _packetPool.Push(packet);
            }
        }

        internal void AddIncomingPacket(NetPacket p)
        {
            if (p.IsFragmented)
            {
                DebugWrite("Fragment. Id: {0}, Part: {1}, Total: {2}", p.FragmentId, p.FragmentPart, p.FragmentsTotal);
                //Get needed array from dictionary
                ushort packetFragId = p.FragmentId;
                IncomingFragments incomingFragments;
                if (!_holdedFragments.TryGetValue(packetFragId, out incomingFragments))
                {
                    incomingFragments = new IncomingFragments
                    {
                        Fragments = new NetPacket[p.FragmentsTotal]
                    };
                    _holdedFragments.Add(packetFragId, incomingFragments);
                }

                //Cache
                var fragments = incomingFragments.Fragments;

                //Fill array
                fragments[p.FragmentPart] = p;

                //Increase received fragments count
                incomingFragments.ReceivedCount++;

                //Increase total size
                int dataOffset = p.GetHeaderSize() + NetConstants.FragmentHeaderSize;
                incomingFragments.TotalSize += p.RawData.Length - dataOffset;

                //Check for finish
                if (incomingFragments.ReceivedCount != fragments.Length)
                    return;

                DebugWrite("Received all fragments!");
                NetPacket resultingPacket = GetPacketFromPool(p.Property, incomingFragments.TotalSize);
                int resultingPacketOffset = resultingPacket.GetHeaderSize();
                int firstFragmentSize = fragments[0].RawData.Length - dataOffset;
                for (int i = 0; i < incomingFragments.ReceivedCount; i++)
                {
                    //Create resulting big packet
                    int fragmentSize = fragments[i].RawData.Length - dataOffset;
                    Buffer.BlockCopy(
                        fragments[i].RawData,
                        dataOffset,
                        resultingPacket.RawData,
                        resultingPacketOffset + firstFragmentSize * i,
                        fragmentSize);

                    //Free memory
                    Recycle(fragments[i]);
                    fragments[i] = null;
                }

                //Send to process
                _peerListener.ReceiveFromPeer(resultingPacket, _remoteEndPoint);

                //Clear memory
                Recycle(resultingPacket);
                _holdedFragments.Remove(packetFragId);
            }
            else //Just simple packet
            {
                _peerListener.ReceiveFromPeer(p, _remoteEndPoint);
                Recycle(p);
            }
        }

        private void ProcessMtuPacket(NetPacket packet)
        {
            if (packet.RawData.Length == 1 || 
                packet.RawData[1] >= NetConstants.PossibleMtu.Length)
                return;

            //MTU auto increase
            if (packet.Property == PacketProperty.MtuCheck)
            {
                if (packet.RawData.Length != NetConstants.PossibleMtu[packet.RawData[1]])
                {
                    return;
                }
                _mtuCheckAttempts = 0;
                DebugWrite("MTU check. Resend: " + packet.RawData[1]);
                var mtuOkPacket = GetPacketFromPool(PacketProperty.MtuOk, 1);
                mtuOkPacket.RawData[1] = packet.RawData[1];
                SendPacket(mtuOkPacket);
            }
            else if(packet.RawData[1] > _mtuIdx) //MtuOk
            {
                lock (_mtuMutex)
                {
                    _mtuIdx = packet.RawData[1];
                    _mtu = NetConstants.PossibleMtu[_mtuIdx];
                }
                //if maxed - finish.
                if (_mtuIdx == NetConstants.PossibleMtu.Length - 1)
                {
                    _finishMtu = true;
                }
                DebugWrite("MTU ok. Increase to: " + _mtu);
            }
        }

        internal void StartConnectionTimer()
        {
            _lastPacketReceivedStart = DateTime.UtcNow;
        }

        //Process incoming packet
        internal void ProcessPacket(NetPacket packet)
        {
            _lastPacketReceivedStart = DateTime.UtcNow;

            DebugWrite("[RR]PacketProperty: {0}", packet.Property);
            switch (packet.Property)
            {
                case PacketProperty.Merged:
                    int pos = NetConstants.HeaderSize;
                    while (pos < packet.RawData.Length)
                    {
                        ushort size = BitConverter.ToUInt16(packet.RawData, pos);
                        pos += 2;
                        NetPacket mergedPacket = GetPacketFromPool(init: false);
                        if (!mergedPacket.FromBytes(packet.RawData, pos, size))
                        {
                            Recycle(packet);
                            break;
                        }
                        pos += size;
                        ProcessPacket(mergedPacket);
                    }
                    break;
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
                    int rtt = (int)(DateTime.UtcNow - _pingTimeStart).TotalMilliseconds;
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
                case PacketProperty.Unreliable:
                    AddIncomingPacket(packet);
                    return;

                case PacketProperty.MtuCheck:
                case PacketProperty.MtuOk:
                    ProcessMtuPacket(packet);
                    break;

                default:
                    DebugWriteForce("Error! Unexpected packet type: " + packet.Property);
                    break;
            }
        }

        internal void SendRawData(byte[] data)
        {
            //2 - merge byte + minimal packet size + datalen(ushort)
            if (_peerListener.MergeEnabled && _mergePos + data.Length + NetConstants.HeaderSize*2 + 2 < _mtu)
            {
                FastBitConverter.GetBytes(_mergeData.RawData, _mergePos + NetConstants.HeaderSize, (ushort)data.Length);
                Buffer.BlockCopy(data, 0, _mergeData.RawData, _mergePos + NetConstants.HeaderSize + 2, data.Length);
                _mergePos += data.Length + 2;
                _mergeCount++;

                //DebugWriteForce("Merged: " + _mergePos + "/" + (_mtu - 2) + ", count: " + _mergeCount);
            }
            else
            {
                _peerListener.SendRaw(data, 0, data.Length, _remoteEndPoint);
            }
        }

        internal void Update(int deltaTime)
        {
            //Get current flow mode
            int maxSendPacketsCount = _peerListener.GetPacketsPerSecond(_currentFlowMode);
            int currentMaxSend;

            if (maxSendPacketsCount > 0)
            {
                int availableSendPacketsCount = maxSendPacketsCount - _sendedPacketsCount;
                currentMaxSend = Math.Min(availableSendPacketsCount, (maxSendPacketsCount*deltaTime)/NetConstants.FlowUpdateTime);
            }
            else
            {
                currentMaxSend = int.MaxValue;
            }

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
            if (_pingSendTimer >= _pingInterval)
            {
                DebugWrite("[PP] Send ping...");

                //reset timer
                _pingSendTimer = 0;

                //send ping
                CreateAndSend(PacketProperty.Ping, _pingSequence);

                //reset timer
                _pingTimeStart = DateTime.UtcNow;
            }

            //RTT - round trip time
            _rttResetTimer += deltaTime;
            if (_rttResetTimer >= RttResetDelay)
            {
                _rttResetTimer = 0;
                //Rtt update
                _rtt = _avgRtt;
                _ping = _avgRtt;
                _peerListener.ConnectionLatencyUpdated(this, _ping);
                _rttCount = 1;
            }

            //MTU - Maximum transmission unit
            if (!_finishMtu)
            {
                _mtuCheckTimer += deltaTime;
                if (_mtuCheckTimer >= MtuCheckDelay)
                {
                    _mtuCheckTimer = 0;
                    _mtuCheckAttempts++;
                    if (_mtuCheckAttempts >= MaxMtuCheckAttempts)
                    {
                        _finishMtu = true;
                    }
                    else
                    {
                        lock (_mtuMutex)
                        {
                            //Send increased packet
                            if (_mtuIdx < NetConstants.PossibleMtu.Length - 1)
                            {
                                int newMtu = NetConstants.PossibleMtu[_mtuIdx + 1] - NetConstants.HeaderSize;
                                var p = GetPacketFromPool(PacketProperty.MtuCheck, newMtu);
                                p.RawData[1] = (byte)(_mtuIdx + 1);
                                SendPacket(p);
                            }
                        }
                    }
                }
            }
            //MTU - end

            //Flush
            if (_mergePos > 0)
            {
                if (_mergeCount > 1)
                {
                    DebugWrite("Send merged: " + _mergePos + ", count: " + _mergeCount);
                    _peerListener.SendRaw(_mergeData.RawData, 0, NetConstants.HeaderSize + _mergePos, _remoteEndPoint);
                }
                else
                {
                    //Send without length information and merging
                    _peerListener.SendRaw(_mergeData.RawData, NetConstants.HeaderSize + 2, _mergePos - 2, _remoteEndPoint);
                }
                _mergePos = 0;
                _mergeCount = 0;
            }
        }
    }
}
