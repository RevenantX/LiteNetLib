#if !WINRT || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private NetEndPoint _localEndPoint;
        private Thread _threadv4;
        private Thread _threadv6;
        private bool _running;
        private readonly NetBase.OnMessageReceived _onMessageReceived;

        private static readonly IPAddress MulticastAddressV4 = IPAddress.Parse(NetConstants.MulticastGroupIPv4);
        private static readonly IPAddress MulticastAddressV6 = IPAddress.Parse(NetConstants.MulticastGroupIPv6);
        private static readonly bool IPv6Support = Socket.OSSupportsIPv6;
        private const int SocketReceivePollTime = 100000;
        private const int SocketSendPollTime = 5000;

        public NetEndPoint LocalEndPoint
        {
            get { return _localEndPoint; }
        }

        public NetSocket(NetBase.OnMessageReceived onMessageReceived)
        {
            _onMessageReceived = onMessageReceived;
        }

        private void ReceiveLogic(object state)
        {
            Socket socket = (Socket)state;
            EndPoint bufferEndPoint = new IPEndPoint(socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            NetEndPoint bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
            byte[] receiveBuffer = new byte[NetConstants.PacketSizeLimit];

            while (_running)
            {
                //wait for data
                if (!socket.Poll(SocketReceivePollTime, SelectMode.SelectRead))
                {
                    continue;
                }

                int result;

                //Reading data
                try
                {
                    result = socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref bufferEndPoint);
                    if (!bufferNetEndPoint.EndPoint.Equals(bufferEndPoint))
                    {
                        bufferNetEndPoint = new NetEndPoint((IPEndPoint)bufferEndPoint);
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode == SocketError.ConnectionReset ||
                        ex.SocketErrorCode == SocketError.MessageSize)
                    {
                        //10040 - message too long
                        //10054 - remote close (not error)
                        //Just UDP
                        NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R] Ingored error: {0} - {1}", ex.ErrorCode, ex.ToString() );
                        continue;
                    }
                    NetUtils.DebugWriteError("[R]Error code: {0} - {1}", ex.ErrorCode, ex.ToString());
                    _onMessageReceived(null, 0, (int)ex.SocketErrorCode, bufferNetEndPoint);
                    continue;
                }

                //All ok!
                NetUtils.DebugWrite(ConsoleColor.Blue, "[R]Recieved data from {0}, result: {1}", bufferNetEndPoint.ToString(), result);
                _onMessageReceived(receiveBuffer, result, 0, bufferNetEndPoint);
            }
        }

        public bool Bind(int port)
        {
            _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocketv4.Blocking = false;
            _udpSocketv4.ReceiveBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.SendBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, NetConstants.SocketTTL);
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, true);

            if (!BindSocket(_udpSocketv4, new IPEndPoint(IPAddress.Any, port)))
            {
                return false;
            }
            _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastAddressV4, IPAddress.Any));
            _localEndPoint = new NetEndPoint((IPEndPoint)_udpSocketv4.LocalEndPoint);

            _running = true;
            _threadv4 = new Thread(ReceiveLogic);
            _threadv4.Name = "SocketThreadv4(" + port + ")";
            _threadv4.IsBackground = true;
            _threadv4.Start(_udpSocketv4);

            //Check IPv6 support
            if (!IPv6Support)
                return true;

            //Use one port for two sockets
            port = _localEndPoint.Port;

            _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            _udpSocketv6.Blocking = false;
            _udpSocketv6.ReceiveBufferSize = NetConstants.SocketBufferSize;
            _udpSocketv6.SendBufferSize = NetConstants.SocketBufferSize;

            if (BindSocket(_udpSocketv6, new IPEndPoint(IPAddress.IPv6Any, port)))
            {
                _localEndPoint = new NetEndPoint((IPEndPoint)_udpSocketv6.LocalEndPoint);

                try
                {
                    _udpSocketv6.SetSocketOption(
                        SocketOptionLevel.IPv6, 
                        SocketOptionName.AddMembership,
                        new IPv6MulticastOption(MulticastAddressV6));
                }
                catch
                {
                    // Unity3d throws exception - ignored
                }

                _threadv6 = new Thread(ReceiveLogic);
                _threadv6.Name = "SocketThreadv6(" + port + ")";
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

        public bool SendMulticast(byte[] data, int offset, int size, int port)
        {
            try
            {
                int result = _udpSocketv4.SendTo(data, offset, size, SocketFlags.None, new IPEndPoint(MulticastAddressV4, port));
                if (result <= 0)
                    return false;
                if (IPv6Support)
                {
                    result = _udpSocketv6.SendTo(data, offset, size, SocketFlags.None, new IPEndPoint(MulticastAddressV6, port));
                    if (result <= 0)
                        return false;
                }
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S][MCAST]" + ex);
                return false;
            }
            return true;
        }

        public int SendTo(byte[] data, int offset, int size, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                int result = 0;
                if (remoteEndPoint.EndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (!_udpSocketv4.Poll(SocketSendPollTime, SelectMode.SelectWrite))
                        return -1;
                    result = _udpSocketv4.SendTo(data, offset, size, SocketFlags.None, remoteEndPoint.EndPoint);
                }
                else if(IPv6Support)
                {
                    if (!_udpSocketv6.Poll(SocketSendPollTime, SelectMode.SelectWrite))
                        return -1;
                    result = _udpSocketv6.SendTo(data, offset, size, SocketFlags.None, remoteEndPoint.EndPoint);
                }

                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint.EndPoint, result);
                return result;
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.MessageSize)
                {
                    NetUtils.DebugWriteError("[S]" + ex);
                }
                
                errorCode = ex.ErrorCode;
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
            _threadv4 = null;
            if (_udpSocketv4 != null)
            {
                _udpSocketv4.Close();
                _udpSocketv4 = null;
            }

            //No ipv6
            if (_udpSocketv6 == null)
                return;

            //Close IPv6
            if (Thread.CurrentThread != _threadv6)
            {
                _threadv6.Join();
            }
            _threadv6 = null;
            if (_udpSocketv6 != null)
            {
                _udpSocketv6.Close();
                _udpSocketv6 = null;
            }
        }
    }
}

#endif
