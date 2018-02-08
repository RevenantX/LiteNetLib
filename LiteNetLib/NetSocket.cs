using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
#if WIN32 && UNSAFE
using System.Runtime.InteropServices;
#endif

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private Thread _threadv4;
        private Thread _threadv6;
        private bool _running;
        private readonly NetManager.OnMessageReceived _onMessageReceived;

#if WIN32 && UNSAFE
        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern unsafe int sendto(
            [In] IntPtr socketHandle, 
            [In] byte* pinnedBuffer,
            [In] int len, 
            [In] SocketFlags socketFlags, 
            [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int recvfrom(
            [In] IntPtr socketHandle, 
            [In] byte[] pinnedBuffer, 
            [In] int len, 
            [In] SocketFlags socketFlags, 
            [Out] byte[] socketAddress, 
            [In, Out] ref int socketAddressSize);

        internal struct TimeValue
        {
            public int Seconds;
            public int Microseconds;
        }
        private static TimeValue SendPollTime = new TimeValue { Microseconds = SocketSendPollTime };

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern int select(
            [In] int ignoredParameter, 
            [In, Out] IntPtr[] readfds, 
            [In, Out] IntPtr[] writefds, 
            [In, Out] IntPtr[] exceptfds, 
            [In] ref TimeValue timeout);
#endif

        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse (NetConstants.MulticastGroupIPv6);
        internal static readonly bool IPv6Support;
        private const int SocketReceivePollTime = 100000;
        private const int SocketSendPollTime = 1000;

        public int LocalPort { get; private set; }

        public short Ttl
        {
            get { return _udpSocketv4.Ttl; }
            set
            {
                _udpSocketv4.Ttl = value;
            }
        }

        static NetSocket()
        {
#if UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
            IPv6Support = Socket.SupportsIPv6;
#elif DISABLE_IPV6
            IPv6Support = false;
#else
            IPv6Support = Socket.OSSupportsIPv6;
#endif
        }

        public NetSocket(NetManager.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
        }

        private void ReceiveLogic(object state)
        {
            Socket socket = (Socket)state;
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            NetEndPoint bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
#if WIN32 && UNSAFE
            int saddrSize = 32;
            byte[] prevAddress = new byte[saddrSize];
            byte[] socketAddress = new byte[saddrSize];
            byte[] addrBuffer = new byte[16]; //IPAddress.IPv6AddressBytes
            var sockeHandle = socket.Handle;
            IntPtr[] fileDescriptorSet = { (IntPtr)1, sockeHandle };
            TimeValue time = new TimeValue {Microseconds = SocketReceivePollTime};
#endif
            byte[] receiveBuffer = new byte[NetConstants.PacketSizeLimit];

            while (_running)
            {
                int result;

                //Reading data
                try
                {
#if WIN32 && UNSAFE
                    fileDescriptorSet[0] = (IntPtr)1;
                    fileDescriptorSet[1] = sockeHandle;
                    int socketCount = select( 0, fileDescriptorSet, null, null, ref time);
                    if ((SocketError) socketCount == SocketError.SocketError)
                    {
                        throw new SocketException(Marshal.GetLastWin32Error());
                    }
                    if ((int)fileDescriptorSet[0] == 0 || fileDescriptorSet[1] != sockeHandle)
                    {
                        continue;
                    }

                    result = recvfrom(
                        sockeHandle,
                        receiveBuffer,
                        receiveBuffer.Length,
                        SocketFlags.None,
                        socketAddress,
                        ref saddrSize);
                    if ((SocketError) result == SocketError.SocketError)
                    {
                        throw new SocketException(Marshal.GetLastWin32Error());
                    }

                    bool recreate = false;
                    for(int i = 0; i < saddrSize; i++)
                    {
                        if(socketAddress[i] != prevAddress[i])
                        {
                            prevAddress[i] = socketAddress[i];
                            recreate = true;
                        }
                    }
                    if(recreate)
                    {
                        if (socket.AddressFamily == AddressFamily.InterNetwork)
                        {
                            int port = (socketAddress[2]<<8 & 0xFF00) | socketAddress[3]; 
                            long address = (
                                (socketAddress[4]     & 0x000000FF) |
                                (socketAddress[5]<<8  & 0x0000FF00) | 
                                (socketAddress[6]<<16 & 0x00FF0000) |
                                (socketAddress[7]<<24) 
                            ) & 0x00000000FFFFFFFF; 
                            bufferNetEndPoint = new NetEndPoint(new IPEndPoint(address, port));
                        }
                        else
                        {
                            for (int i = 0; i < addrBuffer.Length; i++)
                            { 
                                addrBuffer[i] = socketAddress[i + 8]; 
                            }
                            int port = (socketAddress[2]<<8 & 0xFF00) | (socketAddress[3]);
                            long scope = (socketAddress[27] << 24) + 
                                (socketAddress[26] << 16) +
                                (socketAddress[25] << 8 ) + 
                                (socketAddress[24]);
                            bufferNetEndPoint = new NetEndPoint(new IPEndPoint(new IPAddress(addrBuffer, scope), port));
                        }     
                    }
#else
                    if (!socket.Poll(SocketReceivePollTime, SelectMode.SelectRead))
                    {
                        continue;
                    }
                    result = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref bufferEndPoint);
                    if (!bufferNetEndPoint.EndPoint.Equals(bufferEndPoint))
                    {
                        bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
                    }
#endif
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.MessageSize)
                    {
                        //10040 - message too long
                        //10054 - remote close (not error)
                        //Just UDP
                        NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R] Ingored error: {0} - {1}", (int)ex.SocketErrorCode, ex.ToString() );
                        continue;
                    }
                    NetUtils.DebugWriteError("[R]Error code: {0} - {1}", (int)ex.SocketErrorCode, ex.ToString());
                    _onMessageReceived(null, 0, (int) ex.SocketErrorCode, bufferNetEndPoint);

                    continue;
                }

                if (result > 0)
                {
                    //All ok!
                    NetUtils.DebugWrite(ConsoleColor.Blue, "[R]Received data from {0}, result: {1}", bufferNetEndPoint.ToString(), result);
                    _onMessageReceived(receiveBuffer, result, 0, bufferNetEndPoint);
                }
            }
        }

        public bool Bind(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool reuseAddress)
        {
            _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocketv4.Blocking = false;
            _udpSocketv4.ReceiveBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.SendBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.Ttl = NetConstants.SocketTTL;
            _udpSocketv4.ReceiveTimeout = NetConstants.ReceiveTimeout;

            if(reuseAddress)
                _udpSocketv4.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
#if !NETCORE
            _udpSocketv4.DontFragment = true;
#endif
            try
            {
                _udpSocketv4.EnableBroadcast = true;
            }
            catch (SocketException e)
            {
                NetUtils.DebugWriteError("Broadcast error: {0}", e.ToString());
            }

            if (!BindSocket(_udpSocketv4, new IPEndPoint(addressIPv4, port)))
            {
                return false;
            }
            LocalPort = ((IPEndPoint) _udpSocketv4.LocalEndPoint).Port;
            _running = true;
            _threadv4 = new Thread(ReceiveLogic);
            _threadv4.Name = "SocketThreadv4(" + LocalPort + ")";
            _threadv4.IsBackground = true;
            _threadv4.Start(_udpSocketv4);

            //Check IPv6 support
            if (!IPv6Support)
                return true;

            _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            _udpSocketv6.Blocking = false;
            _udpSocketv6.ReceiveBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv6.SendBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv6.ReceiveTimeout = NetConstants.ReceiveTimeout;
            //_udpSocketv6.Ttl = NetConstants.SocketTTL;
            if (reuseAddress)
                _udpSocketv6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            //Use one port for two sockets
            if (BindSocket(_udpSocketv6, new IPEndPoint(addressIPv6, LocalPort)))
            {
                try
                {
#if !ENABLE_IL2CPP
                    _udpSocketv6.SetSocketOption(
                        SocketOptionLevel.IPv6, 
                        SocketOptionName.AddMembership,
                        new IPv6MulticastOption(MulticastAddressV6));
#endif
                }
                catch(Exception)
                {
                    // Unity3d throws exception - ignored
                }

                _threadv6 = new Thread(ReceiveLogic);
                _threadv6.Name = "SocketThreadv6(" + LocalPort + ")";
                _threadv6.IsBackground = true;
                _threadv6.Start(_udpSocketv6);
            }

            return true;
        }

        private bool BindSocket(Socket socket, IPEndPoint ep)
        {
            try
            {
                socket.Bind(ep);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ((IPEndPoint)socket.LocalEndPoint).Port);
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                //TODO: very temporary hack for iOS (Unity3D)
                if (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        public bool SendBroadcast(byte[] data, int offset, int size, int port)
        {
            bool success;
            try
            {
                success = _udpSocketv4.SendTo(
                             data,
                             offset,
                             size,
                             SocketFlags.None,
                             new IPEndPoint(IPAddress.Broadcast, port)) > 0;
           
                if (IPv6Support)
                {
                    success = success || _udpSocketv6.SendTo(
                                 data,
                                 offset,
                                 size,
                                 SocketFlags.None,
                                 new IPEndPoint(MulticastAddressV6, port)) > 0;
                }
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S][MCAST]" + ex);
                return false;
            }
            return success;
        }

        public int SendTo(byte[] data, int offset, int size, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                var socket = _udpSocketv4;
                if (remoteEndPoint.EndPoint.AddressFamily == AddressFamily.InterNetworkV6 && IPv6Support)
                {
                    socket = _udpSocketv6;
                }

                int result;
#if WIN32 && UNSAFE
                var handle = socket.Handle;
                IntPtr[] fileDescriptorSet = { (IntPtr)1, handle };
                int socketCount = select(0, null, fileDescriptorSet, null, ref SendPollTime);
                if ((SocketError)socketCount == SocketError.SocketError)
                {
                    throw new SocketException(Marshal.GetLastWin32Error());
                }
                if ((int)fileDescriptorSet[0] == 0 || fileDescriptorSet[1] != handle)
                {
                    return 0;
                }
                unsafe
                {
                    fixed (byte* pinnedBuffer = data)
                    {
                        result = sendto(
                            handle,
                            pinnedBuffer + offset,
                            size,
                            SocketFlags.None,
                            remoteEndPoint.SocketAddr,
                            remoteEndPoint.SocketAddr.Length);
                    }
                }

                if ((SocketError) result == SocketError.SocketError)
                {
                    throw new SocketException(Marshal.GetLastWin32Error());
                }
#else
                if (!socket.Poll(SocketSendPollTime, SelectMode.SelectWrite))
                    return -1;
                result = socket.SendTo(data, offset, size, SocketFlags.None, remoteEndPoint.EndPoint);
#endif

                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint.EndPoint, result);
                return result;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.NoBufferSpaceAvailable)
                {
                    return 0;
                }
                if (ex.SocketErrorCode != SocketError.MessageSize)
                {
                    NetUtils.DebugWriteError("[S]" + ex);
                }
                
                errorCode = (int)ex.SocketErrorCode;
                return -1;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S]" + ex);
                return -1;
            }
        }

        public void Close()
        {
            _running = false;

            //Close IPv4
            if (Thread.CurrentThread != _threadv4)
            {
                _threadv4.Join();
            }
            if (_udpSocketv4 != null)
            {
                _udpSocketv4.Close();
                _udpSocketv4 = null;
            }
            _threadv4 = null;

            //No ipv6
            if (_udpSocketv6 == null)
                return;

            //Close IPv6
            if (Thread.CurrentThread != _threadv6)
            {
                _threadv6.Join();
            }
            if (_udpSocketv6 != null)
            {
                _udpSocketv6.Close();
                _udpSocketv6 = null;
            }
            _threadv6 = null;
        }
    }
}