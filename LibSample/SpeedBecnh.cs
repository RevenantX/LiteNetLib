using System;
using System.Net;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    internal class SpeedBecnh
    {
        public class Server : INetEventListener
        {
            public int ReliableReceived;
            public int UnreliableReceived;

            readonly NetManager _server;

            public Server()
            {
                _server = new NetManager(this, 15);
                _server.UpdateTime = 1;
                _server.Start(9050);
            }

            public void PollEvents()
            {
                _server.PollEvents();
            }

            void INetEventListener.OnNetworkError(IPEndPoint endPoint, int socketErrorCode)
            {
            }

            void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.AcceptIfKey("ConnKey");
            }

            void INetEventListener.OnNetworkReceive(NetPeer peer, NetDataReader reader, DeliveryMethod deliveryMethod)
            {
                var _isReliable = reader.GetBool();
                var _data = reader.GetString();

                if (_isReliable)
                {
                    ReliableReceived++;
                }
                else
                {
                    UnreliableReceived++;
                }
            }

            void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetDataReader reader,
                UnconnectedMessageType messageType)
            {
            }

            void INetEventListener.OnPeerConnected(NetPeer peer)
            {

            }

            void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
            }
        }

        public class Client : INetEventListener
        {
            public int ReliableSent;
            public int UnreliableSent;

            readonly NetManager _client;
            readonly NetDataWriter _writer;
            NetPeer _peer;

            public NetStatistics Stats
            {
                get { return _client.Statistics; }
            }

            public Client()
            {
                _writer = new NetDataWriter();

                _client = new NetManager(this);
                _client.Start();
            }

            public void SendUnreliable(string pData)
            {
                _writer.Reset();
                _writer.Put(false);
                _writer.Put(pData);

                _peer.Send(_writer, DeliveryMethod.Unreliable);
                UnreliableSent++;
            }

            public void SendReliable(string pData)
            {
                _writer.Reset();
                _writer.Put(true);
                _writer.Put(pData);

                _peer.Send(_writer, DeliveryMethod.ReliableOrdered);
                ReliableSent++;
            }

            public void Connect()
            {
                _peer = _client.Connect("localhost", 9050, "ConnKey");
            }

            public void PollEvents()
            {
                _client.PollEvents();
            }

            void INetEventListener.OnNetworkError(IPEndPoint endPoint, int socketErrorCode)
            {
            }

            void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.AcceptIfKey("ConnKey");
            }

            void INetEventListener.OnNetworkReceive(NetPeer peer, NetDataReader reader, DeliveryMethod deliveryMethod)
            {

            }

            void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetDataReader reader,
                UnconnectedMessageType messageType)
            {
            }

            void INetEventListener.OnPeerConnected(NetPeer peer)
            {
            }

            void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
            }
        }
        private const string DATA = "The quick brown fox jumps over the lazy dog";
        private static int MAX_LOOP_COUNT = 750;
        private static int UNRELIABLE_MESSAGES_PER_LOOP = 1000;
        private static int RELIABLE_MESSAGES_PER_LOOP = 350;
        private static bool CLIENT_RUNNING = true;

        public void Run()
        {
            Console.WriteLine("Testing LiteNetLib...");
            Thread _serverThread = new Thread(StartServer);
            _serverThread.Start();
            Thread _clientThread = new Thread(StartClient);
            _clientThread.Start();
            Console.WriteLine("Processing...");
            Console.ReadKey();
        }

        private static void StartServer()
        {
            Server _s = new Server();

            while (CLIENT_RUNNING)
            {
                _s.PollEvents();
                Thread.Sleep(1);
            }

            Thread.Sleep(10000);
            _s.PollEvents();

            Console.WriteLine("SERVER RECEIVED -> Reliable: " + _s.ReliableReceived + ", Unreliable: " + _s.UnreliableReceived);
        }

        private static void StartClient()
        {
            Client _c = new Client();
            _c.Connect();

            for (int i = 0; i < MAX_LOOP_COUNT; i++)
            {
                //for (int ui = 0; ui < UNRELIABLE_MESSAGES_PER_LOOP; ui++)
                //    _c.SendUnreliable(DATA);

                for (int ri = 0; ri < RELIABLE_MESSAGES_PER_LOOP; ri++)
                    _c.SendReliable(DATA);
                _c.PollEvents();
            }

            int dataSize = MAX_LOOP_COUNT * Encoding.UTF8.GetByteCount(DATA) * (UNRELIABLE_MESSAGES_PER_LOOP + RELIABLE_MESSAGES_PER_LOOP);
            Console.WriteLine("DataSize: {0}b, {1}kb, {2}mb", dataSize, dataSize/1024, dataSize/1024/1024);

            CLIENT_RUNNING = false;
            Thread.Sleep(10000);

            Console.WriteLine("CLIENT SENT -> Reliable: " + _c.ReliableSent + ", Unreliable: " + _c.UnreliableSent);
            Console.WriteLine("CLIENT STATS:\n" + _c.Stats);
        }
    }
}
