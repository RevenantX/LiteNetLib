using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    internal class SpeedBench : IExample
    {
        public class Server : INetEventListener, IDisposable
        {
            public int ReliableReceived;
            public int UnreliableReceived;

            readonly NetManager _server;

            public Server()
            {
                _server = new NetManager(this);
                _server.AutoRecycle = true;
                _server.UpdateTime = 1;
                _server.SimulatePacketLoss = false;
                _server.SimulationPacketLossChance = 20;
                _server.Start(9050);
            }

            public void PollEvents()
            {
                _server.PollEvents();
            }

            void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
            {
            }

            void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.AcceptIfKey("ConnKey");
            }

            void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                var isReliable = reader.GetBool();
                var data = reader.GetString();

                if (isReliable)
                {
                    ReliableReceived++;
                }
                else
                {
                    UnreliableReceived++;
                }
            }

            void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
                UnconnectedMessageType messageType)
            {
            }

            void INetEventListener.OnPeerConnected(NetPeer peer)
            {

            }

            void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                
            }

            public void Dispose()
            {
                _server.Stop();
            }
        }

        public class Client : INetEventListener, IDisposable
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
                _client.UnsyncedEvents = true;
                _client.AutoRecycle = true;
                _client.SimulatePacketLoss = false;
                _client.SimulationPacketLossChance = 20;
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

            void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
            {
            }

            void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.AcceptIfKey("ConnKey");
            }

            void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {

            }

            void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
                UnconnectedMessageType messageType)
            {
            }

            void INetEventListener.OnPeerConnected(NetPeer peer)
            {
            }

            void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
            }

            public void Dispose()
            {
                _client.Stop();
            }
        }
        private const string DATA = "The quick brown fox jumps over the lazy dog";
        private const int MAX_LOOP_COUNT = 750;
        private const int UNRELIABLE_MESSAGES_PER_LOOP = 1000;
        private const int RELIABLE_MESSAGES_PER_LOOP = 350;
        private static bool CLIENT_RUNNING = true;

        public void Run()
        {
            Console.WriteLine("Testing LiteNetLib...");
            Thread serverThread = new Thread(StartServer);
            serverThread.Start();
            Thread clientThread = new Thread(StartClient);
            clientThread.Start();
            Console.WriteLine("Processing...");
            serverThread.Join();
            clientThread.Join();
            Console.WriteLine("Test is end.");
        }

        private static void StartServer()
        {
            using (Server s = new Server())
            {
                while (CLIENT_RUNNING)
                {
                    s.PollEvents();
                    Thread.Sleep(1);
                }

                Thread.Sleep(10000);
                s.PollEvents();
                Console.WriteLine("SERVER RECEIVED -> Reliable: " + s.ReliableReceived + ", Unreliable: " + s.UnreliableReceived);
            }
        }

        private static void StartClient()
        {
            using (Client c = new Client())
            {
                c.Connect();

                for (int i = 0; i < MAX_LOOP_COUNT; i++)
                {
                    for (int ui = 0; ui < UNRELIABLE_MESSAGES_PER_LOOP; ui++)
                        c.SendUnreliable(DATA);

                    for (int ri = 0; ri < RELIABLE_MESSAGES_PER_LOOP; ri++)
                        c.SendReliable(DATA);
                }

                int dataSize = MAX_LOOP_COUNT * Encoding.UTF8.GetByteCount(DATA) * (UNRELIABLE_MESSAGES_PER_LOOP + RELIABLE_MESSAGES_PER_LOOP);
                Console.WriteLine("DataSize: {0}b, {1}kb, {2}mb", dataSize, dataSize / 1024, dataSize / 1024 / 1024);

                Thread.Sleep(10000);
                CLIENT_RUNNING = false;

                Console.WriteLine("CLIENT SENT -> Reliable: " + c.ReliableSent + ", Unreliable: " + c.UnreliableSent);
                Console.WriteLine("CLIENT STATS:\n" + c.Stats);
            }
        }
    }
}
