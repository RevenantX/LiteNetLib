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
        private Socket _udpSocket;
        private EndPoint _bufferEndPoint;
        private const int SocketTTL = 255;
        private readonly AddressFamily _socketAddressFamily;

        public int ReceiveTimeout = 10;

        public NetSocket(ConnectionAddressType addrType)
        {
            _socketAddressFamily = 
                addrType == ConnectionAddressType.IPv4 ? 
                AddressFamily.InterNetwork : 
                AddressFamily.InterNetworkV6;
        }

        public bool Bind(ref NetEndPoint ep)
        {
            _udpSocket = new Socket(_socketAddressFamily, SocketType.Dgram, ProtocolType.Udp);
            if (_socketAddressFamily == AddressFamily.InterNetwork) //IPv4
            {
                _bufferEndPoint = new IPEndPoint(IPAddress.Any, 0);
                _udpSocket.DontFragment = true;
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, SocketTTL);
            }
            else //IPv6
            {
                _bufferEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);
            }
            _udpSocket.Blocking = false;
            _udpSocket.ReceiveBufferSize = BufferSize;
            _udpSocket.SendBufferSize = BufferSize;

            _udpSocket.EnableBroadcast = true;

            try
            {
                _udpSocket.Bind(ep.EndPoint);
                ep = new NetEndPoint((IPEndPoint)_udpSocket.LocalEndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ep.Port);
                return true;
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[B]Bind exception: {0}", ex.ToString());
                //TODO: very temporary hack for iOS (Unity3D)
                if (ex.ErrorCode == 10047)
                {
                    ep = new NetEndPoint((IPEndPoint)_udpSocket.LocalEndPoint);
                    return true;
                }

                return false;
            }
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
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint.EndPoint,
                    result);
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
            if (!_udpSocket.Poll(ReceiveTimeout * 1000, SelectMode.SelectRead))
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
                    remoteEndPoint = new NetEndPoint((IPEndPoint)_bufferEndPoint);
                }
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWriteError("[R]Error code: {0} - {1}", ex.SocketErrorCode, ex.ToString());
                errorCode = (int) ex.SocketErrorCode;
                return -1;
            }

            //All ok!
            NetUtils.DebugWriteError("[R]Recieved data from {0}, result: {1}", remoteEndPoint.ToString(), result);

            //Assign data
            data = _receiveBuffer;

            //Creating packet from data
            return result;
        }

        public void Close()
        {
            _udpSocket.Close();
            _udpSocket = null;
        }
    }
}
#endif
