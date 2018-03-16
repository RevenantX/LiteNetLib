using System.Collections.Generic;
using System.Text;
using System.Threading;

using LiteNetLib.Tests.TestUtility;
using LiteNetLib.Utils;

using NUnit.Framework;

namespace LiteNetLib.Tests
{
    [TestFixture]
    [Category("Communication")]
#if !NETCOREAPP2_0
    [Timeout(10000)]
#endif
    public class CommunicationTest
    {
        [SetUp]
        public void Init()
        {
            ManagerStack = new NetManagerStack(DefaultAppKey, DefaultPort);
        }

        [TearDown]
        public void TearDown()
        {
            ManagerStack?.Dispose();
        }

        private const int DefaultPort = 9050;
        private const string DefaultAppKey = "test_server";

        public NetManagerStack ManagerStack { get; set; }

        [Test, MaxTime(2000)]
        public void ConnectionByIpV4()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.PeersCount, 1);
            Assert.AreEqual(client.PeersCount, 1);
        }

        [Test, MaxTime(10000)]
        public void ConnectionFailedTest()
        {
            NetManager client = ManagerStack.Client(1);

            var result = false;
            DisconnectInfo disconnectInfo = default(DisconnectInfo);

            ManagerStack.ClientListener(1).PeerConnectedEvent += peer =>
            {
                result = true;
            };
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => 
            {
                result = true;
                disconnectInfo = info;
            };

            client.Connect("127.0.0.2", DefaultPort, DefaultAppKey);

            while (!result)
            {
                Thread.Sleep(15);
                client.PollEvents();
            }

            Assert.True(result);
            Assert.AreEqual(DisconnectReason.ConnectionFailed, disconnectInfo.Reason);
        }

        [Test, MaxTime(10000)]
        public void NetPeerDisconnectTimeout()
        {
            NetManager client = ManagerStack.Client(1);
            NetManager server = ManagerStack.Server(1);

            //Default 5 sec timeout for local network is too mach, set 1 for test
            server.DisconnectTimeout = 1000;

            NetPeer clientServerPeer = client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (clientServerPeer.ConnectionState != ConnectionState.Connected)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.AreEqual(ConnectionState.Connected, clientServerPeer.ConnectionState);
            Assert.True(server.PeersCount == 1);

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(clientServerPeer, peer);
                Assert.AreEqual(DisconnectReason.Timeout, info.Reason);
            };

            server.Stop();
            
            Assert.True(server.PeersCount == 0);
            Assert.True(client.PeersCount == 1);

            while (client.PeersCount == 1)
            {
                Thread.Sleep(15);
            }
        }


     
        [Test, MaxTime(10000)]
        public void NetPeerDisconnectAll()
        {
//TODO: Timeout attribute not work in netcoreapp
#if !NETCOREAPP2_0
            NetManager client = ManagerStack.Client(1);
            NetManager server = ManagerStack.Server(1);

            NetPeer clientServerPeer = client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (clientServerPeer.ConnectionState != ConnectionState.Connected)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.AreEqual(ConnectionState.Connected, clientServerPeer.ConnectionState);
            Assert.True(server.PeersCount == 1);

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(clientServerPeer, peer);
                Assert.AreEqual(DisconnectReason.RemoteConnectionClose, info.Reason);
            };

            server.DisconnectAll();
            while (client.GetPeersCount(ConnectionState.Connected) != 0)
            {
                Thread.Sleep(15);
                client.PollEvents();
            }
            server.Stop();

            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(0, client.GetPeersCount(ConnectionState.Connected));
#else
            Assert.Pass();
#endif
        }

        [Test, MaxTime(2000)]
        public void DisconnectTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            bool disconnected = false;
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                var bytes = info.AdditionalData.GetRemainingBytes();
                Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, bytes);
                disconnected = true;
            };
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
            server.DisconnectPeer(server.GetFirstPeer(), new byte[] {1,2,3,4});
            while (!disconnected)
            {
                client.PollEvents();
            }
            Assert.True(disconnected);
        }

        [Test, MaxTime(2000)]
        public void ConnectionByIpV6()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("::1", DefaultPort, DefaultAppKey);

            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.PeersCount, 1);
            Assert.AreEqual(client.PeersCount, 1);
        }

        [Test, MaxTime(2000)]
        public void DiscoveryBroadcastTest()
        {
            var server = ManagerStack.Server(1);
            var clientCount = 10;

            server.DiscoveryEnabled = true;

            var writer = new NetDataWriter();
            writer.Put("Client request");

            ManagerStack.ServerListener(1).NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                var serverWriter = new NetDataWriter();
                writer.Put("Server reponse");
                server.SendDiscoveryResponse(serverWriter, point);
            };

            for (ushort i = 1; i <= clientCount; i++)
            {
                var cache = i;
                ManagerStack.ClientListener(i).NetworkReceiveUnconnectedEvent += (point, reader, type) =>
                {
                    Assert.AreEqual(type, UnconnectedMessageType.DiscoveryResponse);
                    ManagerStack.Client(cache).Connect(point, DefaultAppKey);
                };
            }

            ManagerStack.ClientForeach((i, manager, l) => manager.SendDiscoveryRequest(writer, DefaultPort));

            while (server.PeersCount < clientCount)
            {
                server.PollEvents();
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(15);
            }

            Assert.AreEqual(clientCount, server.PeersCount);
            ManagerStack.ClientForeach(
                (i, manager, l) =>
                {
                    Assert.AreEqual(manager.PeersCount, 1);
                });
        }

        [Test]
        public void HelperManagerStackTest()
        {
            Assert.AreEqual(ManagerStack.Client(1), ManagerStack.Client(1));
            Assert.AreNotEqual(ManagerStack.Client(1), ManagerStack.Client(2));
            Assert.AreEqual(ManagerStack.Client(2), ManagerStack.Client(2));

            Assert.AreEqual(ManagerStack.Server(1), ManagerStack.Server(1));
            Assert.AreNotEqual(ManagerStack.Server(1), ManagerStack.Client(1));
            Assert.AreNotEqual(ManagerStack.Server(1), ManagerStack.Client(2));
        }

        [Test]
        public void SendRawDataToAll()
        {
            var clientCount = 10;

            var server = ManagerStack.Server(1);

            for (ushort i = 1; i <= clientCount; i++)
            {
                ManagerStack.Client(i).Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            }

            while (server.PeersCount < clientCount)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.PeersCount, clientCount);
            ManagerStack.ClientForeach((i, manager, l) => Assert.AreEqual(manager.PeersCount, 1));

            var dataStack = new Stack<byte[]>(clientCount);

            ManagerStack.ClientForeach(
                (i, manager, l) => l.NetworkReceiveEvent += (peer, reader, type) => dataStack.Push(reader.Data));

            var data = Encoding.Default.GetBytes("TextForTest");
            server.SendToAll(data, DeliveryMethod.ReliableUnordered);

            while (dataStack.Count < clientCount)
            {
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(10);
            }

            Assert.AreEqual(dataStack.Count, clientCount);

            Assert.AreEqual(server.PeersCount, clientCount);
            for (ushort i = 1; i <= clientCount; i++)
            {
                Assert.AreEqual(ManagerStack.Client(i).PeersCount, 1);
                Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            }
        }
    }
}