using System;
using System.Threading;
using LiteNetLib;

class Program
{
    private static int _messagesReceivedCount = 0;

    public static void ServerEvent(NetEvent netEvent)
    {
        switch (netEvent.Type)
        {
            case NetEventType.ReceiveUnconnected:
                var data = netEvent.Data;
                Console.WriteLine("ReceiveUnconnected: {0},{1},{2}", data[0], data[1], data[2]);
                break;

            case NetEventType.Receive:
                netEvent.Peer.Send(netEvent.Data, SendOptions.Reliable);
                break;

            case NetEventType.Disconnect:
                Console.WriteLine("Peer disconnected: " + netEvent.RemoteEndPoint);
                break;

            case NetEventType.Connect:
                Console.WriteLine("Peer connected: " + netEvent.RemoteEndPoint);
                break;

            case NetEventType.Error:
                Console.WriteLine("peer eror");
                break;
        }
    }

    public static void ClientEvent(NetEvent netEvent)
    {
        switch (netEvent.Type)
        {
            case NetEventType.Connect:
                Console.WriteLine("Client connected: {0}:{1}", netEvent.Peer.EndPoint.Host, netEvent.Peer.EndPoint.Port);
                break;

            //for (int i = 0; i < 2000; i++)
                //{
                //    byte[] data = new byte[1300];
                //    FastBitConverter.GetBytes(data, 0, i + 1);
                //    netEvent.Peer.Send(data, SendOptions.Reliable);
                //}
            case NetEventType.Receive:
                int dt = BitConverter.ToInt32(netEvent.Data, 0);
                _messagesReceivedCount++;
                if(_messagesReceivedCount % 1000 == 0)
                    Console.WriteLine("CNT: {0}, DT: {1}", _messagesReceivedCount, dt);
                break;

            case NetEventType.Error:
                Console.WriteLine("Connection error!");
                break;

            case NetEventType.Disconnect:
                Console.WriteLine("Disconnected from server!");
                break;
        }
    }

    static void Main(string[] args)
    {
        NetServer server = new NetServer(2);
        server.Start(9050);

        NetClient client = new NetClient();
        client.Start(9051);
        client.Connect("localhost", 9050);
        client.Stop();
        client.Start(9051);
        client.Connect("localhost", 9050);
        client.SendUnconnectedMessage(new byte[] {1, 2, 3}, new NetEndPoint("localhost", 9050));

        while (!Console.KeyAvailable)
        {
            NetEvent evt;
            while ((evt = client.GetNextEvent()) != null)
            {
                ClientEvent(evt);
                client.Recycle(evt);
            }
            while ((evt = server.GetNextEvent()) != null)
            {
                ServerEvent(evt);
                server.Recycle(evt);
            }
            Thread.Sleep(10);
        }

        server.Stop();
        client.Stop();
    }
}
