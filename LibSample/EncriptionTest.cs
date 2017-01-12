using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Encryption;
using LiteNetLib.Utils;

namespace LibSample
{
    public class EncriptionTest
    {
        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);

                NetDataWriter dataWriter = new NetDataWriter();

                dataWriter.Put(199999999L);
                dataWriter.Put(Math.PI);
                dataWriter.Put("Hello, world!", 50);
                peer.Send(dataWriter, SendOptions.ReliableOrdered);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectReason);
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

            public void OnPeerDisconnected(NetPeer peer, DisconnectReason disconnectReason, int socketErrorCode)
            {
                Console.WriteLine("[Server] Peer disconnected: " + peer.EndPoint + ", reason: " + disconnectReason);
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
        }

        private ClientListener _clientListener;
        private ServerListener _serverListener;

        public void Run()
        {
            //Server
            _serverListener = new ServerListener();

            NetServer server = new NetServer(_serverListener, 2, "encription", new NetTripleDESEncryption("te123we"));
            
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetClient client = new NetClient(_clientListener, "encription", new NetTripleDESEncryption("te123we"));
            
            //client1.SimulateLatency = true;
            client.SimulationMaxLatency = 1500;
            client.MergeEnabled = true;
            if (!client.Start())
            {
                Console.WriteLine("Client1 start failed");
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