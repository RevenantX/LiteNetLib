#if UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
#define UNITY
#endif
#if NETSTANDARD2_0 || NETCOREAPP2_0
using System.Runtime.InteropServices;
#endif

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LiteNetLib
{
    internal interface INetSocketListener
    {
        void OnMessageReceived(byte[] data, int length, SocketError errorCode, IPEndPoint remoteEndPoint);
    }

    internal sealed class NetSocket
    {
        public const int ReceivePollingTime = 1000000; //1 second
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private Thread _threadv4;
        private Thread _threadv6;
        private volatile bool _running;
        private readonly INetSocketListener _listener;
        private const int SioUdpConnreset = -1744830452; //SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12
        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse("FF02:0:0:0:0:0:0:1");
        internal static readonly bool IPv6Support;

        public int LocalPort { get; private set; }

        public short Ttl
        {
            get { return _udpSocketv4.Ttl; }
            set { _udpSocketv4.Ttl = value; }
        }

        static NetSocket()
        {
#if DISABLE_IPV6 || (!UNITY_EDITOR && ENABLE_IL2CPP && !UNITY_2018_3_OR_NEWER)
            IPv6Support = false;
#elif !UNITY_2019_1_OR_NEWER && !UNITY_2018_4_OR_NEWER && (!UNITY_EDITOR && ENABLE_IL2CPP && UNITY_2018_3_OR_NEWER)
            string version = UnityEngine.Application.unityVersion;
            IPv6Support = Socket.OSSupportsIPv6 && int.Parse(version.Remove(version.IndexOf('f')).Split('.')[2]) >= 6;
#elif UNITY_2018_2_OR_NEWER
            IPv6Support = Socket.OSSupportsIPv6;
#elif UNITY
#pragma warning disable 618
            IPv6Support = Socket.SupportsIPv6;
#pragma warning restore 618
#else
            IPv6Support = Socket.OSSupportsIPv6;
#endif
        }

        public NetSocket(INetSocketListener listener)
        {
            _listener = listener;
        }

        private void ReceiveLogic(object state)
        {
            Socket socket = (Socket)state;
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            byte[] receiveBuffer = new byte[NetConstants.MaxPacketSize];

            while (_running)
            {
                int result;

                //Reading data
                try
                {
                    if (socket.Available == 0 && !socket.Poll(ReceivePollingTime, SelectMode.SelectRead))
                        continue;
                    result = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None,
                        ref bufferEndPoint);
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                        case SocketError.NotSocket:
                            return;
                        case SocketError.ConnectionReset:
                        case SocketError.MessageSize:
                        case SocketError.TimedOut:
                            NetDebug.Write(NetLogLevel.Trace, "[R]Ignored error: {0} - {1}",
                                (int) ex.SocketErrorCode, ex.ToString());
                            break;
                        default:
                            NetDebug.WriteError("[R]Error code: {0} - {1}", (int) ex.SocketErrorCode,
                                ex.ToString());
                            _listener.OnMessageReceived(null, 0, ex.SocketErrorCode, (IPEndPoint) bufferEndPoint);
                            break;
                    }

                    continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                //All ok!
                NetDebug.Write(NetLogLevel.Trace, "[R]Received data from {0}, result: {1}", bufferEndPoint.ToString(), result);
                _listener.OnMessageReceived(receiveBuffer, result, 0, (IPEndPoint)bufferEndPoint);
            }
        }

        public bool Bind(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool reuseAddress, bool ipv6)
        {
            _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (!BindSocket(_udpSocketv4, new IPEndPoint(addressIPv4, port), reuseAddress))
                return false;
            LocalPort = ((IPEndPoint) _udpSocketv4.LocalEndPoint).Port;
            _running = true;
            _threadv4 = new Thread(ReceiveLogic);
            _threadv4.Name = "SocketThreadv4(" + LocalPort + ")";
            _threadv4.IsBackground = true;
            _threadv4.Start(_udpSocketv4);

            //Check IPv6 support
            if (!IPv6Support || !ipv6)
                return true;

            _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            //Use one port for two sockets
            if (BindSocket(_udpSocketv6, new IPEndPoint(addressIPv6, LocalPort), reuseAddress))
            {
                try
                {
#if !UNITY
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

        private bool BindSocket(Socket socket, IPEndPoint ep, bool reuseAddress)
        {
            //Setup socket
            socket.ReceiveTimeout = 500;
            socket.SendTimeout = 500;
            socket.ReceiveBufferSize = NetConstants.SocketBufferSize;
            socket.SendBufferSize = NetConstants.SocketBufferSize;
            try
            {
                socket.IOControl(SioUdpConnreset, new byte[] {0}, null);
            }
            catch
            {
                //ignored
            }

            try
            {
                socket.ExclusiveAddressUse = !reuseAddress;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, reuseAddress);
            }
            catch
            {
                //Unity with IL2CPP throws an exception here, it doesn't matter in most cases so just ignore it
            }
            if (socket.AddressFamily == AddressFamily.InterNetwork)
            {
                socket.Ttl = NetConstants.SocketTTL;

#if NETSTANDARD2_0 || NETCOREAPP2_0
                if(!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
#endif
                try { socket.DontFragment = true; }
                catch (SocketException e)
                {
                    NetDebug.WriteError("[B]DontFragment error: {0}", e.SocketErrorCode);
                }

                try { socket.EnableBroadcast = true; }
                catch (SocketException e)
                {
                    NetDebug.WriteError("[B]Broadcast error: {0}", e.SocketErrorCode);
                }
            }

            //Bind
            try
            {
                socket.Bind(ep);
                NetDebug.Write(NetLogLevel.Trace, "[B]Successfully binded to port: {0}", ((IPEndPoint)socket.LocalEndPoint).Port);
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
                                socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, true);
                                socket.Bind(ep);
                            }
                            catch (SocketException ex)
                            {
                                NetDebug.WriteError("[B]Bind exception: {0}, errorCode: {1}", ex.ToString(), ex.SocketErrorCode);
                                return false;
                            }
                            return true;
                        }
                        break;
                    //hack for iOS (Unity3D)
                    case SocketError.AddressFamilyNotSupported:
                        return true;
                }
                NetDebug.WriteError("[B]Bind exception: {0}, errorCode: {1}", bindException.ToString(), bindException.SocketErrorCode);
                return false;
            }
            return true;
        }

        public bool SendBroadcast(byte[] data, int offset, int size, int port)
        {
            bool broadcastSuccess = false;
            bool multicastSuccess = false;
            try
            {
                broadcastSuccess = _udpSocketv4.SendTo(
                             data,
                             offset,
                             size,
                             SocketFlags.None,
                             new IPEndPoint(IPAddress.Broadcast, port)) > 0;
           
                if (_udpSocketv6 != null)
                {
                    multicastSuccess = _udpSocketv6.SendTo(
                                                data,
                                                offset,
                                                size,
                                                SocketFlags.None,
                                                new IPEndPoint(MulticastAddressV6, port)) > 0;
                }
            }
            catch (Exception ex)
            {
                NetDebug.WriteError("[S][MCAST]" + ex);
                return broadcastSuccess;
            }
            return broadcastSuccess || multicastSuccess;
        }

        public int SendTo(byte[] data, int offset, int size, IPEndPoint remoteEndPoint, ref SocketError errorCode)
        {
            try
            {
                var socket = _udpSocketv4;
                if (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 && IPv6Support)
                    socket = _udpSocketv6;
                int result = socket.SendTo(data, offset, size, SocketFlags.None, remoteEndPoint);
                NetDebug.Write(NetLogLevel.Trace, "[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
                return result;
            }
            catch (SocketException ex)
            {
                switch (ex.SocketErrorCode)
                {
                    case SocketError.NoBufferSpaceAvailable:
                    case SocketError.Interrupted:
                        return 0;
                    case SocketError.MessageSize: //do nothing              
                        break;
                    default:
                        NetDebug.WriteError("[S]" + ex);
                        break;
                }    
                errorCode = ex.SocketErrorCode;
                return -1;
            }
            catch (Exception ex)
            {
                NetDebug.WriteError("[S]" + ex);
                return -1;
            }
        }

        public void Close()
        {
            _running = false;
            // first close sockets
            if (_udpSocketv4 != null)
            {
                _udpSocketv4.Close();
                _udpSocketv4 = null;
            }
            if (_udpSocketv6 != null)
            {
                _udpSocketv6.Close();
                _udpSocketv6 = null;
            }
            // then join threads
            if (_threadv4 != null)
            {
                if (_threadv4 != Thread.CurrentThread)
                    _threadv4.Join();
                _threadv4 = null;
            }
            if (_threadv6 != null)
            {
                if (_threadv6 != Thread.CurrentThread)
                    _threadv6.Join();
                _threadv6 = null;
            }
        }
    }
}
