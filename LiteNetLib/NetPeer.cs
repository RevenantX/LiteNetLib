#if DEBUG
#define STATS_ENABLED
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    /// <summary>
    /// Peer connection state
    /// </summary>
    [Flags]
    public enum ConnectionState : byte
    {
        Incoming          = 1 << 1,
        Outcoming         = 1 << 2,
        Connected         = 1 << 3,
        ShutdownRequested = 1 << 4,
        Disconnected      = 1 << 5,
        Any = Incoming | Outcoming | Connected | ShutdownRequested
    }

    internal enum ConnectRequestResult
    {
        None,
        P2PConnection, //when peer connecting
        Reconnection,  //when peer was connected
        NewConnection  //when peer was disconnected
    }

    internal enum DisconnectResult
    {
        None,
        Reject,
        Disconnect
    }

    /// <summary>
    /// Network peer. Main purpose is sending messages to specific peer.
    /// </summary>
    public class NetPeer
    {
        //Ping and RTT
        private int _rtt;
        private int _avgRtt;
        private int _rttCount;
        private double _resendDelay = 27.0;
        private int _pingSendTimer;
        private int _rttResetTimer;
        private readonly Stopwatch _pingTimer = new Stopwatch();
        private int _timeSinceLastPacket;
        private long _remoteDelta;

        //Common            
        private readonly IPEndPoint _remoteEndPoint;
        private readonly NetManager _netManager;
        private readonly NetPacketPool _packetPool;
        private readonly object _flushLock = new object();
        private readonly object _sendLock = new object();
        private readonly object _shutdownLock = new object();

        internal NetPeer NextPeer;
        internal NetPeer PrevPeer;

        internal byte ConnectionNum
        {
            get { return _connectNum; }
            private set
            {
                _connectNum = value;
                _mergeData.ConnectionNumber = value;
                _pingPacket.ConnectionNumber = value;
                _pongPacket.ConnectionNumber = value;
            }
        }
 
        //Channels
        private readonly SimpleChannel _unreliableChannel;
        private readonly BaseChannel[] _channels;
        private BaseChannel _headChannel;
        private readonly byte _channelsCount;

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
        private readonly NetPacket _mergeData;
        private int _mergePos;
        private int _mergeCount;

        //Connection
        private int _connectAttempts;
        private int _connectTimer;
        private long _connectTime;
        private byte _connectNum;
        private ConnectionState _connectionState;
        private NetPacket _shutdownPacket;
        private const int ShutdownDelay = 300;
        private int _shutdownTimer;
        private readonly NetPacket _pingPacket;
        private readonly NetPacket _pongPacket;
        private readonly NetPacket _connectRequestPacket;
        private NetPacket _connectAcceptPacket;

        /// <summary>
        /// Current connection state
        /// </summary>
        public ConnectionState ConnectionState { get { return _connectionState; } }

        /// <summary>
        /// Connection time for internal purposes
        /// </summary>
        internal long ConnectTime { get { return _connectTime; } }

        /// <summary>
        /// Peer id can be used as key in your dictionary of peers
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Peer ip address and port
        /// </summary>
        public IPEndPoint EndPoint { get { return _remoteEndPoint; } }

        /// <summary>
        /// Current ping in milliseconds
        /// </summary>
        public int Ping { get { return _avgRtt/2; } }

        /// <summary>
        /// Current MTU - Maximum Transfer Unit ( maximum udp packet size without fragmentation )
        /// </summary>
        public int Mtu { get { return _mtu; } }

        /// <summary>
        /// Delta with remote time in ticks (not accurate)
        /// positive - remote time > our time
        /// </summary>
        public long RemoteTimeDelta
        {
            get { return _remoteDelta; }
        }

        /// <summary>
        /// Remote UTC time (not accurate)
        /// </summary>
        public DateTime RemoteUtcTime
        {
            get { return new DateTime(DateTime.UtcNow.Ticks + _remoteDelta); }
        }

        /// <summary>
        /// Time since last packet received (including internal library packets)
        /// </summary>
        public int TimeSinceLastPacket { get { return _timeSinceLastPacket; } }

        /// <summary>
        /// Peer parent NetManager
        /// </summary>
        public NetManager NetManager { get { return _netManager; } }

        internal double ResendDelay { get { return _resendDelay; } }

        /// <summary>
        /// Application defined object containing data about the connection
        /// </summary>
        public object Tag;

        /// <summary>
        /// Statistics of peer connection
        /// </summary>
        public readonly NetStatistics Statistics;

        //incoming connection constructor
        internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id)
        {
            Id = id;
            Statistics = new NetStatistics();
            _packetPool = netManager.NetPacketPool;
            _netManager = netManager;
            _remoteEndPoint = remoteEndPoint;
            _connectionState = ConnectionState.Incoming;
            _mergeData = new NetPacket(PacketProperty.Merged, NetConstants.MaxPacketSize);
            _pongPacket = new NetPacket(PacketProperty.Pong, 0);
            _pingPacket = new NetPacket(PacketProperty.Ping, 0) {Sequence = 1};
           
            _unreliableChannel = new SimpleChannel(this);
            _headChannel = _unreliableChannel;
            _holdedFragments = new Dictionary<ushort, IncomingFragments>();
            
            _channelsCount = netManager.ChannelsCount;
            _channels = new BaseChannel[_channelsCount * 4];
        }

        private BaseChannel CreateChannel(byte idx)
        {
            BaseChannel newChannel = _channels[idx];
            if (newChannel != null)
                return newChannel;
            switch (NetConstants.ChannelIdToDeliveryMethod(idx, _channelsCount))
            {
                case DeliveryMethod.ReliableUnordered:
                    newChannel = new ReliableChannel(this, false, idx);
                    break;
                case DeliveryMethod.Sequenced:
                    newChannel = new SequencedChannel(this, false, idx);
                    break;
                case DeliveryMethod.ReliableOrdered:
                    newChannel = new ReliableChannel(this, true, idx);
                    break;
                case DeliveryMethod.ReliableSequenced:
                    newChannel = new SequencedChannel(this, true, idx);
                    break;
            }
            _channels[idx] = newChannel;
            newChannel.Next = _headChannel;
            _headChannel = newChannel;
            return newChannel;
        }

        //"Connect to" constructor
        internal NetPeer(NetManager netManager, IPEndPoint remoteEndPoint, int id, byte connectNum, NetDataWriter connectData) 
            : this(netManager, remoteEndPoint, id)
        {
            _connectTime = DateTime.UtcNow.Ticks;
            _connectionState = ConnectionState.Outcoming;
            ConnectionNum = connectNum;

            //Make initial packet
            _connectRequestPacket = NetConnectRequestPacket.Make(connectData, _connectTime);
            _connectRequestPacket.ConnectionNumber = connectNum;

            //Send request
            _netManager.SendRaw(_connectRequestPacket, _remoteEndPoint);

            NetDebug.Write(NetLogLevel.Trace, "[CC] ConnectId: {0}, ConnectNum: {1}", _connectTime, connectNum);
        }

        //"Accept" incoming constructor
        internal void Accept(long connectId, byte connectNum)
        {
            _connectTime = connectId;
            _connectionState = ConnectionState.Connected;
            ConnectionNum = connectNum;

            //Make initial packet
            _connectAcceptPacket = NetConnectAcceptPacket.Make(_connectTime, connectNum, false);
            //Send
            _netManager.SendRaw(_connectAcceptPacket, _remoteEndPoint);

            NetDebug.Write(NetLogLevel.Trace, "[CC] ConnectId: {0}", _connectTime);
        }

        internal bool ProcessConnectAccept(NetConnectAcceptPacket packet)
        {
            if (_connectionState != ConnectionState.Outcoming)
                return false;

            //check connection id
            if (packet.ConnectionId != _connectTime)
            {
                NetDebug.Write(NetLogLevel.Trace, "[NC] Invalid connectId: {0}", _connectTime);
                return false;
            }
            //check connect num
            ConnectionNum = packet.ConnectionNumber;

            NetDebug.Write(NetLogLevel.Trace, "[NC] Received connection accept");
            _timeSinceLastPacket = 0;
            _connectionState = ConnectionState.Connected;
            return true;
        }

        /// <summary>
        /// Gets maximum size of packet that will be not fragmented.
        /// </summary>
        /// <param name="options">Type of packet that you want send</param>
        /// <returns>size in bytes</returns>
        public int GetMaxSinglePacketSize(DeliveryMethod options)
        {
            return _mtu - NetPacket.GetHeaderSize(options == DeliveryMethod.Unreliable ? PacketProperty.Unreliable : PacketProperty.Channeled);
        }

        /// <summary>
        /// Send data to peer (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(byte[] data, DeliveryMethod options)
        {
            Send(data, 0, data.Length, 0, options);
        }

        /// <summary>
        /// Send data to peer (channel - 0)
        /// </summary>
        /// <param name="dataWriter">DataWriter with data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(NetDataWriter dataWriter, DeliveryMethod options)
        {
            Send(dataWriter.Data, 0, dataWriter.Length, 0, options);
        }

        /// <summary>
        /// Send data to peer (channel - 0)
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(byte[] data, int start, int length, DeliveryMethod options)
        {
            Send(data, start, length, 0, options);
        }

        /// <summary>
        /// Send data to peer
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(byte[] data, byte channelNumber, DeliveryMethod options)
        {
            Send(data, 0, data.Length, channelNumber, options);
        }

        /// <summary>
        /// Send data to peer
        /// </summary>
        /// <param name="dataWriter">DataWriter with data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(NetDataWriter dataWriter, byte channelNumber, DeliveryMethod options)
        {
            Send(dataWriter.Data, 0, dataWriter.Length, channelNumber, options);
        }

        /// <summary>
        /// Send data to peer
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="start">Start of data</param>
        /// <param name="length">Length of data</param>
        /// <param name="channelNumber">Number of channel (from 0 to channelsCount - 1)</param>
        /// <param name="options">Send options (reliable, unreliable, etc.)</param>
        /// <exception cref="TooBigPacketException">
        ///     If size exceeds maximum limit:<para/>
        ///     MTU - headerSize bytes for Unreliable<para/>
        ///     Fragment count exceeded ushort.MaxValue<para/>
        /// </exception>
        public void Send(byte[] data, int start, int length, byte channelNumber, DeliveryMethod options)
        {
            if (_connectionState == ConnectionState.ShutdownRequested ||
                _connectionState == ConnectionState.Disconnected)
                return;
            if (channelNumber >= _channels.Length)
                return;

            //Select channel
            PacketProperty property;
            BaseChannel channel;

            if (options == DeliveryMethod.Unreliable)
            {
                property = PacketProperty.Unreliable;
                channel = _unreliableChannel;
            }
            else
            {
                property = PacketProperty.Channeled;
                channel = CreateChannel(NetConstants.ChannelNumberToId(options, channelNumber, _channelsCount));
            }

            //Prepare  
            NetDebug.Write("[RS]Packet: " + property);

            //Check fragmentation
            int headerSize = NetPacket.GetHeaderSize(property);
            //Save mtu for multithread
            int mtu = _mtu;
            if (length + headerSize > mtu)
            {
                //if cannot be fragmented
                if (options != DeliveryMethod.ReliableOrdered && options != DeliveryMethod.ReliableUnordered)
                    throw new TooBigPacketException("Unreliable packet size exceeded maximum of " + (_mtu - headerSize) + " bytes");

                int packetFullSize = mtu - headerSize;
                int packetDataSize = packetFullSize - NetConstants.FragmentHeaderSize;

                int fullPacketsCount = length / packetDataSize;
                int lastPacketSize = length % packetDataSize;
                int totalPackets = fullPacketsCount + (lastPacketSize == 0 ? 0 : 1);

                NetDebug.Write("FragmentSend:\n" +
                           " MTU: {0}\n" +
                           " headerSize: {1}\n" +
                           " packetFullSize: {2}\n" +
                           " packetDataSize: {3}\n" +
                           " fullPacketsCount: {4}\n" +
                           " lastPacketSize: {5}\n" +
                           " totalPackets: {6}",
                    mtu, headerSize, packetFullSize, packetDataSize, fullPacketsCount, lastPacketSize, totalPackets);

                if (totalPackets > ushort.MaxValue)
                {
                    throw new TooBigPacketException("Data was split in " + totalPackets + " fragments, which exceeds " + ushort.MaxValue);
                }

                int dataOffset = headerSize + NetConstants.FragmentHeaderSize;

                lock (_sendLock)
                {
                    for (ushort i = 0; i < fullPacketsCount; i++)
                    {
                        NetPacket p = _packetPool.GetWithProperty(property, packetFullSize);
                        p.FragmentId = _fragmentId;
                        p.FragmentPart = i;
                        p.FragmentsTotal = (ushort)totalPackets;
                        p.MarkFragmented();
                        Buffer.BlockCopy(data, i * packetDataSize, p.RawData, dataOffset, packetDataSize);
                        channel.AddToQueue(p);
                    }
                    if (lastPacketSize > 0)
                    {
                        NetPacket p = _packetPool.GetWithProperty(property, lastPacketSize + NetConstants.FragmentHeaderSize);
                        p.FragmentId = _fragmentId;
                        p.FragmentPart = (ushort)fullPacketsCount; //last
                        p.FragmentsTotal = (ushort)totalPackets;
                        p.MarkFragmented();
                        Buffer.BlockCopy(data, fullPacketsCount * packetDataSize, p.RawData, dataOffset, lastPacketSize);
                        channel.AddToQueue(p);
                    }
                    _fragmentId++;
                }
                return;
            }

            //Else just send
            NetPacket packet = _packetPool.GetWithData(property, data, start, length);
            channel.AddToQueue(packet);
        }

        public void Disconnect(byte[] data)
        {
            _netManager.DisconnectPeer(this, data);
        }

        public void Disconnect(NetDataWriter writer)
        {
            _netManager.DisconnectPeer(this, writer);
        }

        public void Disconnect(byte[] data, int start, int count)
        {
            _netManager.DisconnectPeer(this, data, start, count);
        }

        public void Disconnect()
        {
            _netManager.DisconnectPeer(this);
        }

        internal DisconnectResult ProcessDisconnect(NetPacket packet)
        {
            if ((_connectionState == ConnectionState.Connected || _connectionState == ConnectionState.Outcoming) &&
                packet.Size >= 9 &&
                BitConverter.ToInt64(packet.RawData, 1) == _connectTime &&
                packet.ConnectionNumber == _connectNum)
            {
                return _connectionState == ConnectionState.Connected
                    ? DisconnectResult.Disconnect
                    : DisconnectResult.Reject;
            }
            return DisconnectResult.None;
        }

        internal void Reject(long connectionId, byte connectionNumber, byte[] data, int start, int length)
        {
            _connectTime = connectionId;
            _connectNum = connectionNumber;
            Shutdown(data, start, length, false);
        }

        internal bool Shutdown(byte[] data, int start, int length, bool force)
        {
            lock (_shutdownLock)
            {
                //trying to shutdown already disconnected
                if (_connectionState == ConnectionState.Disconnected ||
                    _connectionState == ConnectionState.ShutdownRequested)
                {
                    return false;
                }

                //don't send anything
                if (force)
                {
                    _connectionState = ConnectionState.Disconnected;
                    return true;
                }

                //reset time for reconnect protection
                _timeSinceLastPacket = 0;

                //send shutdown packet
                _shutdownPacket = new NetPacket(PacketProperty.Disconnect, length);
                _shutdownPacket.ConnectionNumber = _connectNum;
                FastBitConverter.GetBytes(_shutdownPacket.RawData, 1, _connectTime);
                if (_shutdownPacket.Size >= _mtu)
                {
                    //Drop additional data
                    NetDebug.WriteError("[Peer] Disconnect additional data size more than MTU - 8!");
                }
                else if (data != null && length > 0)
                {
                    Buffer.BlockCopy(data, start, _shutdownPacket.RawData, 9, length);
                }
                _connectionState = ConnectionState.ShutdownRequested;
                NetDebug.Write("[Peer] Send disconnect");
                _netManager.SendRaw(_shutdownPacket, _remoteEndPoint);
                return true;
            }
        }

        private void UpdateRoundTripTime(int roundTripTime)
        {
            _rtt += roundTripTime;
            _rttCount++;
            _avgRtt = _rtt/_rttCount;
            _resendDelay = 25.0 + _avgRtt * 2.1; // 25 ms + double rtt
        }

        internal void AddIncomingPacket(NetPacket p)
        {
            if (p.IsFragmented)
            {
                NetDebug.Write("Fragment. Id: {0}, Part: {1}, Total: {2}", p.FragmentId, p.FragmentPart, p.FragmentsTotal);
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

                //Error check
                if (p.FragmentPart >= fragments.Length || fragments[p.FragmentPart] != null)
                {
                    _packetPool.Recycle(p);
                    NetDebug.WriteError("Invalid fragment packet");
                    return;
                }
                //Fill array
                fragments[p.FragmentPart] = p;

                //Increase received fragments count
                incomingFragments.ReceivedCount++;

                //Increase total size
                int dataOffset = p.GetHeaderSize() + NetConstants.FragmentHeaderSize;
                incomingFragments.TotalSize += p.Size - dataOffset;

                //Check for finish
                if (incomingFragments.ReceivedCount != fragments.Length)
                    return;

                NetDebug.Write("Received all fragments!");
                NetPacket resultingPacket = _packetPool.GetWithProperty( p.Property, incomingFragments.TotalSize );

                int resultingPacketOffset = resultingPacket.GetHeaderSize();
                int firstFragmentSize = fragments[0].Size - dataOffset;
                for (int i = 0; i < incomingFragments.ReceivedCount; i++)
                {
                    //Create resulting big packet
                    int fragmentSize = fragments[i].Size - dataOffset;
                    Buffer.BlockCopy(
                        fragments[i].RawData,
                        dataOffset,
                        resultingPacket.RawData,
                        resultingPacketOffset + firstFragmentSize * i,
                        fragmentSize);

                    //Free memory
                    _packetPool.Recycle(fragments[i]);
                    fragments[i] = null;
                }

                //Send to process
                _netManager.ReceiveFromPeer(resultingPacket, _remoteEndPoint);

                //Clear memory
                _holdedFragments.Remove(packetFragId);
            }
            else //Just simple packet
            {
                _netManager.ReceiveFromPeer(p, _remoteEndPoint);
            }
        }

        private void ProcessMtuPacket(NetPacket packet)
        {
            //header + int
            if (packet.Size < NetConstants.PossibleMtu[0])
                return;

            //first stage check (mtu check and mtu ok)
            int receivedMtu = BitConverter.ToInt32(packet.RawData, 1);
            int endMtuCheck = BitConverter.ToInt32(packet.RawData, packet.Size - 4);
            if (receivedMtu != packet.Size || receivedMtu != endMtuCheck || receivedMtu > NetConstants.MaxPacketSize)
            {
                NetDebug.WriteError("[MTU] Broken packet. RMTU {0}, EMTU {1}, PSIZE {2}", receivedMtu, endMtuCheck, packet.Size);
                return;
            }

            if (packet.Property == PacketProperty.MtuCheck)
            {
                _mtuCheckAttempts = 0;
                NetDebug.Write("[MTU] check. send back: " + receivedMtu);
                packet.Property = PacketProperty.MtuOk;
                _netManager.SendRawAndRecycle(packet, _remoteEndPoint);
            }
            else if(receivedMtu > _mtu && !_finishMtu) //MtuOk
            {
                //invalid packet
                if (receivedMtu != NetConstants.PossibleMtu[_mtuIdx + 1])
                    return;

                lock (_mtuMutex)
                {
                    _mtuIdx++;
                    _mtu = receivedMtu;
                }
                //if maxed - finish.
                if (_mtuIdx == NetConstants.PossibleMtu.Length - 1)
                    _finishMtu = true;

                NetDebug.Write("[MTU] ok. Increase to: " + _mtu);
            }
        }

        private void UpdateMtuLogic(int deltaTime)
        {
            if (_finishMtu)
                return;

            _mtuCheckTimer += deltaTime;
            if (_mtuCheckTimer < MtuCheckDelay)
                return;

            _mtuCheckTimer = 0;
            _mtuCheckAttempts++;
            if (_mtuCheckAttempts >= MaxMtuCheckAttempts)
            {
                _finishMtu = true;
                return;
            }

            lock (_mtuMutex)
            {
                if (_mtuIdx >= NetConstants.PossibleMtu.Length - 1)
                    return;

                //Send increased packet
                int newMtu = NetConstants.PossibleMtu[_mtuIdx + 1];
                var p = _packetPool.GetWithProperty(PacketProperty.MtuCheck, newMtu - NetConstants.HeaderSize);
                FastBitConverter.GetBytes(p.RawData, 1, newMtu);         //place into start
                FastBitConverter.GetBytes(p.RawData, p.Size - 4, newMtu);//and end of packet

                //Must check result for MTU fix
                if (_netManager.SendRawAndRecycle(p, _remoteEndPoint) <= 0)
                    _finishMtu = true;
            }
        }

        internal ConnectRequestResult ProcessConnectRequest(NetConnectRequestPacket connRequest)
        {
            //current or new request
            switch (_connectionState)
            {
                //P2P case or just ID update
                case ConnectionState.Outcoming:
                case ConnectionState.Incoming:
                    //change connect id if newer
                    if (connRequest.ConnectionTime >= _connectTime)
                    {
                        //Change connect id
                        _connectTime = connRequest.ConnectionTime;
                        ConnectionNum = connRequest.ConnectionNumber;
                    }
                    return _connectionState == ConnectionState.Outcoming 
                        ? ConnectRequestResult.P2PConnection 
                        : ConnectRequestResult.None;

                case ConnectionState.Connected:
                    //Old connect request
                    if (connRequest.ConnectionTime == _connectTime)
                    {
                        //just reply accept
                        _netManager.SendRaw(_connectAcceptPacket, _remoteEndPoint);
                    }
                    //New connect request
                    else if (connRequest.ConnectionTime > _connectTime)
                    {
                        return ConnectRequestResult.Reconnection;
                    }
                    break;

                case ConnectionState.Disconnected:
                case ConnectionState.ShutdownRequested:
                    if (connRequest.ConnectionTime >= _connectTime)
                    {
                        return ConnectRequestResult.NewConnection;
                    }
                    break;
            }
            return ConnectRequestResult.None;
        }

        //Process incoming packet
        internal void ProcessPacket(NetPacket packet)
        {
            //not initialized
            if (_connectionState == ConnectionState.Incoming)
            {
                _packetPool.Recycle(packet);
                return;
            }
            if (packet.ConnectionNumber != _connectNum && packet.Property != PacketProperty.ShutdownOk) //without connectionNum
            {
                NetDebug.Write(NetLogLevel.Trace, "[RR]Old packet");
                _packetPool.Recycle(packet);
                return;
            }
            _timeSinceLastPacket = 0;

            NetDebug.Write("[RR]PacketProperty: {0}", packet.Property);
            switch (packet.Property)
            {
                case PacketProperty.Merged:
                    int pos = NetConstants.HeaderSize;
                    while (pos < packet.Size)
                    {
                        ushort size = BitConverter.ToUInt16(packet.RawData, pos);
                        pos += 2;
                        NetPacket mergedPacket = _packetPool.GetPacket(size, false);
                        if (!mergedPacket.FromBytes(packet.RawData, pos, size))
                        {
                            _packetPool.Recycle(packet);
                            break;
                        }
                        pos += size;
                        ProcessPacket(mergedPacket);
                    }
                    break;
                //If we get ping, send pong
                case PacketProperty.Ping:
                    if (NetUtils.RelativeSequenceNumber(packet.Sequence, _pongPacket.Sequence) > 0)
                    {
                        NetDebug.Write("[PP]Ping receive, send pong");
                        FastBitConverter.GetBytes(_pongPacket.RawData, 3, DateTime.UtcNow.Ticks);
                        _pongPacket.Sequence = packet.Sequence;
                        _netManager.SendRaw(_pongPacket, _remoteEndPoint);
                    }
                    _packetPool.Recycle(packet);
                    break;

                //If we get pong, calculate ping time and rtt
                case PacketProperty.Pong:
                    if (packet.Sequence == _pingPacket.Sequence)
                    {
                        _pingTimer.Stop();
                        int elapsedMs = (int)_pingTimer.ElapsedMilliseconds;
                        _remoteDelta = BitConverter.ToInt64(packet.RawData, 3) + (elapsedMs * TimeSpan.TicksPerMillisecond ) / 2 - DateTime.UtcNow.Ticks;
                        UpdateRoundTripTime(elapsedMs);
                        _netManager.ConnectionLatencyUpdated(this, elapsedMs / 2);
                        NetDebug.Write("[PP]Ping: {0} - {1} - {2}", packet.Sequence, elapsedMs, _remoteDelta);
                    }
                    _packetPool.Recycle(packet);
                    break;

                case PacketProperty.Ack:
                case PacketProperty.Channeled:
                    if (packet.ChannelId > _channels.Length)
                    {
                        _packetPool.Recycle(packet);
                        break;
                    }
                    var channel = _channels[packet.ChannelId] ?? (packet.Property == PacketProperty.Ack ? null : CreateChannel(packet.ChannelId));
                    if (channel != null)
                    {
                        if (!channel.ProcessPacket(packet))
                            _packetPool.Recycle(packet);
                    }
                    break;

                //Simple packet without acks
                case PacketProperty.Unreliable:
                    AddIncomingPacket(packet);
                    return;

                case PacketProperty.MtuCheck:
                case PacketProperty.MtuOk:
                    ProcessMtuPacket(packet);
                    break;

                case PacketProperty.ShutdownOk:
                    if(_connectionState == ConnectionState.ShutdownRequested)
                        _connectionState = ConnectionState.Disconnected;
                    _packetPool.Recycle(packet);
                    break;            
                
                default:
                    NetDebug.WriteError("Error! Unexpected packet type: " + packet.Property);
                    break;
            }
        }

        private void SendMerged()
        {
            if (_mergeCount == 0)
                return;
            int bytesSent;
            if (_mergeCount > 1)
            {
                NetDebug.Write("[P]Send merged: " + _mergePos + ", count: " + _mergeCount);
                bytesSent = _netManager.SendRaw(_mergeData.RawData, 0, NetConstants.HeaderSize + _mergePos, _remoteEndPoint);
            }
            else
            {
                //Send without length information and merging
                bytesSent = _netManager.SendRaw(_mergeData.RawData, NetConstants.HeaderSize + 2, _mergePos - 2, _remoteEndPoint);
            }
#if STATS_ENABLED
            Statistics.PacketsSent++;
            Statistics.BytesSent += (ulong)bytesSent;
#endif
            _mergePos = 0;
            _mergeCount = 0;
        }

        internal void SendUserData(NetPacket packet)
        {
            packet.ConnectionNumber = _connectNum;
            int mergedPacketSize = NetConstants.HeaderSize + packet.Size + 2;
            const int sizeTreshold = 20;
            if (mergedPacketSize + sizeTreshold >= _mtu)
            {
                NetDebug.Write(NetLogLevel.Trace, "[P]SendingPacket: " + packet.Property);
                int bytesSent = _netManager.SendRaw(packet, _remoteEndPoint);
#if STATS_ENABLED
                Statistics.PacketsSent++;
                Statistics.BytesSent += (ulong)bytesSent;
#endif
                return;
            }
            if (_mergePos + mergedPacketSize > _mtu)
                SendMerged();

            FastBitConverter.GetBytes(_mergeData.RawData, _mergePos + NetConstants.HeaderSize, (ushort)packet.Size);
            Buffer.BlockCopy(packet.RawData, 0, _mergeData.RawData, _mergePos + NetConstants.HeaderSize + 2, packet.Size);
            _mergePos += packet.Size + 2;
            _mergeCount++;
            //DebugWriteForce("Merged: " + _mergePos + "/" + (_mtu - 2) + ", count: " + _mergeCount);
        }

        /// <summary>
        /// Flush all queued packets
        /// </summary>
        public void Flush()
        {
            if (_connectionState != ConnectionState.Connected)
                return;
            lock (_flushLock)
            {
                BaseChannel currentChannel = _headChannel;
                while (currentChannel != null)
                {
                    currentChannel.SendNextPackets();
                    currentChannel = currentChannel.Next;
                }
                SendMerged();
            }
        }

        internal void Update(int deltaTime)
        {
            _timeSinceLastPacket += deltaTime;
            switch (_connectionState)
            {
                case ConnectionState.Connected:
                    if (_timeSinceLastPacket > _netManager.DisconnectTimeout)
                    {
                        NetDebug.Write(
                            "[UPDATE] Disconnect by timeout: {0} > {1}",
                            _timeSinceLastPacket,
                            _netManager.DisconnectTimeout);
                        _netManager.DisconnectPeerForce(this, DisconnectReason.Timeout, 0, null);
                        return;
                    }
                    break;

                case ConnectionState.ShutdownRequested:
                    if (_timeSinceLastPacket > _netManager.DisconnectTimeout)
                    {
                        _connectionState = ConnectionState.Disconnected;
                    }
                    else
                    {
                        _shutdownTimer += deltaTime;
                        if (_shutdownTimer >= ShutdownDelay)
                        {
                            _shutdownTimer = 0;
                            _netManager.SendRaw(_shutdownPacket, _remoteEndPoint);
                        }
                    }
                    return;

                case ConnectionState.Outcoming:
                    _connectTimer += deltaTime;
                    if (_connectTimer > _netManager.ReconnectDelay)
                    {
                        _connectTimer = 0;
                        _connectAttempts++;
                        if (_connectAttempts > _netManager.MaxConnectAttempts)
                        {
                            _netManager.DisconnectPeerForce(this, DisconnectReason.ConnectionFailed, 0, null);
                            return;
                        }

                        //else send connect again
                        _netManager.SendRaw(_connectRequestPacket, _remoteEndPoint);
                    }
                    return;

                case ConnectionState.Disconnected:
                case ConnectionState.Incoming:
                    return;
            }

            //Send ping
            _pingSendTimer += deltaTime;
            if (_pingSendTimer >= _netManager.PingInterval)
            {
                NetDebug.Write("[PP] Send ping...");
                //reset timer
                _pingSendTimer = 0;
                //send ping
                _pingPacket.Sequence++;
                //ping timeout
                if (_pingTimer.IsRunning)
                    UpdateRoundTripTime((int)_pingTimer.ElapsedMilliseconds);
                _pingTimer.Reset();
                _pingTimer.Start();
                _netManager.SendRaw(_pingPacket, _remoteEndPoint);
            }

            //RTT - round trip time
            _rttResetTimer += deltaTime;
            if (_rttResetTimer >= _netManager.PingInterval * 3)
            {
                _rttResetTimer = 0;
                _rtt = _avgRtt;
                _rttCount = 1;
            }

            UpdateMtuLogic(deltaTime);

            //Pending send
            Flush();
        }

        //For channels
        internal void Recycle(NetPacket packet)
        {
            _packetPool.Recycle(packet);
        }
    }
}
