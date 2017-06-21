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
            public NetManager Client;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client {0}] connected to: {1}:{2}", Client.LocalPort, peer.EndPoint.Host, peer.EndPoint.Port);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectInfo.Reason);
            }

            public void OnNetworkError(NetEndPoint endPoint, int error)
            {
                Console.WriteLine("[Client] error! " + error);
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {

            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Client] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
                if (messageType == UnconnectedMessageType.DiscoveryResponse)
                {
                    Client.Connect(remoteEndPoint);
                }
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }
        }

        private class ServerListener : INetEventListener
        {
            public NetManager Server;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
                var peers = Server.GetPeers();
                foreach (var netPeer in peers)
                {
                    Console.WriteLine("ConnectedPeersList: id={0}, ep={1}", netPeer.ConnectId, netPeer.EndPoint);
                }
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectInfo.Reason);
            }

            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
                Console.WriteLine("[Server] error: " + socketErrorCode);
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

        private ClientListener _clientListener1;
        private ClientListener _clientListener2;
        private ServerListener _serverListener;

        public void Run()
        {
            //Server
            _serverListener = new ServerListener();

            NetManager server = new NetManager(_serverListener, 2);
            server.DiscoveryEnabled = true;
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener1 = new ClientListener();

            NetManager client1 = new NetManager(_clientListener1);
            _clientListener1.Client = client1;
            client1.SimulateLatency = true;
            client1.SimulationMaxLatency = 1500;
            if (!client1.Start())
            {
                Console.WriteLine("Client1 start failed");

                return;
            }

            _clientListener2 = new ClientListener();
            NetManager client2 = new NetManager(_clientListener2);
            _clientListener2.Client = client2;
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
