using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    sealed class NetSocket
    {
        private const int BufferSize = ushort.MaxValue;
        private readonly byte[] _receiveBuffer = new byte[NetConstants.PacketSizeLimit];
        private Socket _udpSocket;

        public int ReceiveTimeout = 10;

        public bool Bind(NetEndPoint ep)
        {            
            try
            {
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
                _udpSocket.Blocking = false;
                _udpSocket.ReceiveBufferSize = BufferSize;
                _udpSocket.SendBufferSize = BufferSize;
                _udpSocket.DontFragment = true;
                _udpSocket.EnableBroadcast = true;
                _udpSocket.Bind(ep.EndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ep.Port);
                return true;
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Red, "[B]Bind exception: {0}", ex.ToString());
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
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]" + ex);
                errorCode = ex.ErrorCode;
                return -1;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]" + ex);
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
                EndPoint p = remoteEndPoint.EndPoint;
                result = _udpSocket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref p);
                remoteEndPoint = new NetEndPoint((IPEndPoint)p);
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Error code: {0} - {1}", ex.SocketErrorCode, ex.ToString());
                errorCode = (int) ex.SocketErrorCode;
                return -1;
            }

            //All ok!
            NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Recieved data from {0}, result: {1}", remoteEndPoint.ToString(), result);

            //Detecting bad data
            if (result == 0)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (0)");
                return 0;
            }

            if (result < NetConstants.HeaderSize)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (D<HS)");
                return 0;
            }
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
