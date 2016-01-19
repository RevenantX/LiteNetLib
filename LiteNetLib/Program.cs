using System;
using System.Threading;
using LiteNetLib;

class Program
{
    private static int _messagesReceivedCount = 0;

    public static void ServerEvent(NetEvent netEvent)
    {
        if (netEvent.type == NetEventType.Receive)
        {
            netEvent.peer.Send(netEvent.data, SendOptions.ReliableOrdered);
        }
        else if (netEvent.type == NetEventType.Disconnect)
        {
            Console.WriteLine("peer disconnected!");
        }
        else if(netEvent.type == NetEventType.Error)
        {
            Console.WriteLine("peer eror");
        }
    }

    public static void ClientEvent(NetEvent netEvent)
    {
        if (netEvent.type == NetEventType.Connect)
        {
            Console.WriteLine("CliConnected");

            for (int i = 0; i < 4000; i++)
            {
                byte[] data = new byte[1300];
                FastBitConverter.GetBytes(data, 0, i+1);
                netEvent.peer.Send(data, SendOptions.Reliable);
            }
        }
        else if (netEvent.type == NetEventType.Receive)
        {
            int dt = BitConverter.ToInt32(netEvent.data, 0);
            _messagesReceivedCount++;
            Console.WriteLine("CNT: {0}, DT: {1}", _messagesReceivedCount, dt);
            if (_messagesReceivedCount != dt)
            {
                //Console.WriteLine("DIFF DIFF DIFF DIFF DIFF DIFF DIFF");
            }
        }
        else if (netEvent.type == NetEventType.Error)
        {
            Console.WriteLine("Connection error!");
        }
        else if (netEvent.type == NetEventType.Disconnect)
        {
            Console.WriteLine("Disconnected from server!");
        }
    }

    static void Main(string[] args)
    {
        NetServer server = new NetServer(2);
        server.Start(9050);

        NetClient client = new NetClient();
        client.Start(9051);
        client.Connect("localhost", 9050);

        while (!Console.KeyAvailable)
        {
            NetEvent evt;
            while ((evt = client.GetNextEvent()) != null)
            {
                ClientEvent(evt);
            }
            while ((evt = server.GetNextEvent()) != null)
            {
                ServerEvent(evt);
            }
            Thread.Sleep(10);
        }

        server.Stop();
        client.Stop();
    }
}
