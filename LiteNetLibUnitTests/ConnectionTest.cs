using System;
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
        private EventBasedNetListener _serverListener = new EventBasedNetListener();
        private EventBasedNetListener _clientListener1 = new EventBasedNetListener();
        private EventBasedNetListener _clientListener2 = new EventBasedNetListener();

        private NetManager _server;
        private NetManager _client1;
        private NetManager _client2;

        private const string ServerName = "test_server";

        [SetUp]
        public void Init()
        {
            _server = new NetManager(_serverListener, ServerName);
            _client1 = new NetManager(_clientListener1, ServerName);
            _client2 = new NetManager(_clientListener2, ServerName);

            if (!_server.Start(9050))
            {
                Assert.Fail("Server start failed");
            }
            
            if (!_client1.Start())
            {
                Assert.Fail("Client1 start failed");
            }

            if (!_client2.Start())
            {
                Assert.Fail("Client2 start failed");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _client1.Stop();
            _client2.Stop();
            _server.Stop();
        }

        [Test, Timeout(2000)]
        public void ConnectionByIpV4()
        {
            _server.MaxConnectAttempts = 2;
            bool connected = false;
            EventBasedNetListener.OnPeerConnected action = peer =>
            {
                //TODO: Identify user
                connected = true;
            };
           
            _serverListener.PeerConnectedEvent += action;

            _client1.Connect("127.0.0.1", 9050);

            while (!connected)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(connected, true);
            Assert.AreEqual(_server.PeersCount, 1);
            Assert.AreEqual(_client1.PeersCount, 1);

            foreach (var netPeer in _client1.GetPeers())
            {
                _client1.DisconnectPeer(netPeer);
            }
            
            _clientListener1.PeerConnectedEvent -= action;
        }

        [Test, Timeout(2000)]
        public void ConnectionByIpV6()
        {
            bool connected = false;
            EventBasedNetListener.OnPeerConnected action = peer =>
            {
                //TODO: Identify user
                connected = true;
            };

            _serverListener.PeerConnectedEvent += action;

            _client1.Connect("::1", 9050);

            while (!connected)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(connected, true);
            Assert.AreEqual(_server.PeersCount, 1);
            Assert.AreEqual(_client1.PeersCount, 1);

            foreach (var netPeer in _client1.GetPeers())
            {
                _client1.DisconnectPeer(netPeer);
            }

            _clientListener1.PeerConnectedEvent -= action;
        }

        [Test, Timeout(2000)]
        public void SendRawDataToAll()
        {
            bool connected = false;
            EventBasedNetListener.OnPeerConnected action = peer =>
            {
                //TODO: Identify user
                connected = true;
            };

            _serverListener.PeerConnectedEvent += action;

            _client1.Connect("127.0.0.1", 9050);

            while (!connected)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(connected, true);
            Assert.AreEqual(_server.PeersCount, 1);
            Assert.AreEqual(_client1.PeersCount, 1);
            Assert.AreEqual(_client2.PeersCount, 0);

            connected = false;

            _client2.Connect("127.0.0.1", 9050);

            while (!connected)
            {
                Thread.Sleep(15);
                _server.PollEvents();
            }

            Assert.AreEqual(connected, true);
            Assert.AreEqual(_server.PeersCount, 2);
            Assert.AreEqual(_client1.PeersCount, 1);
            Assert.AreEqual(_client2.PeersCount, 1);
            
            byte[] data = Encoding.Default.GetBytes("TextForTest");

            _client1.SendToAll(data, SendOptions.ReliableOrdered);

            byte[] recivedData = null;

            _serverListener.NetworkReceiveEvent += (peer, reader) =>
            {
                recivedData = reader.Data;
            };

            while (recivedData == null)
            {
                _server.PollEvents();
                Thread.Sleep(15);
            }

            Assert.That(data, Is.EqualTo(recivedData).AsCollection);
        }
    }
}
