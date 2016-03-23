using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

class Program
{
    private static int _messagesReceivedCount = 0;

    private static void ClientEvent(NetClient client, NetEvent netEvent)
    {
        switch (netEvent.Type)
        {
            case NetEventType.Connect:
                Console.WriteLine("[Client] connected: {0}:{1}", netEvent.Peer.EndPoint.Host, netEvent.Peer.EndPoint.Port);
                //byte[] testData = new byte[13218];
                //testData[0] = 192;
                //testData[13217] = 31;
                //netEvent.Peer.Send(testData, SendOptions.ReliableOrdered);

                NetDataWriter dataWriter = new NetDataWriter();
                for (int i = 0; i < 1000; i++)
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
                Console.WriteLine("CNT: {0}, TYPE: {1}, NUM: {2}", _messagesReceivedCount, type, num);
                //Console.WriteLine("Received size: {0}", netEvent.Data.Length);
                //if (netEvent.Data.Length == 13218)
                //{
                //    Console.WriteLine("TestFrag: {0}, {1}", netEvent.Data[0], netEvent.Data[13217]);
                //}
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
                //Console.WriteLine("Received size: {0}", netEvent.Data.Length);
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

    static void Main(string[] args)
    {
        //Test ntp
        NtpSyncModule ntpSync = new NtpSyncModule("pool.ntp.org");
        ntpSync.GetNetworkTime();
        if (ntpSync.SyncedTime.HasValue)
        {
            Console.WriteLine("Synced time test: " + ntpSync.SyncedTime.Value);
        }

        //Server
        NetServer server = new NetServer(2, "myapp1");
        server.UnconnectedMessagesEnabled = true;
        server.Start(9050);

        //Client
        NetClient client = new NetClient();
        client.UnconnectedMessagesEnabled = true;
        client.Start();
        client.Connect("localhost", 9050, "myapp1");

        //Test unconnected
        NetDataWriter dw = new NetDataWriter();
        dw.Put("HELLO! TST!");
        client.SendUnconnectedMessage(dw.CopyData(), new NetEndPoint("localhost", 9050));

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
