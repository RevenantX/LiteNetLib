using System;
using System.Threading;
using LiteNetLib;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture, SingleThreaded]
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

        [Test]
        public void ConnectionByIpV4()
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
            
            foreach (var netPeer in _client1.GetPeers())
            {
                _client1.DisconnectPeer(netPeer);
            }
            
            _clientListener1.PeerConnectedEvent -= action;
        }

        [Test]
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

            foreach (var netPeer in _client1.GetPeers())
            {
                _client1.DisconnectPeer(netPeer);
            }

            _clientListener1.PeerConnectedEvent -= action;
        }
    }
}
