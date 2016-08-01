using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    class BroadcastTest
    {
        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);
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

            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Client] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
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

            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Server] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
                NetDataWriter wrtier = new NetDataWriter();
                wrtier.Put("SERVER DISCOVERY RESPONSE :)");
                Server.SendDiscoveryResponse(wrtier, remoteEndPoint);
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
            server.DiscoveryEnabled = true;
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetClient client1 = new NetClient(_clientListener, "myapp1");
            client1.SimulateLatency = true;
            client1.SimulationMaxLatency = 1500;
            if (!client1.Start())
            {
                Console.WriteLine("Client1 start failed");

                return;
            }

            NetClient client2 = new NetClient(_clientListener, "myapp1");
            client2.SimulateLatency = true;
            client2.SimulationMaxLatency = 1500;
            client2.Start();

            //Send broadcast
            NetDataWriter writer = new NetDataWriter();

            writer.Put("CLIENT 1 DISCOVERY REQUEST");
            client1.SendDiscoveryRequest(writer, 9050);
            writer.Reset();

            writer.Put("CLIENT 2 DISCOVERY REQUEST");
            client2.SendDiscoveryRequest(writer, 9050);

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
