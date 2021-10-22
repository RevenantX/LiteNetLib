using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LiteNetLib.Layers;
using LiteNetLib.Tests.TestUtility;
using LiteNetLib.Utils;

using NUnit.Framework;

namespace LiteNetLib.Tests
{
    class LibErrorChecker : INetLogger
    {
        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            if(level == NetLogLevel.Error || level == NetLogLevel.Warning)
                Assert.Fail(str, args);
        }
    }

    [TestFixture]
    [Category("Communication")]
    public class CommunicationTest
    {
        const int TestTimeout = 4000;
        [SetUp]
        public void Init()
        {
            NetDebug.Logger = new LibErrorChecker();
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

        [Test, Timeout(TestTimeout)]
        public void ConnectionByIpV4()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void P2PConnect()
        {
            var client1 = ManagerStack.Client(1);
            var client2 = ManagerStack.Client(2);

            client1.Connect("127.0.0.1", client2.LocalPort, DefaultAppKey);
            client2.Connect("127.0.0.1", client1.LocalPort, DefaultAppKey);

            while (client1.ConnectedPeersCount != 1 || client2.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                client1.PollEvents();
                client2.PollEvents();
            }

            Assert.AreEqual(1, client1.ConnectedPeersCount);
            Assert.AreEqual(1, client2.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void ConnectionByIpV4Unsynced()
        {
            var server = ManagerStack.Server(1);
            server.UnsyncedEvents = true;
            var client = ManagerStack.Client(1);
            client.UnsyncedEvents = true;
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
            }

            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void DeliveryTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250 * 1024;
            ManagerStack.ClientListener(1).DeliveryEvent += (peer, obj) =>
            {
                Assert.AreEqual(5, (int)obj);
                msgDelivered = true;
            };
            ManagerStack.ClientListener(1).PeerConnectedEvent += peer =>
            {
                int testData = 5;
                byte[] arr = new byte[testSize];
                arr[0] = 196;
                arr[7000] = 32;
                arr[12499] = 200;
                arr[testSize - 1] = 254;
                peer.SendWithDeliveryEvent(arr, 0, DeliveryMethod.ReliableUnordered, testData);
            };
            ManagerStack.ServerListener(1).NetworkReceiveEvent += (peer, reader, method, channel) =>
            {
                Assert.AreEqual(testSize, reader.UserDataSize);
                Assert.AreEqual(196, reader.RawData[reader.UserDataOffset]);
                Assert.AreEqual(32, reader.RawData[reader.UserDataOffset + 7000]);
                Assert.AreEqual(200, reader.RawData[reader.UserDataOffset + 12499]);
                Assert.AreEqual(254, reader.RawData[reader.UserDataOffset + testSize - 1]);
                msgReceived = true;
            };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1 || !msgDelivered || !msgReceived)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void PeerNotFoundTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            DisconnectInfo? disconnectInfo = null;
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => disconnectInfo = info;
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }
            server.Stop(false);
            server.Start(DefaultPort);
            while (client.ConnectedPeersCount == 1)
            {
                Thread.Sleep(15);
            }
            client.PollEvents();

            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(0, client.ConnectedPeersCount);
            Assert.IsTrue(disconnectInfo.HasValue);
            Assert.AreEqual(DisconnectReason.RemoteConnectionClose, disconnectInfo.Value.Reason);
        }

        [Test, Timeout(10000)]
        public void ConnectionFailedTest()
        {
            NetManager client = ManagerStack.Client(1);

            var result = false;
            DisconnectInfo disconnectInfo = default;

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

        [Test, Timeout(10000)]
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
            Assert.True(server.ConnectedPeersCount == 1);

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(clientServerPeer, peer);
                Assert.AreEqual(DisconnectReason.Timeout, info.Reason);
            };

            server.Stop();

            Assert.True(server.ConnectedPeersCount == 0);
            while (client.ConnectedPeersCount == 1)
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

            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(0, client.ConnectedPeersCount);
        }

        [Test]
        public void RejectForceTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            bool rejectReceived = false;

            ManagerStack.ServerListener(1).ClearConnectionRequestEvent();
            ManagerStack.ServerListener(1).ConnectionRequestEvent += request =>
            {
                request.RejectForce(Encoding.UTF8.GetBytes("reject_test"));
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

            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(0, client.ConnectedPeersCount);
        }

        [Test, Timeout(10000)]
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

            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(ConnectionState.Disconnected, clientServerPeer.ConnectionState);
        }

        [Test, Timeout(TestTimeout)]
        public void DisconnectFromServerTest()
        {
            NetManager server = ManagerStack.Server(1);
            NetManager client = ManagerStack.Client(1);
            var clientDisconnected = false;
            var serverDisconnected = false;
            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) => { clientDisconnected = true; };
            ManagerStack.ServerListener(1).PeerDisconnectedEvent += (peer, info) => { serverDisconnected = true; };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.ConnectedPeersCount != 1)
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

            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(0, client.ConnectedPeersCount);
        }

        [Test, Timeout(5000)]
        public void EncryptTest()
        {
            EventBasedNetListener srvListener = new EventBasedNetListener();
            EventBasedNetListener cliListener = new EventBasedNetListener();
            NetManager srv = new NetManager(srvListener, new XorEncryptLayer("secret_key"));
            NetManager cli = new NetManager(cliListener, new XorEncryptLayer("secret_key"));
            srv.Start(DefaultPort + 1);
            cli.Start();

            srvListener.ConnectionRequestEvent += request => { request.AcceptIfKey(DefaultAppKey); };
            cli.Connect("127.0.0.1", DefaultPort + 1, DefaultAppKey);

            while (srv.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                srv.PollEvents();
            }
            Thread.Sleep(200);
            Assert.AreEqual(1, srv.ConnectedPeersCount);
            Assert.AreEqual(1, cli.ConnectedPeersCount);
            cli.Stop();
            srv.Stop();
        }

        [Test, Timeout(5000)]
        public void ConnectAfterDisconnectWithSamePort()
        {
            NetManager server = ManagerStack.Server(1);

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener, new Crc32cLayer());
            Assert.True(client.Start(9049));
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.ConnectedPeersCount != 1)
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
            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
            client.Stop();
        }

        [Test, Timeout(TestTimeout)]
        public void DisconnectFromClientTest()
        {
            NetManager server = ManagerStack.Server(1);
            NetManager client = ManagerStack.Client(1);
            var clientDisconnected = false;
            var serverDisconnected = false;

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(DisconnectReason.DisconnectPeerCalled, info.Reason);
                Assert.AreEqual(0, client.ConnectedPeersCount);
                clientDisconnected = true;
            };
            ManagerStack.ServerListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.AreEqual(DisconnectReason.RemoteConnectionClose, info.Reason);
                serverDisconnected = true;
            };

            NetPeer serverPeer = client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);
            while (server.ConnectedPeersCount != 1)
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

            Assert.True(clientDisconnected);
            Assert.True(serverDisconnected);
            Assert.AreEqual(0, server.ConnectedPeersCount);
            Assert.AreEqual(0, client.ConnectedPeersCount);
        }

        [Test, Timeout(10000)]
        public void ChannelsTest()
        {
            const int channelsCount = 64;
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            server.ChannelsCount = channelsCount;
            client.ChannelsCount = channelsCount;

            NetDataWriter writer = new NetDataWriter();
            var methods = new[]
            {
                DeliveryMethod.Unreliable,
                DeliveryMethod.Sequenced,
                DeliveryMethod.ReliableOrdered,
                DeliveryMethod.ReliableSequenced,
                DeliveryMethod.ReliableUnordered
            };

            int messagesReceived = 0;
            ManagerStack.ClientListener(1).PeerConnectedEvent += peer =>
            {
                for (int i = 0; i < channelsCount; i++)
                {
                    foreach (var deliveryMethod in methods)
                    {
                        writer.Reset();
                        writer.Put((byte) deliveryMethod);
                        if (deliveryMethod == DeliveryMethod.ReliableOrdered ||
                            deliveryMethod == DeliveryMethod.ReliableUnordered)
                            writer.Put(new byte[506]);
                        peer.Send(writer, (byte) i, deliveryMethod);
                    }
                }
            };
            ManagerStack.ServerListener(1).NetworkReceiveEvent += (peer, reader, method, channel) =>
            {
                Assert.AreEqual((DeliveryMethod)reader.GetByte(), method);
                messagesReceived++;
            };
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (messagesReceived != methods.Length*channelsCount)
            {
                server.PollEvents();
                client.PollEvents();
                Thread.Sleep(15);
            }

            Assert.AreEqual(methods.Length*channelsCount, messagesReceived);
            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
        }

        [Test, Timeout(TestTimeout)]
        public void ConnectionByIpV6()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            client.Connect("::1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
        }

        [Test, Timeout(10000)]
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
                    if (point.AddressFamily == AddressFamily.InterNetworkV6)
                        return;
                    Assert.AreEqual(type, UnconnectedMessageType.BasicMessage);
                    Assert.AreEqual("Server response", reader.GetString());
                    ManagerStack.Client(cache).Connect(point, DefaultAppKey);
                };
            }

            ManagerStack.ClientForeach((i, manager, l) => manager.SendBroadcast(writer, DefaultPort));

            while (server.ConnectedPeersCount < clientCount)
            {
                server.PollEvents();
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(15);
            }

            Assert.AreEqual(clientCount, server.ConnectedPeersCount);
            ManagerStack.ClientForeach(
                (i, manager, l) =>
                {
                    Assert.AreEqual(manager.ConnectedPeersCount, 1);
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

        [Test, Timeout(TestTimeout)]
        public void ManualMode()
        {
            var serverListener = new EventBasedNetListener();
            var server = new NetManager(serverListener, new Crc32cLayer());

            serverListener.ConnectionRequestEvent += request => request.AcceptIfKey(DefaultAppKey);

            var client = ManagerStack.Client(1);
            Assert.IsTrue(server.StartInManualMode(DefaultPort));

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
                server.ManualUpdate(15);
            }

            Assert.AreEqual(1, server.ConnectedPeersCount);
            Assert.AreEqual(1, client.ConnectedPeersCount);
            server.Stop();
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

            while (server.ConnectedPeersCount < clientCount)
            {
                Thread.Sleep(15);
                server.PollEvents();
            }

            Assert.AreEqual(server.ConnectedPeersCount, clientCount);
            Thread.Sleep(100);
            ManagerStack.ClientForeach((i, manager, l) => Assert.AreEqual(manager.ConnectedPeersCount, 1));

            var dataStack = new Stack<byte[]>(clientCount);

            ManagerStack.ClientForeach(
                (i, manager, l) => l.NetworkReceiveEvent += (peer, reader, type, channel) => dataStack.Push(reader.GetRemainingBytes()));

            var data = Encoding.Default.GetBytes("TextForTest");
            server.SendToAll(data, DeliveryMethod.ReliableUnordered);

            while (dataStack.Count < clientCount)
            {
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(10);
            }

            Assert.AreEqual(dataStack.Count, clientCount);

            Assert.AreEqual(server.ConnectedPeersCount, clientCount);
            for (ushort i = 1; i <= clientCount; i++)
            {
                Assert.AreEqual(ManagerStack.Client(i).ConnectedPeersCount, 1);
                Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            }
        }
    }
}
