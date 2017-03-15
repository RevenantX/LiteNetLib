using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace LibSample
{
    class SerializerTest : IRunnable
    {
        [Serializable] //Just for test binary formatter
        private class SamplePacket
        {
            public string SomeString { get; set; }
            public float SomeFloat { get; set; }
            public int[] SomeIntArray { get; set; }
            public SomeVector2 SomeVector2 { get; set; }
            public SomeVector2[] SomeVectors { get; set; }
            public string EmptyString { get; set; }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("SomeString: " + SomeString);
                sb.AppendLine("SomeFloat: " + SomeFloat);
                sb.AppendLine("SomeIntArray: ");
                for (int i = 0; i < SomeIntArray.Length; i++)
                {
                    sb.AppendLine(" " + SomeIntArray[i]);
                }
                sb.AppendLine("SomeVector2 X: " + SomeVector2);
                sb.AppendLine("SomeVectors: ");
                for (int i = 0; i < SomeVectors.Length; i++)
                {
                    sb.AppendLine(" " + SomeVectors[i]);
                }
                sb.AppendLine("EmptyString: " + EmptyString);
                return sb.ToString();
            }
        }

        [Serializable] //Just for test binary formatter
        private struct SomeVector2
        {
            public int X;
            public int Y;

            public SomeVector2(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override string ToString()
            {
                return "X: " + X + ", Y: " + Y;
            }

            public static void Serialize(NetDataWriter writer, SomeVector2 vector)
            {
                writer.Put(vector.X);
                writer.Put(vector.Y);
            }

            public static SomeVector2 Deserialize(NetDataReader reader)
            {
                SomeVector2 res = new SomeVector2();
                res.X = reader.GetInt();
                res.Y = reader.GetInt();
                return res;
            }
        }

        private class ClientListener : INetEventListener
        {
            private readonly NetSerializer _serializer;

            public ClientListener()
            {
                _serializer = new NetSerializer();
                _serializer.RegisterCustomType( SomeVector2.Serialize, SomeVector2.Deserialize );
            }

            public void OnPeerConnected(NetPeer peer)
            {
                Console.WriteLine("[Client] connected to: {0}:{1}", peer.EndPoint.Host, peer.EndPoint.Port);
                SamplePacket sp = new SamplePacket
                {
                    SomeFloat = 3.42f,
                    SomeIntArray = new[] {6, 5, 4},
                    SomeString = "Test String",
                    SomeVector2 = new SomeVector2(4, 5),
                    SomeVectors = new[] {new SomeVector2(1, 2), new SomeVector2(3, 4)}
                };
                Console.WriteLine("Sending to server:\n" + sp );
                peer.Send(_serializer.Serialize(sp), SendOptions.ReliableOrdered);
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
            private readonly NetSerializer _netSerializer;

            public ServerListener()
            {
                _netSerializer = new NetSerializer();
                _netSerializer.RegisterCustomType( SomeVector2.Serialize, SomeVector2.Deserialize );
                _netSerializer.Subscribe<SamplePacket>(OnSamplePacketReceived);
            }

            private void OnSamplePacketReceived(SamplePacket samplePacket)
            {
                Console.WriteLine("[Server] ReceivedPacket:\n" + samplePacket);
            }

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
                Console.WriteLine("[Server] received data. Processing...");
                _netSerializer.ReadAllPackets(reader, true);
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
            const int LoopLength = 100000;
            //Test serializer performance
            Stopwatch stopwatch = new Stopwatch();
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            MemoryStream memoryStream = new MemoryStream();
            NetDataWriter netDataWriter = new NetDataWriter();

            SamplePacket samplePacket = new SamplePacket
            {
                SomeFloat = 0.3f,
                SomeString = "TEST",
                SomeIntArray = new [] { 1, 2, 3 },
                SomeVector2 = new SomeVector2(1, 2),
                SomeVectors = new [] { new SomeVector2(3,4), new SomeVector2(5,6) }
            };

            NetSerializer netSerializer = new NetSerializer();
            netSerializer.RegisterCustomType( SomeVector2.Serialize, SomeVector2.Deserialize );

            //Prewarm cpu
            for (int i = 0; i < 10000000; i++)
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
            Console.WriteLine("NetSerializer first run time: " + stopwatch.ElapsedMilliseconds + " ms");

            //Test NetSerializer
            netDataWriter.Reset();
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
            {
                netSerializer.Serialize(netDataWriter, samplePacket);
            }
            stopwatch.Stop();
            Console.WriteLine("NetSerializer second run time: " + stopwatch.ElapsedMilliseconds + " ms");

            //Test RAW
            netDataWriter.Reset();
            stopwatch.Restart();
            for (int i = 0; i < LoopLength; i++)
            {
                netDataWriter.Put(samplePacket.SomeFloat);
                netDataWriter.Put(samplePacket.SomeString);
                netDataWriter.Put(samplePacket.SomeIntArray);
                netDataWriter.Put(samplePacket.SomeVector2.X);
                netDataWriter.Put(samplePacket.SomeVector2.Y);
                netDataWriter.Put(samplePacket.SomeVectors.Length);
                for (int j = 0; j < samplePacket.SomeVectors.Length; j++)
                {
                    netDataWriter.Put(samplePacket.SomeVectors[j].X);
                    netDataWriter.Put(samplePacket.SomeVectors[j].Y);
                }
                netDataWriter.Put(samplePacket.EmptyString);
            }
            stopwatch.Stop();
            Console.WriteLine("DataWriter (raw put method calls) time: " + stopwatch.ElapsedMilliseconds + " ms");
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
        }
    }
}
