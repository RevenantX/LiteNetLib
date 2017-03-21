using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using NUnit.Framework;

namespace LiteNetLibUnitTests
{
    [TestFixture, SingleThreaded]
    public class LiteNetLibTest
    {
        [Test]
        public void ConnectionByIpV4()
        {
            var serverListener = new EventBasedNetListener();
            var server = new NetManager(serverListener, "IpV4Test");

            if (!server.Start(9050))
            {
                Assert.Fail("Server start failed");
            }

            var clientListener = new EventBasedNetListener();
            var client = new NetManager(clientListener, "IpV4Test")
            {
                SimulationMaxLatency = 1500,
                MergeEnabled = true
            };
            
            if (!client.Start())
            {
                Assert.Fail("Client1 start failed");
            }

            bool clietnConnected = false;
            serverListener.PeerConnectedEvent += peer =>
            {
                //TODO: Identify user
                clietnConnected = true;
            };

            client.Connect("127.0.0.1", 9050);

            while (!clietnConnected)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
        }

        [Test]
        public void ConnectionByIpV6()
        {
            var serverListener = new EventBasedNetListener();
            var server = new NetManager(serverListener, "IpV6");

            if (!server.Start(9051))
            {
                Assert.Fail("Server start failed");
            }

            var clientListener = new EventBasedNetListener();
            var client = new NetManager(clientListener, "IpV6")
            {
                SimulationMaxLatency = 1500,
                MergeEnabled = true
            };

            if (!client.Start())
            {
                Assert.Fail("Client1 start failed");
            }

            bool clietnConnected = false;
            serverListener.PeerConnectedEvent += peer =>
            {
                //TODO: Identify user
                clietnConnected = true;
            };

            client.Connect("::1", 9051);

            while (!clietnConnected)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
        }
    }
}
