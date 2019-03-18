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

            while (server.PeersCount != 1 || client.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(1, server.PeersCount);
            Assert.AreEqual(1, client.PeersCount);
        }

        [Test, MaxTime(2000)]
        public void PeerNotFoundTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            DisconnectInfo? disconnectInfo = null;
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => disconnectInfo = info;
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.PeersCount != 1 || client.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
            server.Stop(false);
            server.Start(DefaultPort);
            while (client.PeersCount == 1)
            {
                Thread.Sleep(15);
            }
            client.PollEvents();

            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(0, client.PeersCount);
            Assert.IsTrue(disconnectInfo.HasValue);
            Assert.AreEqual(DisconnectReason.RemoteConnectionClose, disconnectInfo.Value.Reason);
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
            while (client.PeersCount == 1)
            {
                Thread.Sleep(15);
            }
        }

        [Test]
        public void ReconnectTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            int connectCount = 0;
            bool reconnected = false;
            ManagerStack.ServerListener(1).PeerConnectedEvent += peer =>
            {
                if (connectCount == 0)
                {
                    byte[] data = {1,2,3,4,5,6,7,8,9};
                    for (int i = 0; i < 1000; i++)
                    {
                        peer.Send(data, DeliveryMethod.ReliableOrdered);
                    }
                }
                connectCount++;
            };

            client.Stop();
            client.Start(10123);
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (connectCount < 2)
            {
                if (connectCount == 1 && !reconnected)
                {
                    client.Stop();
                    Thread.Sleep(500);
                    client.Start(10123);
                    client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
                    reconnected = true;
                }
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }
            Assert.AreEqual(2, connectCount);
        }

        [Test]
        public void RejectTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            bool rejectReceived = false;
            
            ManagerStack.ServerListener(1).ClearConnectionRequestEvent();
            ManagerStack.ServerListener(1).ConnectionRequestEvent += request =>
            {
                request.Reject(Encoding.UTF8.GetBytes("reject_test"));
            };
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
                {
                    Assert.AreEqual(true, info.Reason == DisconnectReason.ConnectionRejected);
                    Assert.AreEqual("reject_test", Encoding.UTF8.GetString(info.AdditionalData.GetRemainingBytes()));
                    rejectReceived = true;
                };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (!rejectReceived)
            {
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }

            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(0, client.PeersCount);
        }

        [Test, MaxTime(10000)]
        public void NetPeerDisconnectAll()
        {
            NetManager client = ManagerStack.Client(1);
            NetManager client2 = ManagerStack.Client(2);
            NetManager server = ManagerStack.Server(1);

            NetPeer clientServerPeer = client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            client2.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (clientServerPeer.ConnectionState != ConnectionState.Connected)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
                client2.PollEvents();
            }

            Assert.AreEqual(ConnectionState.Connected, clientServerPeer.ConnectionState);
            Assert.AreEqual(2, server.GetPeersCount(ConnectionState.Connected));

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                byte[] bytes = info.AdditionalData.GetRemainingBytes();
                Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, bytes);
                Assert.AreEqual(clientServerPeer, peer);
                Assert.AreEqual(DisconnectReason.RemoteConnectionClose, info.Reason);
            };

            server.DisconnectAll(new byte[]{1, 2, 3, 4}, 0, 4);
            
            Assert.AreEqual(0, server.GetPeersCount(ConnectionState.Connected));

            while (client.GetPeersCount(ConnectionState.Connected) != 0)
            {
                Thread.Sleep(15);
                client.PollEvents();
                server.PollEvents();
            }
           
            //Wait for client 'ShutdownOk' response
            Thread.Sleep(100);

            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(ConnectionState.Disconnected, clientServerPeer.ConnectionState);
        }

        [Test, MaxTime(2000)]
        public void DisconnectFromServerTest()
        {
            NetManager server = ManagerStack.Server(1);
            NetManager client = ManagerStack.Client(1);
            var clientDisconnected = false;
            var serverDisconnected = false;
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => { clientDisconnected = true; };
            ManagerStack.ServerListener(1).PeerDisconnectedEvent += (peer, info) => { serverDisconnected = true; };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            server.DisconnectPeer(server.FirstPeer);

            while (!(clientDisconnected && serverDisconnected))
            {
                Thread.Sleep(15);
                client.PollEvents();
                server.PollEvents();
            }
            
            // Wait that server remove disconnected peers
            Thread.Sleep(100);

            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(0, client.PeersCount);
        }

        [Test, MaxTime(5000)]
        public void ConnectAfterDisconnectWithSamePort()
        {
            NetManager server = ManagerStack.Server(1);

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener);
            Assert.True(client.Start(9049));
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
            client.Stop();

            var connected = false;
            listener.PeerConnectedEvent += (peer) =>
            {
                connected = true;
            };
            Assert.True(client.Start(9049));
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (!connected)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.True(connected);
            Assert.AreEqual(1, server.PeersCount);
            Assert.AreEqual(1, client.PeersCount);
        }

        [Test, MaxTime(2000)]
        public void DisconnectFromClientTest()
        {
            NetManager server = ManagerStack.Server(1);
            NetManager client = ManagerStack.Client(1);
            var clientDisconnected = false;
            var serverDisconnected = false;
            
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => { clientDisconnected = true; };
            ManagerStack.ServerListener(1).PeerDisconnectedEvent += (peer, info) => { serverDisconnected = true; };

            NetPeer serverPeer = client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            //User server peer from client
            serverPeer.Disconnect();

            while (!(clientDisconnected && serverDisconnected))
            {
                Thread.Sleep(15);
                client.PollEvents();
                server.PollEvents();
            }

            // Wait that server remove disconnected peers
            Thread.Sleep(100);

            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, server.PeersCount);
            Assert.AreEqual(0, client.PeersCount);
        }

        [Test, MaxTime(2000)]
        public void ConnectionByIpV6()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("::1", DefaultPort, DefaultAppKey);

            while (server.PeersCount != 1 || client.PeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(1, server.PeersCount);
            Assert.AreEqual(1, client.PeersCount);
        }

        [Test, MaxTime(2000)]
        public void DiscoveryBroadcastTest()
        {
            var server = ManagerStack.Server(1);
            var clientCount = 10;

            server.BroadcastReceiveEnabled = true;

            var writer = new NetDataWriter();
            writer.Put("Client request");

            ManagerStack.ServerListener(1).NetworkReceiveUnconnectedEvent += (point, reader, type) =>
            {
                if (type == UnconnectedMessageType.Broadcast)
                {
                    var serverWriter = new NetDataWriter();
                    serverWriter.Put("Server response");
                    server.SendUnconnectedMessage(serverWriter, point);
                }
            };

            for (ushort i = 1; i <= clientCount; i++)
            {
                var cache = i;
                ManagerStack.Client(i).UnconnectedMessagesEnabled = true;
                ManagerStack.ClientListener(i).NetworkReceiveUnconnectedEvent += (point, reader, type) =>
                {
                    Assert.AreEqual(type, UnconnectedMessageType.BasicMessage);
                    Assert.AreEqual("Server response", reader.GetString());
                    ManagerStack.Client(cache).Connect(point, DefaultAppKey);
                };
            }

            ManagerStack.ClientForeach((i, manager, l) => manager.SendBroadcast(writer, DefaultPort));

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
            Assert.AreSame(ManagerStack.Client(1), ManagerStack.Client(1));
            Assert.AreNotSame(ManagerStack.Client(1), ManagerStack.Client(2));
            Assert.AreSame(ManagerStack.Client(2), ManagerStack.Client(2));

            Assert.AreSame(ManagerStack.Server(1), ManagerStack.Server(1));
            Assert.AreNotSame(ManagerStack.Server(1), ManagerStack.Client(1));
            Assert.AreNotSame(ManagerStack.Server(1), ManagerStack.Client(2));
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
            Thread.Sleep(100);
            ManagerStack.ClientForeach((i, manager, l) => Assert.AreEqual(manager.PeersCount, 1));

            var dataStack = new Stack<byte[]>(clientCount);

            ManagerStack.ClientForeach(
                (i, manager, l) => l.NetworkReceiveEvent += (peer, reader, type) => dataStack.Push(reader.GetRemainingBytes()));

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