using System.Runtime.InteropServices;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib.Utils;

namespace LiteNetLib
{
    public partial class NetManager
    {
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private IPEndPoint _bufferEndPointv4;
        private IPEndPoint _bufferEndPointv6;
#if UNITY_2018_3_OR_NEWER
        private PausedSocketFix _pausedSocketFix;
#endif

#if NET8_0_OR_GREATER
        private readonly SocketAddress _sockAddrCacheV4 = new SocketAddress(AddressFamily.InterNetwork);
        private readonly SocketAddress _sockAddrCacheV6 = new SocketAddress(AddressFamily.InterNetworkV6);
#endif

        private const int SioUdpConnreset = -1744830452; //SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12
        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("ff02::1");
        public static readonly bool IPv6Support;

        /// <summary>
        /// Maximum packets count that will be processed in Manual PollEvents
        /// </summary>
        public int MaxPacketsReceivePerUpdate = 0;

        // special case in iOS (and possibly android that should be resolved in unity)
        internal bool NotConnected;

        public short Ttl
        {
            get
            {
#if UNITY_SWITCH
                return 0;
#else
                return _udpSocketv4.Ttl;
#endif
            }
            internal set
            {
#if !UNITY_SWITCH
                _udpSocketv4.Ttl = value;
#endif
            }
        }

        static NetManager()
        {
#if DISABLE_IPV6
            IPv6Support = false;
#elif !UNITY_2019_1_OR_NEWER && !UNITY_2018_4_OR_NEWER && (!UNITY_EDITOR && ENABLE_IL2CPP)
            string version = UnityEngine.Application.unityVersion;
            IPv6Support = Socket.OSSupportsIPv6 && int.Parse(version.Remove(version.IndexOf('f')).Split('.')[2]) >= 6;
#else
            IPv6Support = Socket.OSSupportsIPv6;
#endif
        }

        private bool ProcessError(SocketError ex)
        {
            switch (ex)
            {
                case SocketError.NotConnected:
                    NotConnected = true;
                    return true;
                case SocketError.Interrupted:
                case SocketError.NotSocket:
                case SocketError.OperationAborted:
                    return true;
                case SocketError.ConnectionReset:
                case SocketError.MessageSize:
                case SocketError.TimedOut:
                case SocketError.NetworkReset:
                case SocketError.WouldBlock:
                    //NetDebug.Write($"[R]Ignored error: {(int)ex.SocketErrorCode} - {ex}");
                    break;
                default:
                    NetDebug.WriteError($"[R]Error code: {(int)ex}");
                    CreateEvent(NetEvent.EType.Error, errorCode: ex);
                    break;
            }
            return false;
        }

        private void ManualReceive(Socket socket, EndPoint bufferEndPoint)
        {
            //Reading data
            try
            {
                int packetsReceived = 0;
                while (socket.Available > 0)
                {
                    //TODO
                    //ReceiveFrom(socket, ref bufferEndPoint);
                    packetsReceived++;
                    if (packetsReceived == MaxPacketsReceivePerUpdate)
                        break;
                }
            }
            catch (SocketException ex)
            {
                ProcessError(ex.SocketErrorCode);
            }
            catch (ObjectDisposedException)
            {

            }
            catch (Exception e)
            {
                //protects socket receive thread
                NetDebug.WriteError("[NM] SocketReceiveThread error: " + e );
            }
        }

         /// <summary>
        /// Start logic thread and listening on selected port
        /// </summary>
        /// <param name="addressIPv4">bind to specific ipv4 address</param>
        /// <param name="addressIPv6">bind to specific ipv6 address</param>
        /// <param name="port">port to listen</param>
        /// <param name="manualMode">mode of library</param>
        public bool Start(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool manualMode)
        {
            if (IsRunning && NotConnected == false)
                return false;

            NotConnected = false;
            _manualMode = manualMode;
            _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (!BindSocket(_udpSocketv4, new IPEndPoint(addressIPv4, port)))
                return false;

            LocalPort = ((IPEndPoint) _udpSocketv4.LocalEndPoint).Port;

#if UNITY_2018_3_OR_NEWER
            if (_pausedSocketFix == null)
                _pausedSocketFix = new PausedSocketFix(this, addressIPv4, addressIPv6, port, manualMode);
#endif

            IsRunning = true;
            _bufferEndPointv4 = new IPEndPoint(IPAddress.Any, 0);

            //Check IPv6 support
            if (IPv6Support && IPv6Enabled)
            {
                _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                //Use one port for two sockets
                if (BindSocket(_udpSocketv6, new IPEndPoint(addressIPv6, LocalPort)))
                {
                    _bufferEndPointv6 = new IPEndPoint(IPAddress.IPv6Any, 0);
                }
                else
                {
                    _udpSocketv6 = null;
                }
            }

            if (!manualMode)
            {
#if NET8_0_OR_GREATER
                Task.Run(() => ReceiveLogic(_udpSocketv4, _sockAddrCacheV4));
                if(_udpSocketv6 != null)
                    ReceiveLogic(_udpSocketv6, _sockAddrCacheV6);
#else
                var sockArgs = new SocketAsyncEventArgs { RemoteEndPoint = _bufferEndPointv4 };
                var bufferPacket = PoolGetPacket(NetConstants.MaxPacketSize);
                sockArgs.UserToken = bufferPacket;
                sockArgs.SetBuffer(bufferPacket.RawData, 0, bufferPacket.Size);
                sockArgs.Completed += ReceiveCompleted;
                _udpSocketv4.ReceiveFromAsync(sockArgs);
                if (_udpSocketv6 != null)
                {
                    sockArgs = new SocketAsyncEventArgs { RemoteEndPoint = _bufferEndPointv6, };
                    bufferPacket = PoolGetPacket(NetConstants.MaxPacketSize);
                    sockArgs.UserToken = bufferPacket;
                    sockArgs.SetBuffer(bufferPacket.RawData, 0, bufferPacket.Size);
                    sockArgs.Completed += ReceiveCompleted;
                    _udpSocketv6.ReceiveFromAsync(sockArgs);
                }
#endif
                if (_logicThread == null)
                {
                    _logicThread = new Thread(UpdateLogic) { Name = "LogicThread", IsBackground = true };
                    _logicThread.Start();
                }
            }

            return true;
        }

#if NET8_0_OR_GREATER
        private async void ReceiveLogic(Socket s, SocketAddress saddr)
        {
            try
            {
                var bufferPacket = PoolGetPacket(NetConstants.MaxPacketSize);
                bufferPacket.Size = await s.ReceiveFromAsync(
                    new Memory<byte>(bufferPacket.RawData, 0, NetConstants.MaxPacketSize),
                    SocketFlags.None,
                    saddr);
                OnMessageReceived(bufferPacket, TryGetPeer(saddr, out var peer) ? peer : (IPEndPoint)_bufferEndPointv4.Create(saddr));
            }
            catch (ObjectDisposedException)
            {
                return; //socket closed
            }
            catch (SocketException ex)
            {
                if (ProcessError(ex.SocketErrorCode))
                    return;
            }
            catch (Exception ex)
            {
                NetDebug.WriteError("[NM] SocketReceive error: " + ex);
            }
            ReceiveLogic(s, saddr);
        }
#else
        private void ReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            var packet = (NetPacket)e.UserToken;
            var socket = (Socket)sender;
            bool isPending = false;
            while (!isPending)
            {
                if (e.SocketError != SocketError.Success && ProcessError(e.SocketError))
                    return;
                packet.Size = e.BytesTransferred;
                try
                {
                    OnMessageReceived(packet, (IPEndPoint)e.RemoteEndPoint);
                }
                catch (Exception ex)
                {
                    NetDebug.WriteError("[NM] SocketReceive error: " + ex);
                }
                packet = PoolGetPacket(NetConstants.MaxPacketSize);
                e.UserToken = packet;
                e.SetBuffer(packet.RawData, 0, NetConstants.MaxPacketSize);
                try
                {
                    isPending = socket.ReceiveFromAsync(e);
                }
                catch (ObjectDisposedException)
                {
                    //socket closed
                    break;
                }
            };
        }
#endif
        private bool BindSocket(Socket socket, IPEndPoint ep)
        {
            //Setup socket
            socket.ReceiveTimeout = 500;
            socket.SendTimeout = 500;
            socket.ReceiveBufferSize = NetConstants.SocketBufferSize;
            socket.SendBufferSize = NetConstants.SocketBufferSize;
            socket.Blocking = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    socket.IOControl(SioUdpConnreset, new byte[] {0}, null);
                }
                catch
                {
                    //ignored
                }
            }

            try
            {
                socket.ExclusiveAddressUse = !ReuseAddress;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, ReuseAddress);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, DontRoute);
            }
            catch
            {
                //Unity with IL2CPP throws an exception here, it doesn't matter in most cases so just ignore it
            }
            if (ep.AddressFamily == AddressFamily.InterNetwork)
            {
                Ttl = NetConstants.SocketTTL;

                try { socket.EnableBroadcast = true; }
                catch (SocketException e)
                {
                    NetDebug.WriteError($"[B]Broadcast error: {e.SocketErrorCode}");
                }

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    try { socket.DontFragment = true; }
                    catch (SocketException e)
                    {
                        NetDebug.WriteError($"[B]DontFragment error: {e.SocketErrorCode}");
                    }
                }
            }
            //Bind
            try
            {
                socket.Bind(ep);
                NetDebug.Write(NetLogLevel.Trace, $"[B]Successfully binded to port: {((IPEndPoint)socket.LocalEndPoint).Port}, AF: {socket.AddressFamily}");

                //join multicast
                if (ep.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    try
                    {
#if !UNITY_2018_3_OR_NEWER
                        socket.SetSocketOption(
                            SocketOptionLevel.IPv6,
                            SocketOptionName.AddMembership,
                            new IPv6MulticastOption(MulticastAddressV6));
#endif
                    }
                    catch (Exception)
                    {
                        // Unity3d throws exception - ignored
                    }
                }
            }
            catch (SocketException bindException)
            {
                switch (bindException.SocketErrorCode)
                {
                    //IPv6 bind fix
                    case SocketError.AddressAlreadyInUse:
                        if (socket.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            try
                            {
                                //Set IPv6Only
                                socket.DualMode = false;
                                socket.Bind(ep);
                            }
                            catch (SocketException ex)
                            {
                                //because its fixed in 2018_3
                                NetDebug.WriteError($"[B]Bind exception: {ex}, errorCode: {ex.SocketErrorCode}");
                                return false;
                            }
                            return true;
                        }
                        break;
                    //hack for iOS (Unity3D)
                    case SocketError.AddressFamilyNotSupported:
                        return true;
                }
                NetDebug.WriteError($"[B]Bind exception: {bindException}, errorCode: {bindException.SocketErrorCode}");
                return false;
            }
            return true;
        }

        internal int SendRawAndRecycle(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            int result = SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
            PoolRecycle(packet);
            return result;
        }

        internal int SendRaw(NetPacket packet, IPEndPoint remoteEndPoint)
        {
            return SendRaw(packet.RawData, 0, packet.Size, remoteEndPoint);
        }

        internal int SendRaw(byte[] message, int start, int length, IPEndPoint remoteEndPoint)
        {
            if (!IsRunning)
                return 0;

            NetPacket expandedPacket = null;
            if (_extraPacketLayer != null)
            {
                expandedPacket = PoolGetPacket(length + _extraPacketLayer.ExtraPacketSizeForLayer);
                Buffer.BlockCopy(message, start, expandedPacket.RawData, 0, length);
                start = 0;
                _extraPacketLayer.ProcessOutBoundPacket(ref remoteEndPoint, ref expandedPacket.RawData, ref start, ref length);
                message = expandedPacket.RawData;
            }

            var socket = _udpSocketv4;
            if (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 && IPv6Support)
            {
                socket = _udpSocketv6;
                if (socket == null)
                    return 0;
            }

            int result;
            try
            {
#if NET8_0_OR_GREATER
                    result = socket.SendTo(new ReadOnlySpan<byte>(message, start, length), SocketFlags.None, remoteEndPoint.Serialize());
#elif NET6_0_OR_GREATER
                    result = socket.SendToAsync(new ReadOnlyMemory<byte>(message, start, length), SocketFlags.None, remoteEndPoint).Result;
#else
                    result = socket.SendTo(message, start, length, SocketFlags.None, remoteEndPoint);
#endif
            }
            catch (SocketException ex)
            {
                switch (ex.SocketErrorCode)
                {
                    case SocketError.NoBufferSpaceAvailable:
                    case SocketError.Interrupted:
                        return 0;
                    case SocketError.MessageSize:
                        NetDebug.Write(NetLogLevel.Trace, $"[SRD] 10040, datalen: {length}");
                        return 0;

                    case SocketError.HostUnreachable:
                    case SocketError.NetworkUnreachable:
                        if (DisconnectOnUnreachable && remoteEndPoint is NetPeer peer)
                        {
                            DisconnectPeerForce(
                                peer,
                                ex.SocketErrorCode == SocketError.HostUnreachable
                                    ? DisconnectReason.HostUnreachable
                                    : DisconnectReason.NetworkUnreachable,
                                ex.SocketErrorCode,
                                null);
                        }

                        CreateEvent(NetEvent.EType.Error, remoteEndPoint: remoteEndPoint, errorCode: ex.SocketErrorCode);
                        return -1;

                    case SocketError.Shutdown:
                        CreateEvent(NetEvent.EType.Error, remoteEndPoint: remoteEndPoint, errorCode: ex.SocketErrorCode);
                        return -1;

                    default:
                        NetDebug.WriteError($"[S] {ex}");
                        return -1;
                }
            }

            if(expandedPacket != null)
                PoolRecycle(expandedPacket);

            if (result <= 0)
                return 0;

            if (EnableStatistics)
            {
                Statistics.IncrementPacketsSent();
                Statistics.AddBytesSent(length);
            }

            return result;
        }

        public bool SendBroadcast(NetDataWriter writer, int port)
        {
            return SendBroadcast(writer.Data, 0, writer.Length, port);
        }

        public bool SendBroadcast(byte[] data, int port)
        {
            return SendBroadcast(data, 0, data.Length, port);
        }

        public bool SendBroadcast(byte[] data, int start, int length, int port)
        {
            if (!IsRunning)
                return false;

            NetPacket packet;
            if (_extraPacketLayer != null)
            {
                var headerSize = NetPacket.GetHeaderSize(PacketProperty.Broadcast);
                packet = PoolGetPacket(headerSize + length + _extraPacketLayer.ExtraPacketSizeForLayer);
                packet.Property = PacketProperty.Broadcast;
                Buffer.BlockCopy(data, start, packet.RawData, headerSize, length);
                var checksumComputeStart = 0;
                int preCrcLength = length + headerSize;
                IPEndPoint emptyEp = null;
                _extraPacketLayer.ProcessOutBoundPacket(ref emptyEp, ref packet.RawData, ref checksumComputeStart, ref preCrcLength);
            }
            else
            {
                packet = PoolGetWithData(PacketProperty.Broadcast, data, start, length);
            }

            bool broadcastSuccess = false;
            bool multicastSuccess = false;
            try
            {
                broadcastSuccess = _udpSocketv4.SendTo(
                    packet.RawData,
                    0,
                    packet.Size,
                    SocketFlags.None,
                    new IPEndPoint(IPAddress.Broadcast, port)) > 0;

                if (_udpSocketv6 != null)
                {
                    multicastSuccess = _udpSocketv6.SendTo(
                        packet.RawData,
                        0,
                        packet.Size,
                        SocketFlags.None,
                        new IPEndPoint(MulticastAddressV6, port)) > 0;
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.HostUnreachable)
                    return broadcastSuccess;
                NetDebug.WriteError($"[S][MCAST] {ex}");
                return broadcastSuccess;
            }
            catch (Exception ex)
            {
                NetDebug.WriteError($"[S][MCAST] {ex}");
                return broadcastSuccess;
            }
            finally
            {
                PoolRecycle(packet);
            }

            return broadcastSuccess || multicastSuccess;
        }

        private void CloseSocket()
        {
            IsRunning = false;
            _udpSocketv4?.Close();
            _udpSocketv6?.Close();
            _udpSocketv4 = null;
            _udpSocketv6 = null;

        }
    }
}
