using System;
using System.Net;
using System.Text;

namespace LiteNetLib
{
    public class NetPeer
    {
        private ReliableConnection _connection;
        private int _id;

        public NetPeer(ReliableConnection connection, int id)
        {
            _connection = connection;
            _id = id;
        }

        public EndPoint GetEndPoint()
        {
            return _connection.EndPoint;
        }

        public void SendString(string str)
        {
            _connection.Send(Encoding.ASCII.GetBytes(str), PacketProperty.Reliable);
        }

        public void Send(byte[] data, SendOptions options)
        {
            PacketProperty p;
            switch (options)
            {
                case SendOptions.None: p = PacketProperty.None; break;
                case SendOptions.InOrder: p = PacketProperty.InOrder; break;
                case SendOptions.Reliable: p = PacketProperty.Reliable; break;
                case SendOptions.ReliableInOrder: p = PacketProperty.ReliableInOrder; break;
                default: p = PacketProperty.None; break;
            }
            _connection.Send(data, p);
        }

        public int Id 
        {
            get { return _id; }
        }

        public int Ping
        {
            get { return _connection.Ping; }
        }

        public ReliableConnection Connection
        {
            get { return _connection; }
        }
    }
}
