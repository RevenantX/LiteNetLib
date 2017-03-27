using System.Collections.Generic;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibUnitTests.Helper;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture, Timeout(2000)]
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
        public void ConnectionByIpV4()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("127.0.0.1", DefaultPort);

            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.PeersCount, 1);
            Assert.AreEqual(client.PeersCount, 1);
        }

        [Test]
        public void ConnectionByIpV6()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("::1", DefaultPort);

            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.PeersCount, 1);
            Assert.AreEqual(client.PeersCount, 1);
        }

        [Test]
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
                    ManagerStack.Client(cache).Connect(point);
                };
            }

            ManagerStack.ClientForeach((i, manager, l) => manager.SendDiscoveryRequest(writer, DefaultPort));

            while (server.PeersCount < clientCount)
            {
                server.PollEvents();
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(15);
            }

            Assert.AreEqual(server.PeersCount, clientCount);
            ManagerStack.ClientForeach(
                (i, manager, l) =>
                {
                    Assert.AreEqual(manager.PeersCount, 1);
                });
        }

        [Test]
        public void SendRawDataToAll()
        {
            var clientCount = 10;

            var server = ManagerStack.Server(1);
            
            for (ushort i = 1; i <= clientCount; i++)
            {
                ManagerStack.Client(i).Connect("127.0.0.1", DefaultPort);
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
                (i, manager, l) => l.NetworkReceiveEvent += (peer, reader) => dataStack.Push(reader.Data));
            
            var data = Encoding.Default.GetBytes("TextForTest");
            server.SendToAll(data, SendOptions.ReliableUnordered);

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