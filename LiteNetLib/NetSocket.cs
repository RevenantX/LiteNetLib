using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    public class NetSocket
    {
        private const int BufferSize = 131071;
        private byte[] _receiveBuffer = new byte[BufferSize];
        private Socket _udpSocket;               //Udp socket

        //Socket constructor
        public NetSocket()
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
            _udpSocket.ReceiveBufferSize = BufferSize;
            _udpSocket.SendBufferSize = BufferSize;
            //_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 1);
            //_udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1);
            _udpSocket.Blocking = false;
            //_udpSocket.DontFragment = true;
        }

        //Bind socket to port
        public bool Bind(IPEndPoint ep)
        {            
            try
            {
                _udpSocket.Bind(ep);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[B]Succesfully binded to port: {0}", ep.Port);
                return true;
            }
            catch (SocketException ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Red, "[B]Bind exception: {0}", ex.ToString());
                return false;
            }
        }

        //Send to
        public int SendTo(NetPacket packet, EndPoint remoteEndPoint)
        {
            try
            {
                byte[] data = packet.ToByteArray();
                int result = _udpSocket.SendTo(data, remoteEndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
                return result;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]" + ex);
                return -1;
            }
        }

        //Receive from
        public int ReceiveFrom(NetPacket packet, ref EndPoint remoteEndPoint, ref int errorCode)
        {
            //wait for data
            if (!_udpSocket.Poll(1000, SelectMode.SelectRead))
            {
                return 0;
            }

            int result;

            //Reading data
            try
            {
                result = _udpSocket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, ref remoteEndPoint);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    return 0;
                }
                else
                {
                    NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Error code: {0} - {1}", ex.ErrorCode, ex.ToString());
                    errorCode = ex.ErrorCode;
                    return -1;
                }
            }

            //All ok!
            NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Recieved data from {0}, result: {1}", remoteEndPoint.ToString(), result);

            //Detecting bad data
            if (result == 0)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (0)");
                return 0;
            }
            else if (result < NetConstants.HeaderSize)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (D<HS)");
                return 0;
            }

            //Creating packet from data
            if (!packet.FromBytes(_receiveBuffer, result))
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (corrupted packet)");
                return 0;
            }

            return result;
        }

        //Close socket
        public void Close()
        {
            _udpSocket.Close();
        }
    }
}
