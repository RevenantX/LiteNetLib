#if UNITY_4 || UNITY_5 || UNITY_5_3_OR_NEWER
#define UNITY
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
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private Thread _threadv4;
        private Thread _threadv6;
        private bool _running;
        private readonly INetSocketListener _listener;

        private const int Timeout = 100;
        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse (NetConstants.MulticastGroupIPv6);
        internal static readonly bool IPv6Support;

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
#if UNITY
            IPv6Support = Socket.SupportsIPv6;
#elif DISABLE_IPV6
            IPv6Support = false;
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
                    result = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref bufferEndPoint);
                }
                catch (SocketException ex)
                {
                    switch (ex.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                            return;
                        case SocketError.ConnectionReset:
                        case SocketError.MessageSize:
                        case SocketError.TimedOut:
                            NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R] Ingored error: {0} - {1}", (int)ex.SocketErrorCode, ex.ToString());
                            break;
                        default:
                            NetUtils.DebugWriteError("[R]Error code: {0} - {1}", (int)ex.SocketErrorCode, ex.ToString());
                            _listener.OnMessageReceived(null, 0, ex.SocketErrorCode, (IPEndPoint)bufferEndPoint);
                            break;
                    }
                    continue;
                }

                //All ok!
                NetUtils.DebugWrite(ConsoleColor.Blue, "[R]Received data from {0}, result: {1}", bufferEndPoint.ToString(), result);
                _listener.OnMessageReceived(receiveBuffer, result, 0, (IPEndPoint)bufferEndPoint);
            }
        }

        public bool Bind(IPAddress addressIPv4, IPAddress addressIPv6, int port, bool reuseAddress)
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
            if (!IPv6Support)
                return true;

            _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            //Use one port for two sockets
            if (BindSocket(_udpSocketv6, new IPEndPoint(addressIPv6, LocalPort), reuseAddress))
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

        private bool BindSocket(Socket socket, IPEndPoint ep, bool reuseAddress)
        {
            //Setup socket
            socket.ReceiveTimeout = Timeout;
            socket.SendTimeout = Timeout;
            socket.ExclusiveAddressUse = true;
            socket.ReceiveBufferSize = NetConstants.SocketBufferSize;
            socket.SendBufferSize = NetConstants.SocketBufferSize;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, reuseAddress);
            if (socket.AddressFamily == AddressFamily.InterNetwork)
            {
                socket.Ttl = NetConstants.SocketTTL;
                
                try { socket.DontFragment = true; }
                catch (SocketException e)
                {
                    NetUtils.DebugWriteError("[B]DontFragment error: {0}", e.SocketErrorCode);
                }

                try { socket.EnableBroadcast = true; }
                catch (SocketException e)
                {
                    NetUtils.DebugWriteError("[B]Broadcast error: {0}", e.SocketErrorCode);
                }
            }
            else
            {
#if !UNITY
                try { socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName) 27, true); }
                catch (SocketException)
                {
                }
#endif
            }

            //Bind
            try
            {
                socket.Bind(ep);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Successfully binded to port: {0}", ((IPEndPoint)socket.LocalEndPoint).Port);
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                //hack for iOS (Unity3D)
                if (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                    return true;
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

        public int SendTo(byte[] data, int offset, int size, IPEndPoint remoteEndPoint, ref SocketError errorCode)
        {
            try
            {
                var socket = _udpSocketv4;
                if (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 && IPv6Support)
                    socket = _udpSocketv6;
                int result = socket.SendTo(data, offset, size, SocketFlags.None, remoteEndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
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
                        NetUtils.DebugWriteError("[S]" + ex);
                        break;
                }    
                errorCode = ex.SocketErrorCode;
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
            if (_udpSocketv4 != null)
            {
                _udpSocketv4.Close();
                _udpSocketv4 = null;
                if (_threadv4 != Thread.CurrentThread)
                    _threadv4.Join();
                _threadv4 = null;
            }
            if (_udpSocketv6 != null)
            {
                _udpSocketv6.Close();
                _udpSocketv6 = null;
                if (_threadv6 != Thread.CurrentThread)
                    _threadv6.Join();
                _threadv6 = null;
            }
        }
    }
}