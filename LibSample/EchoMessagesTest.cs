using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibSample
{
    class EchoMessagesTest
    {
        private static int _messagesReceivedCount = 0;

        private static void ClientEvent(NetClient client, NetEvent netEvent)
        {
            switch (netEvent.Type)
            {
                case NetEventType.Connect:
                    Console.WriteLine("[Client] connected: {0}:{1}", netEvent.Peer.EndPoint.Host, netEvent.Peer.EndPoint.Port);

                    NetDataWriter dataWriter = new NetDataWriter();
                    for (int i = 0; i < 5; i++)
                    {
                        dataWriter.Reset();
                        dataWriter.Put(0);
                        dataWriter.Put(i);
                        netEvent.Peer.Send(dataWriter, SendOptions.ReliableUnordered);

                        dataWriter.Reset();
                        dataWriter.Put(1);
                        dataWriter.Put(i);
                        netEvent.Peer.Send(dataWriter, SendOptions.ReliableOrdered);

                        dataWriter.Reset();
                        dataWriter.Put(2);
                        dataWriter.Put(i);
                        netEvent.Peer.Send(dataWriter, SendOptions.Sequenced);

                        dataWriter.Reset();
                        dataWriter.Put(3);
                        dataWriter.Put(i);
                        netEvent.Peer.Send(dataWriter, SendOptions.Unreliable);
                    }
                    break;

                case NetEventType.Receive:
                    int type = netEvent.DataReader.GetInt();
                    int num = netEvent.DataReader.GetInt();
                    _messagesReceivedCount++;
                    Console.WriteLine("[{0}] CNT: {1}, TYPE: {2}, NUM: {3}", client.LocalEndPoint.Port, _messagesReceivedCount, type, num);
                    break;

                case NetEventType.Error:
                    Console.WriteLine("[Client] connection error!");
                    break;

                case NetEventType.Disconnect:
                    Console.WriteLine("[Client] disconnected: " + netEvent.AdditionalInfo);
                    break;
            }
        }

        private static void ServerEvent(NetServer server, NetEvent netEvent)
        {
            switch (netEvent.Type)
            {
                case NetEventType.ReceiveUnconnected:
                    Console.WriteLine("[Server] ReceiveUnconnected: {0}", netEvent.DataReader.GetString(100));
                    break;

                case NetEventType.Receive:
                    //echo
                    netEvent.Peer.Send(netEvent.DataReader.Data, SendOptions.ReliableUnordered);

                    break;

                case NetEventType.Disconnect:
                    Console.WriteLine("[Server] Peer disconnected: " + netEvent.RemoteEndPoint + ", reason: " + netEvent.AdditionalInfo);
                    break;

                case NetEventType.Connect:
                    Console.WriteLine("[Server] Peer connected: " + netEvent.RemoteEndPoint);
                    var peers = server.GetPeers();
                    foreach (var netPeer in peers)
                    {
                        Console.WriteLine("ConnectedPeersList: id={0}, ep={1}", netPeer.Id, netPeer.EndPoint);
                    }
                    break;

                case NetEventType.Error:
                    Console.WriteLine("[Server] peer eror");
                    break;
            }
        }

        public void Run()
        {
            //Server
            NetServer server = new NetServer(2, "myapp1");
            server.Start(9050);

            //Client
            NetClient client1 = new NetClient();
            client1.Start();
            client1.Connect("localhost", 9050, "myapp1");
            client1.SimulateLatency = true;
            client1.SimulationMaxLatency = 1500;

            NetClient client2 = new NetClient();
            client2.Start();
            client2.Connect("localhost", 9050, "myapp1");

            while (!Console.KeyAvailable)
            {
                NetEvent evt;
                while ((evt = client1.GetNextEvent()) != null)
                {
                    ClientEvent(client1, evt);
                    client1.Recycle(evt);
                }
                while ((evt = client2.GetNextEvent()) != null)
                {
                    ClientEvent(client2, evt);
                    client2.Recycle(evt);
                }
                while ((evt = server.GetNextEvent()) != null)
                {
                    ServerEvent(server, evt);
                    server.Recycle(evt);
                }
                Thread.Sleep(15);
            }

            client1.Stop();
            client2.Stop();
            server.Stop();
            Console.ReadKey();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
