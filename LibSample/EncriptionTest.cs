using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Encryption;
using LiteNetLib.Utils;

namespace LibSample
{
    public class EncryptionTest
    {
        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);

                NetDataWriter dataWriter = new NetDataWriter();

                dataWriter.Put(191992949L);
                dataWriter.Put(Math.PI);
                dataWriter.Put("Hello, world!", 50);
                peer.Send(dataWriter, SendOptions.ReliableOrdered);
            }

            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
                Console.WriteLine("[Client] error! " + socketErrorCode);
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {

            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {

            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectInfo.Reason);
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

            public void OnNetworkError(NetEndPoint endPoint, int socketErrorCode)
            {
                Console.WriteLine("[Server] error: " + socketErrorCode);
            }

            public void OnNetworkReceive(NetPeer peer, NetDataReader reader)
            {
                var resultLong = reader.GetLong();
                var resultDouble = reader.GetDouble();
                var resultString = reader.GetString(50);
                Console.WriteLine(
                    "[Server] Result: Long[{0}] Double[{1}] String[{2}]",
                    resultLong,
                    resultDouble,
                    resultString);
            }

            public void OnNetworkReceiveUnconnected(NetEndPoint remoteEndPoint, NetDataReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectInfo.Reason);
            }
        }

        private ClientListener _clientListener;
        private ServerListener _serverListener;

        public void Run()
        {
            //Server
            _serverListener = new ServerListener();
            NetEncryption encryption = new NetXorEncryption("te123we");

            NetManager server = new NetManager(_serverListener, 2, "encription");
            server.EnableEncryption(encryption);
            
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetManager client = new NetManager(_clientListener, "encription");
            client.EnableEncryption(encryption);
            
            //client1.SimulateLatency = true;
            client.SimulationMaxLatency = 1500;
            client.MergeEnabled = true;
            if (!client.Start())
            {
                Console.WriteLine("Client start failed");
                return;
            }
            client.Connect("127.0.0.1", 9050);

            while (!Console.KeyAvailable)
            {
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }

            client.Stop();
            server.Stop();

            Console.ReadKey();
            Console.WriteLine("ServStats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                server.BytesReceived,
                server.PacketsReceived,
                server.BytesSent,
                server.PacketsSent);
            Console.WriteLine("Client1Stats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                client.BytesReceived,
                client.PacketsReceived,
                client.BytesSent,
                client.PacketsSent);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}