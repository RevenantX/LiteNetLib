using System.Collections.Generic;
using System.Text;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture]
    public class LiteNetLibTest
    {
        [SetUp]
        public void Init()
        {
            _serverListener = new EventBasedNetListener();
            _clientListener1 = new EventBasedNetListener();
            _clientListener2 = new EventBasedNetListener();
            _clientListener3 = new EventBasedNetListener();
            _clientListener4 = new EventBasedNetListener();

            _server = new NetManager(_serverListener, 60, ServerName);
            _client1 = new NetManager(_clientListener1, ServerName);
            _client2 = new NetManager(_clientListener2, ServerName);
            _client3 = new NetManager(_clientListener3, ServerName);
            _client4 = new NetManager(_clientListener4, ServerName);

            if (!_server.Start(Port))
            {
                Assert.Fail("Server start failed");
            }
            Thread.Yield();

            if (!_client1.Start())
            {
                Assert.Fail("Client1 start failed");
            }
            Thread.Yield();

            if (!_client2.Start())
            {
                Assert.Fail("Client2 start failed");
            }
            Thread.Yield();

            if (!_client3.Start())
            {
                Assert.Fail("Client3 start failed");
            }
            Thread.Yield();

            if (!_client4.Start())
            {
                Assert.Fail("Client4 start failed");
            }
            Thread.Yield();
        }

        [TearDown]
        public void TearDown()
        {
            _client1.Stop();
            _client2.Stop();
            _server.Stop();
        }

        private const int Port = 9050;
        private const string ServerName = "test_server";

        private EventBasedNetListener _serverListener;
        private EventBasedNetListener _clientListener1;
        private EventBasedNetListener _clientListener2;
        private EventBasedNetListener _clientListener3;
        private EventBasedNetListener _clientListener4;

        private NetManager _server;
        private NetManager _client1;
        private NetManager _client2;
        private NetManager _client3;
        private NetManager _client4;

        [Test]
        [Timeout(2000)]
        public void ConnectionByIpV4()
        {
            _client1.Connect("127.0.0.1", Port);

            while (_server.PeersCount != 1)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(_server.PeersCount, 1);
            Assert.AreEqual(_client1.PeersCount, 1);
        }

        [Test]
        [Timeout(2000)]
        public void ConnectionByIpV6()
        {
            _client1.Connect("::1", Port);

            while (_server.PeersCount != 1)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(_server.PeersCount, 1);
            Assert.AreEqual(_client1.PeersCount, 1);
        }

        [Test]
        [Timeout(2000)]
        public void DiscoveryBroadcastTest()
        {
            _server.DiscoveryEnabled = true;

            var writer = new NetDataWriter();
            writer.Put("Client request");

            _serverListener.NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                var serverWriter = new NetDataWriter();
                writer.Put("Server reponse");
                _server.SendDiscoveryResponse(serverWriter, point);
            };

            _clientListener1.NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                if (type == UnconnectedMessageType.DiscoveryResponse)
                {
                    _client1.Connect(point);
                }
            };
            _clientListener2.NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                if (type == UnconnectedMessageType.DiscoveryResponse)
                {
                    _client2.Connect(point);
                }
            };
            _clientListener3.NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                if (type == UnconnectedMessageType.DiscoveryResponse)
                {
                    _client3.Connect(point);
                }
            };
            _clientListener4.NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                if (type == UnconnectedMessageType.DiscoveryResponse)
                {
                    _client4.Connect(point);
                }
            };

            _client1.SendDiscoveryRequest(writer, Port);
            _client2.SendDiscoveryRequest(writer, Port);
            _client3.SendDiscoveryRequest(writer, Port);
            _client4.SendDiscoveryRequest(writer, Port);

            while (_server.PeersCount != 4)
            {
                _server.PollEvents();
                _client1.PollEvents();
                _client2.PollEvents();
                _client3.PollEvents();
                _client4.PollEvents();
                Thread.Sleep(15);
            }

            Assert.AreEqual(_server.PeersCount, 4);
            Assert.AreEqual(_client1.PeersCount, 1);
            Assert.AreEqual(_client2.PeersCount, 1);
            Assert.AreEqual(_client3.PeersCount, 1);
            Assert.AreEqual(_client4.PeersCount, 1);
        }

        [Test]
        [Timeout(2000)]
        public void SendRawDataToAll()
        {
            _client1.Connect("127.0.0.1", Port);
            _client2.Connect("127.0.0.1", Port);
            _client3.Connect("127.0.0.1", Port);
            _client4.Connect("127.0.0.1", Port);

            while (_server.PeersCount != 4)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(_server.PeersCount, 4);
            Assert.AreEqual(_client1.PeersCount, 1);
            Assert.AreEqual(_client2.PeersCount, 1);
            Assert.AreEqual(_client3.PeersCount, 1);
            Assert.AreEqual(_client4.PeersCount, 1);

            var dataStack = new Stack<byte[]>(4);

            _clientListener1.NetworkReceiveEvent += (peer, reader) => dataStack.Push(reader.Data);
            _clientListener2.NetworkReceiveEvent += (peer, reader) => dataStack.Push(reader.Data);
            _clientListener3.NetworkReceiveEvent += (peer, reader) => dataStack.Push(reader.Data);
            _clientListener4.NetworkReceiveEvent += (peer, reader) => dataStack.Push(reader.Data);

            var data = Encoding.Default.GetBytes("TextForTest");
            _server.SendToAll(data, SendOptions.ReliableUnordered);

            while (dataStack.Count != 4)
            {
                _client1.PollEvents();
                _client2.PollEvents();
                _client3.PollEvents();
                _client4.PollEvents();
                Thread.Sleep(10);
            }

            Assert.AreEqual(dataStack.Count, 4);

            Assert.AreEqual(_server.PeersCount, 4);
            Assert.AreEqual(_client1.PeersCount, 1);
            Assert.AreEqual(_client2.PeersCount, 1);
            Assert.AreEqual(_client3.PeersCount, 1);
            Assert.AreEqual(_client4.PeersCount, 1);

            Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
        }
    }
}