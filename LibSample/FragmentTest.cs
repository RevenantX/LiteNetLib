using System;
using System.Threading;
using LiteNetLib;

namespace LibSample
{
    class FragmentTest
    {
        private static void ClientEvent(NetClient client, NetEvent netEvent)
        {
            switch (netEvent.Type)
            {
                case NetEventType.Connect:
                    Console.WriteLine("[Client] connected: {0}:{1}", netEvent.Peer.EndPoint.Host, netEvent.Peer.EndPoint.Port);
                    byte[] testData = new byte[13218];
                    testData[0] = 192;
                    testData[13217] = 31;
                    netEvent.Peer.Send(testData, SendOptions.ReliableOrdered);
                    break;

                case NetEventType.Receive:
                    Console.WriteLine("Received size: {0}", netEvent.Data.Length);
                    if (netEvent.Data.Length == 13218)
                    {
                        Console.WriteLine("TestFrag: {0}, {1}", netEvent.Data[0], netEvent.Data[13217]);
                    }
                    break;

                case NetEventType.Error:
                    Console.WriteLine("[Client] connection error!");
                    break;

                case NetEventType.Disconnect:
                    Console.WriteLine("[Client] disconnected: " + netEvent.AdditionalInfo);
                    break;
            }
        }

        private static void ServerEvent(NetEvent netEvent)
        {
            switch (netEvent.Type)
            {
                case NetEventType.ReceiveUnconnected:
                    Console.WriteLine("[Server] ReceiveUnconnected: {0}", netEvent.DataReader.GetString(100));
                    break;

                case NetEventType.Receive:
                    //echo
                    if (netEvent.Data.Length == 13218)
                    {
                        Console.WriteLine("TestFrag: {0}, {1}", netEvent.Data[0], netEvent.Data[13217]);
                    }
                    netEvent.Peer.Send(netEvent.DataReader.Data, SendOptions.ReliableUnordered);

                    break;

                case NetEventType.Disconnect:
                    Console.WriteLine("[Server] Peer disconnected: " + netEvent.RemoteEndPoint + ", reason: " + netEvent.AdditionalInfo);
                    break;

                case NetEventType.Connect:
                    Console.WriteLine("[Server] Peer connected: " + netEvent.RemoteEndPoint);
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
            server.UnconnectedMessagesEnabled = true;
            server.Start(9050);

            //Client
            NetClient client = new NetClient();
            client.UnconnectedMessagesEnabled = true;
            client.Start();
            client.Connect("localhost", 9050, "myapp1");

            while (!Console.KeyAvailable)
            {
                NetEvent evt;
                while ((evt = client.GetNextEvent()) != null)
                {
                    ClientEvent(client, evt);
                    client.Recycle(evt);
                }
                while ((evt = server.GetNextEvent()) != null)
                {
                    ServerEvent(evt);
                    server.Recycle(evt);
                }
                Thread.Sleep(15);
            }

            client.Stop();
            server.Stop();
            Console.ReadKey();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
