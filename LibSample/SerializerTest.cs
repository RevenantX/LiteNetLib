using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace LibSample
{
    class SerializerTest
    {
        [Serializable] //Just for test binary formatter
        private struct SamplePacket
        {
            public string SomeString { get; set; }
            public float SomeFloat { get; set; }
        }

        private class ClientListener : INetEventListener
        {
            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);
            }

            public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
            {
                Console.WriteLine("[Client] disconnected: " + disconnectInfo.Reason);
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
                Console.WriteLine("[Server] ReceiveUnconnected: {0}", reader.GetString(100));
            }

            public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
            {

            }
        }

        private ClientListener _clientListener;
        private ServerListener _serverListener;

        private void TestPerformance()
        {
            const int LoopLength = 1000000;
            //Test serializer performance
            Stopwatch stopwatch = new Stopwatch();
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            NetDataWriter netDataWriter = new NetDataWriter();

            SamplePacket samplePacket = new SamplePacket
            {
                SomeFloat = 0.3f,
                SomeString = "TEST"
            };

            NetSerializer netSerializer = new NetSerializer();

            //Prewarm cpu
            for(int i = 0; i < 10000000; i++)
            {
                double c = Math.Sin(i);
            }

            //Test binary formatter
            stopwatch.Start();
            for (int i = 0; i < LoopLength; i++)
            {
                binaryFormatter.Serialize(memoryStream, samplePacket);
            }
            stopwatch.Stop();
            Console.WriteLine("BinaryFormatter time: " + stopwatch.ElapsedMilliseconds + " ms");

            //Test NetSerializer
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
            {
                netSerializer.Serialize(netDataWriter, samplePacket);
            }
            stopwatch.Stop();
            Console.WriteLine("NetSerializer time: " + stopwatch.ElapsedMilliseconds + " ms");

            //Test RAW
            netDataWriter.Reset();
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
            {
                netDataWriter.Put(samplePacket.SomeFloat);
                netDataWriter.Put(samplePacket.SomeString);
            }
            stopwatch.Stop();
            Console.WriteLine("DataWriter time: " + stopwatch.ElapsedMilliseconds + " ms");
        }

        public void Run()
        {
            TestPerformance();

            //Server
            _serverListener = new ServerListener();

            NetManager server = new NetManager(_serverListener, 2, "myapp1");
            if (!server.Start(9050))
            {
                Console.WriteLine("Server start failed");
                Console.ReadKey();
                return;
            }
            _serverListener.Server = server;

            //Client
            _clientListener = new ClientListener();

            NetManager client = new NetManager(_clientListener, "myapp1");
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
            Console.WriteLine("ClientStats:\n BytesReceived: {0}\n PacketsReceived: {1}\n BytesSent: {2}\n PacketsSent: {3}",
                client.BytesReceived,
                client.PacketsReceived,
                client.BytesSent,
                client.PacketsSent);
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
