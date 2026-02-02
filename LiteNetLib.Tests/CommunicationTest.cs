using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LiteNetLib.Layers;
using LiteNetLib.Tests.TestUtility;
using LiteNetLib.Utils;

using NUnit.Framework;

namespace LiteNetLib.Tests
{
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
        private static readonly byte[] DefaultAppKeyBytes = [12, 0, 116, 101, 115, 116, 95, 115, 101, 114, 118, 101, 114];

        public NetManagerStack ManagerStack { get; set; }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(client1.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client2.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
        public void P2PConnectWithSpan()
        {
            var client1 = ManagerStack.Client(1);
            var client2 = ManagerStack.Client(2);

            IPEndPoint endPoint1 = new IPEndPoint(IPAddress.Loopback, client2.LocalPort);
            IPEndPoint endPoint2 = new IPEndPoint(IPAddress.Loopback, client1.LocalPort);
            client1.Connect(endPoint1, DefaultAppKeyBytes.AsSpan());
            client2.Connect(endPoint2, DefaultAppKeyBytes.AsSpan());

            while (client1.ConnectedPeersCount != 1 || client2.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                client1.PollEvents();
                client2.PollEvents();
            }

            Assert.That(client1.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client2.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
        public void DeliveryTest()
        {
            var server = ManagerStack.Server(1);
            var client = ManagerStack.Client(1);
            bool msgDelivered = false;
            bool msgReceived = false;
            const int testSize = 250 * 1024;
            ManagerStack.ClientListener(1).DeliveryEvent += (peer, obj) =>
            {
                Assert.That((int)obj, Is.EqualTo(5));
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
            ManagerStack.ServerListener(1).NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                Assert.That(reader.UserDataSize, Is.EqualTo(testSize));
                Assert.That(reader.RawData[reader.UserDataOffset], Is.EqualTo(196));
                Assert.That(reader.RawData[reader.UserDataOffset + 7000], Is.EqualTo(32));
                Assert.That(reader.RawData[reader.UserDataOffset + 12499], Is.EqualTo(200));
                Assert.That(reader.RawData[reader.UserDataOffset + testSize - 1], Is.EqualTo(254));
                msgReceived = true;
            };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1 || !msgDelivered || !msgReceived)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(disconnectInfo.HasValue, Is.True);
            Assert.That(disconnectInfo.Value.Reason, Is.EqualTo(DisconnectReason.PeerNotFound));
        }

        [Test, CancelAfter(10000)]
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

            Assert.That(result, Is.True);
            Assert.That(disconnectInfo.Reason, Is.EqualTo(DisconnectReason.ConnectionFailed));
        }

        [Test, CancelAfter(10000)]
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

            Assert.That(clientServerPeer.ConnectionState, Is.EqualTo(ConnectionState.Connected));
            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.That(peer, Is.EqualTo(clientServerPeer));
                Assert.That(info.Reason, Is.EqualTo(DisconnectReason.Timeout));
            };

            server.Stop();

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
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
            Assert.That(connectCount, Is.EqualTo(2));
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
                    Assert.That(info.Reason, Is.EqualTo(DisconnectReason.ConnectionRejected));
                    Assert.That(Encoding.UTF8.GetString(info.AdditionalData.GetRemainingBytes()), Is.EqualTo("reject_test"));
                    rejectReceived = true;
                };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (!rejectReceived)
            {
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
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
                Assert.That(info.Reason, Is.EqualTo(DisconnectReason.ConnectionRejected));
                Assert.That(Encoding.UTF8.GetString(info.AdditionalData.GetRemainingBytes()), Is.EqualTo("reject_test"));
                rejectReceived = true;
            };

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (!rejectReceived)
            {
                client.PollEvents();
                server.PollEvents();
                Thread.Sleep(15);
            }

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
        }

        [Test, CancelAfter(10000)]
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

            Assert.That(clientServerPeer.ConnectionState, Is.EqualTo(ConnectionState.Connected));
            Assert.That(server.GetPeersCount(ConnectionState.Connected), Is.EqualTo(2));

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                byte[] bytes = info.AdditionalData.GetRemainingBytes();
                Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }).AsCollection);
                Assert.That(peer, Is.EqualTo(clientServerPeer));
                Assert.That(info.Reason, Is.EqualTo(DisconnectReason.RemoteConnectionClose));
            };

            server.DisconnectAll(new byte[]{1, 2, 3, 4}, 0, 4);

            Assert.That(server.GetPeersCount(ConnectionState.Connected), Is.EqualTo(0));

            while (client.GetPeersCount(ConnectionState.Connected) != 0)
            {
                Thread.Sleep(15);
                client.PollEvents();
                server.PollEvents();
            }

            //Wait for client 'ShutdownOk' response
            Thread.Sleep(100);

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(clientServerPeer.ConnectionState, Is.EqualTo(ConnectionState.Disconnected));
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(clientDisconnected, Is.True);
            Assert.That(serverDisconnected, Is.True);
            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
        }

        [Test, CancelAfter(5000)]
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
            Assert.That(srv.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(cli.ConnectedPeersCount, Is.EqualTo(1));
            cli.Stop();
            srv.Stop();
        }

        [Test, CancelAfter(5000)]
        public void ConnectAfterDisconnectWithSamePort()
        {
            NetManager server = ManagerStack.Server(1);

            EventBasedNetListener listener = new EventBasedNetListener();
            NetManager client = new NetManager(listener, new Crc32cLayer());
            Assert.That(client.Start(9049), Is.True);
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
            Assert.That(client.Start(9049), Is.True);
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (!connected)
            {
                Thread.Sleep(15);
                server.PollEvents();
                client.PollEvents();
            }

            Assert.That(connected, Is.True);
            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
            client.Stop();
        }

        [Test, CancelAfter(TestTimeout)]
        public void DisconnectFromClientTest()
        {
            NetManager server = ManagerStack.Server(1);
            NetManager client = ManagerStack.Client(1);
            var clientDisconnected = false;
            var serverDisconnected = false;

            ManagerStack.ClientListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.That(info.Reason, Is.EqualTo(DisconnectReason.DisconnectPeerCalled));
                Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
                clientDisconnected = true;
            };
            ManagerStack.ServerListener(1).PeerDisconnectedEvent += (peer, info) =>
            {
                Assert.That(info.Reason, Is.EqualTo(DisconnectReason.RemoteConnectionClose));
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

            Assert.That(clientDisconnected, Is.True);
            Assert.That(serverDisconnected, Is.True);
            Assert.That(server.ConnectedPeersCount, Is.EqualTo(0));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(0));
        }

        [Test, CancelAfter(10000)]
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
            ManagerStack.ServerListener(1).NetworkReceiveEvent += (peer, reader, channel, method) =>
            {
                Assert.That((DeliveryMethod)reader.GetByte(), Is.EqualTo(method));
                messagesReceived++;
            };
            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (messagesReceived != methods.Length*channelsCount)
            {
                server.PollEvents();
                client.PollEvents();
                Thread.Sleep(15);
            }

            Assert.That(messagesReceived, Is.EqualTo(methods.Length * channelsCount));
            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
        }

        [Test, CancelAfter(10000)]
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
                    Assert.That(type, Is.EqualTo(UnconnectedMessageType.BasicMessage));
                    Assert.That(reader.GetString(), Is.EqualTo("Server response"));
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(clientCount));
            ManagerStack.ClientForeach(
                (i, manager, l) =>
                {
                    Assert.That(manager.ConnectedPeersCount, Is.EqualTo(1));
                });
        }

        [Test]
        public void HelperManagerStackTest()
        {
            Assert.That(ManagerStack.Client(1), Is.SameAs(ManagerStack.Client(1)));
            Assert.That(ManagerStack.Client(1), Is.Not.SameAs(ManagerStack.Client(2)));
            Assert.That(ManagerStack.Client(2), Is.SameAs(ManagerStack.Client(2)));

            Assert.That(ManagerStack.Server(1), Is.SameAs(ManagerStack.Server(1)));
            Assert.That(ManagerStack.Server(1), Is.Not.SameAs(ManagerStack.Client(1)));
            Assert.That(ManagerStack.Server(1), Is.Not.SameAs(ManagerStack.Client(2)));
        }

        [Test, CancelAfter(TestTimeout)]
        public void ManualMode()
        {
            var serverListener = new EventBasedNetListener();
            var server = new NetManager(serverListener, new Crc32cLayer());

            serverListener.ConnectionRequestEvent += request => request.AcceptIfKey(DefaultAppKey);

            var client = ManagerStack.Client(1);
            Assert.That(server.StartInManualMode(DefaultPort), Is.True);

            client.Connect("127.0.0.1", DefaultPort, DefaultAppKey);

            while (server.ConnectedPeersCount != 1 || client.ConnectedPeersCount != 1)
            {
                Thread.Sleep(15);
                server.PollEvents();
                server.ManualUpdate(15);
            }

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(1));
            Assert.That(client.ConnectedPeersCount, Is.EqualTo(1));
            server.Stop();
        }

        [Test, CancelAfter(TestTimeout)]
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

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(clientCount));
            Thread.Sleep(100);
            ManagerStack.ClientForeach((i, manager, l) => Assert.That(manager.ConnectedPeersCount, Is.EqualTo(1)));

            var dataStack = new Stack<byte[]>(clientCount);

            ManagerStack.ClientForeach(
                (i, manager, l) => l.NetworkReceiveEvent += (peer, reader, channel, type) => dataStack.Push(reader.GetRemainingBytes()));

            var data = Encoding.Default.GetBytes("TextForTest");
            server.SendToAll(data, DeliveryMethod.ReliableUnordered);

            while (dataStack.Count < clientCount)
            {
                ManagerStack.ClientForeach((i, manager, l) => manager.PollEvents());

                Thread.Sleep(10);
            }

            Assert.That(dataStack.Count, Is.EqualTo(clientCount));

            Assert.That(server.ConnectedPeersCount, Is.EqualTo(clientCount));
            for (ushort i = 1; i <= clientCount; i++)
            {
                Assert.That(ManagerStack.Client(i).ConnectedPeersCount, Is.EqualTo(1));
                Assert.That(data, Is.EqualTo(dataStack.Pop()).AsCollection);
            }
        }
    }
}
