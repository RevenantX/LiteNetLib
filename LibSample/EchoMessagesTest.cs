using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    class EchoMessagesTest
    {
        private static int _messagesReceivedCount = 0;

        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);

                NetDataWriter dataWriter = new NetDataWriter();
                for (int i = 0; i < 5; i++)
                {
                    dataWriter.Reset();
                    dataWriter.Put(0);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, SendOptions.ReliableUnordered);

                    dataWriter.Reset();
                    dataWriter.Put(1);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, SendOptions.ReliableOrdered);

                    dataWriter.Reset();
                    dataWriter.Put(2);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, SendOptions.Sequenced);

                    dataWriter.Reset();
                    dataWriter.Put(3);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, SendOptions.Unreliable);
                }

                //And test fragment
                byte[] testData = new byte[13218];
                testData[0] = 192;
                testData[13217] = 31;
                peer.Send(testData, SendOptions.ReliableOrdered);
            }

            public void OnPeerDisconnected(NetPeer peer, string additionalInfo)
            {
                Console.WriteLine("[Client] disconnected: " + additionalInfo);
            }

            public void OnNetworkError(NetEndPoint endPoint, string error)
            {
                Console.WriteLine("[Client] error! " + error);
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                if (reader.AvailableBytes == 13218)
                {
                    Console.WriteLine("[{0}] TestFrag: {1}, {2}", peer.Handler.LocalEndPoint.Port, reader.Data[0], reader.Data[13217]);
                }
                else
                {
                    int type = reader.GetInt();
                    int num = reader.GetInt();
                    _messagesReceivedCount++;
                    Console.WriteLine("[{0}] CNT: {1}, TYPE: {2}, NUM: {3}", peer.Handler.LocalEndPoint.Port, _messagesReceivedCount, type, num);
                }
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader)
            {

            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {
                
            }
        }

        private class ServerListener : INetEventListener
        {
            public NetServer Server;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
                var peers = Server.GetPeers();
                foreach (var netPeer in peers)
                {
                    Console.WriteLine("ConnectedPeersList: id={0}, ep={1}", netPeer.Id, netPeer.EndPoint);
                }
            }

            public void OnPeerDisconnected(NetPeer peer, string additionalInfo)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + additionalInfo);
            }

            public void OnNetworkError(NetEndPoint endPoint, string error)
            {
                Console.WriteLine("[Server] error: " + error);
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                //echo
                peer.Send(reader.Data, SendOptions.ReliableUnordered);

                //fragment log
                if (reader.AvailableBytes == 13218)
                {
                    Console.WriteLine("[Server] TestFrag: {0}, {1}", reader.Data[0], reader.Data[13217]);
                }
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader)
            {
                Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }
        }

        private ClientListener _clientListener;
        private ServerListener _serverListener;

        public void Run()
        {
            //Server
            _serverListener = new ServerListener();

            NetServer server = new NetServer(_serverListener, 2, "myapp1");
            server.Start(9050);
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetClient client1 = new NetClient(_clientListener);
            client1.Start();
            client1.Connect("localhost", 9050, "myapp1");
            client1.SimulateLatency = true;
            client1.SimulationMaxLatency = 1500;

            NetClient client2 = new NetClient(_clientListener);
            client2.Start();
            client2.Connect("localhost", 9050, "myapp1");

            while (!Console.KeyAvailable)
            {
                client1.PollEvents();
                client2.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }

            client1.Stop();
            client2.Stop();
            server.Stop();
            Console.ReadKey();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
