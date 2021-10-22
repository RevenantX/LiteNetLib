using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    class BroadcastTest : IExample
    {
        private class ClientListener : INetEventListener
        {
            public NetManager Client;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client {0}] connected to: {1}:{2}", Client.LocalPort, peer.EndPoint.Address, peer.EndPoint.Port);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectInfo.Reason);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError error)
            {
                Console.WriteLine("[Client] error! " + error);
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod, byte channel)
            {

            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
                var text = reader.GetString(100);
                Console.WriteLine("[Client] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, text);
                if (messageType == UnconnectedMessageType.BasicMessage && text == "SERVER DISCOVERY RESPONSE")
                {
                    Client.Connect(remoteEndPoint, "key");
                }
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.Reject();
            }
        }

        private class ServerListener : INetEventListener
        {
            public NetManager Server;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
                var peers = Server.ConnectedPeerList;
                foreach (var netPeer in peers)
                {
                    Console.WriteLine("ConnectedPeersList: id={0}, ep={1}", netPeer.Id, netPeer.EndPoint);
                }
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectInfo.Reason);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
            {
                Console.WriteLine("[Server] error: " + socketErrorCode);
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod, byte channel)
            {

            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Server] ReceiveUnconnected {0}. From: {1}. Data: {2}", messageType, remoteEndPoint, reader.GetString(100));
                NetDataWriter wrtier = new NetDataWriter();
                wrtier.Put("SERVER DISCOVERY RESPONSE");
                Server.SendUnconnectedMessage(wrtier, remoteEndPoint);
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                request.AcceptIfKey("key");
            }
        }

        private ClientListener _clientListener1;
        private ClientListener _clientListener2;
        private ServerListener _serverListener;

        public void Run()
        {
            Console.WriteLine("=== Broadcast Test ===");
            //Server
            _serverListener = new ServerListener();

            NetManager server = new NetManager(_serverListener)
            {
                BroadcastReceiveEnabled = true,
                IPv6Mode = IPv6Mode.DualMode
            };
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener1 = new ClientListener();

            NetManager client1 = new NetManager(_clientListener1)
            {
                UnconnectedMessagesEnabled = true,
                SimulateLatency = true,
                SimulationMaxLatency = 1500,
                IPv6Mode = IPv6Mode.DualMode
            };
            _clientListener1.Client = client1;
            if (!client1.Start())
            {
                Console.WriteLine("Client1 start failed");

                return;
            }

            _clientListener2 = new ClientListener();
            NetManager client2 = new NetManager(_clientListener2)
            {
                UnconnectedMessagesEnabled = true,
                SimulateLatency = true,
                SimulationMaxLatency = 1500,
                IPv6Mode = IPv6Mode.DualMode
            };

            _clientListener2.Client = client2;
            client2.Start();

            //Send broadcast
            NetDataWriter writer = new NetDataWriter();

            writer.Put("CLIENT 1 DISCOVERY REQUEST");
            client1.SendBroadcast(writer, 9050);
            writer.Reset();

            writer.Put("CLIENT 2 DISCOVERY REQUEST");
            client2.SendBroadcast(writer, 9050);

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
