using System;
using System.Net;
using System.Net.Sockets;

namespace LiteNetLib
{
    public class NetSocket : INetSocket
    {
        private byte[] _receiveBuffer = new byte[NetConstants.MaxPacketSize];
        private Socket _udpSocket;               //Udp socket

        //Socket constructor
        public NetSocket()
        {
            _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _udpSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 1);
            _udpSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 1);
            //_udpSocket.Blocking = false;
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
                int result = _udpSocket.SendTo(packet.ToByteArray(), remoteEndPoint);
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]Send packet to {0}, result: {1}", remoteEndPoint, result);
                return result;
            }
            catch (Exception ex)
            {
                NetUtils.DebugWrite(ConsoleColor.Blue, "[S]" + ex.ToString());
                return -1;
            }
        }

        //Receive from
        public int ReceiveFrom(out NetPacket packet, ref EndPoint remoteEndPoint, ref int errorCode)
        {
            int result;

            //Reading data
            try
            {
                result = _udpSocket.ReceiveFrom(_receiveBuffer, ref remoteEndPoint);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    packet = null;
                    return 0;
                }
                else
                {
                    NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Error code: {0} - {1}", ex.ErrorCode, ex.ToString());
                    packet = null;
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
                packet = null;
                return 0;
            }
            else if (result < NetConstants.HeaderSize)
            {
                NetUtils.DebugWrite(ConsoleColor.DarkRed, "[R]Bad data (D<HS)");
                packet = null;
                return 0;
            }

            //Creating packet from data
            packet = NetPacket.CreateFromBytes(_receiveBuffer, result);
            if (packet == null)
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
