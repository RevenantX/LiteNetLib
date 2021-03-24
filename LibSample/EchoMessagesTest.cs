using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    class EchoMessagesTest : IExample
    {
        private static int _messagesReceivedCount = 0;

        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Address, peer.EndPoint.Port);

                NetDataWriter dataWriter = new NetDataWriter();
                for (int i = 0; i < 5; i++)
                {
                    dataWriter.Reset();
                    dataWriter.Put(0);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, DeliveryMethod.ReliableUnordered);

                    dataWriter.Reset();
                    dataWriter.Put(1);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, DeliveryMethod.ReliableOrdered);

                    dataWriter.Reset();
                    dataWriter.Put(2);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, DeliveryMethod.Sequenced);

                    dataWriter.Reset();
                    dataWriter.Put(3);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, DeliveryMethod.Unreliable);

                    dataWriter.Reset();
                    dataWriter.Put(4);
                    dataWriter.Put(i);
                    peer.Send(dataWriter, DeliveryMethod.ReliableSequenced);
                }

                //And test fragment
                byte[] testData = new byte[13218];
                testData[0] = 192;
                testData[13217] = 31;
                peer.Send(testData, DeliveryMethod.ReliableOrdered);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectInfo.Reason);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
            {
                Console.WriteLine("[Client] error! " + socketErrorCode);
            }

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                if (reader.AvailableBytes == 13218)
                {
                    Console.WriteLine("[{0}] TestFrag: {1}, {2}",
                        peer.NetManager.LocalPort,
                        reader.RawData[reader.UserDataOffset],
                        reader.RawData[reader.UserDataOffset + 13217]);
                }
                else
                {
                    int type = reader.GetInt();
                    int num = reader.GetInt();
                    _messagesReceivedCount++;
                    Console.WriteLine("[{0}] CNT: {1}, TYPE: {2}, NUM: {3}, MTD: {4}", peer.NetManager.LocalPort, _messagesReceivedCount, type, num, deliveryMethod);
                }
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {

            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnConnectionRequest(ConnectionRequest request)
            {

            }
        }

        private class ServerListener : INetEventListener
        {
            public NetManager Server;

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Server] Peer connected: " + peer.EndPoint);
                foreach (var netPeer in Server)
                {
                    if(netPeer.ConnectionState == ConnectionState.Connected)
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

            public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                //echo
                peer.Send(reader.GetRemainingBytes(), deliveryMethod);

                //fragment log
                if (reader.AvailableBytes == 13218)
                {
                    Console.WriteLine("[Server] TestFrag: {0}, {1}",
                        reader.RawData[reader.UserDataOffset],
                        reader.RawData[reader.UserDataOffset + 13217]);
                }
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
                Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }

            public void OnConnectionRequest(ConnectionRequest request)
            {
                var acceptedPeer = request.AcceptIfKey("gamekey");
                Console.WriteLine("[Server] ConnectionRequest. Ep: {0}, Accepted: {1}",
                    request.RemoteEndPoint,
                    acceptedPeer != null);
            }
        }

        private ClientListener _clientListener;
        private ServerListener _serverListener;
        private const int Port = 9050;

        public void Run()
        {
            Console.WriteLine("=== Echo Messages Test ===");
            //Server
            _serverListener = new ServerListener();

            NetManager server = new NetManager(_serverListener);

            if (!server.Start(Port))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetManager client1 = new NetManager(_clientListener)
            {
                SimulationMaxLatency = 1500,
                //SimulateLatency = true,
            };
            //client1
            if (!client1.Start())
            {
                Console.WriteLine("Client1 start failed");
                return;
            }
            client1.Connect("127.0.0.1", Port, "gamekey");

            NetManager client2 = new NetManager(_clientListener)
            {
                //SimulateLatency = true,
                SimulationMaxLatency = 1500
            };

            client2.Start();
            client2.Connect("::1", Port, "gamekey");

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
            Console.WriteLine("ServStats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                server.Statistics.BytesReceived,
                server.Statistics.PacketsReceived,
                server.Statistics.BytesSent,
                server.Statistics.PacketsSent);
            Console.WriteLine("Client1Stats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                client1.Statistics.BytesReceived,
                client1.Statistics.PacketsReceived,
                client1.Statistics.BytesSent,
                client1.Statistics.PacketsSent);
            Console.WriteLine("Client2Stats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                client2.Statistics.BytesReceived,
                client2.Statistics.PacketsReceived,
                client2.Statistics.BytesSent,
                client2.Statistics.PacketsSent);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
