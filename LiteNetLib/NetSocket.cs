#if !WINRT || UNITY_EDITOR
using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    internal sealed class NetSocket
    {
        private const int BufferSize = ushort.MaxValue;
        private readonly byte[] _receiveBuffer = new byte[NetConstants.PacketSizeLimit];
        private Socket _udpSocketv4;
        private Socket _udpSocketv6;
        private EndPoint _bufferEndPointv4;
        private EndPoint _bufferEndPointv6;
        private const int SocketTTL = 255;
        private readonly ConnectionAddressType _connectionAddressType;

        public int ReceiveTimeout = 1000;

        public NetSocket(ConnectionAddressType addrType)
        {
            _connectionAddressType = addrType;
        }

        public bool Bind()
        {
            if (_connectionAddressType == ConnectionAddressType.IPv4 ||
                _connectionAddressType == ConnectionAddressType.Dual)
            {
                _udpSocketv4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _udpSocketv4.DontFragment = true;
                _udpSocketv4.Blocking = false;
                _udpSocketv4.ReceiveBufferSize = BufferSize;
                _udpSocketv4.SendBufferSize = BufferSize;
                _udpSocketv4.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, SocketTTL);

                _bufferEndPointv4 = new IPEndPoint(IPAddress.Any, 0);


                if (!BindSocket(_udpSocketv4, new IPEndPoint(IPAddress.Any, 0)))
                {
                    return false;
                }
            }

            if (_connectionAddressType == ConnectionAddressType.IPv6 ||
                _connectionAddressType == ConnectionAddressType.Dual)
            {
                _udpSocketv6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                _udpSocketv6.Blocking = false;
                _udpSocketv6.ReceiveBufferSize = BufferSize;
                _udpSocketv6.SendBufferSize = BufferSize;

                _bufferEndPointv6 = new IPEndPoint(IPAddress.IPv6Any, 0);

                if(!BindSocket(_udpSocketv6, new IPEndPoint(IPAddress.IPv6Any, 0)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool BindSocket(Socket socket, EndPoint ep)
        {
            try
            {
                socket.Bind(ep);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ep.Port);
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                //TODO: very temporary hack for iOS (Unity3D)
                if (ex.ErrorCode == 10047)
                {
                    return true;
                }
                return false;
            }
            return true;
        }

        public int SendTo(byte[] data, NetEndPoint remoteEndPoint)
        {
            int unusedErrorCode = 0;
            return SendTo(data, remoteEndPoint, ref unusedErrorCode);
        }

        public int SendTo(byte[] data, NetEndPoint remoteEndPoint, ref int errorCode)
        {
            try
            {
                if (!_udpSocket.Poll(1000, SelectMode.SelectWrite))
                    return -1;

                int result = _udpSocket.SendTo(data, remoteEndPoint.EndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint.EndPoint, result);
                return result;
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[S]" + ex);
                errorCode = ex.ErrorCode;
                return -1;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWriteError("[S]" + ex);
                return -1;
            }
        }

        public int ReceiveFrom(ref byte[] data, ref NetEndPoint remoteEndPoint, ref int errorCode)
        {
            //wait for data
            if (!_udpSocket.Poll(ReceiveTimeout*1000, SelectMode.SelectRead))
            {
                return 0;
            }

            int result;

            //Reading data
            try
            {
                result = _udpSocket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref _bufferEndPoint);
                if (!remoteEndPoint.EndPoint.Equals(_bufferEndPoint))
                {
                    remoteEndPoint = new NetEndPoint((IPEndPoint) _bufferEndPoint);
                }
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[R]Error code: {0} - {1}", ex.SocketErrorCode, ex.ToString());
                errorCode = (int) ex.SocketErrorCode;
                return -1;
            }

            //All ok!
            NetUtils.DebugWrite(ConsoleColor.Blue, "[R]Recieved data from {0}, result: {1}", remoteEndPoint.ToString(), result);

            //Assign data
            data = _receiveBuffer;

            //Creating packet from data
            return result;
        }

        public void Close()
        {
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
        }
    }
}

#endif
