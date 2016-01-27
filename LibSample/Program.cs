using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

class Program
{
    private static int _messagesReceivedCount = 0;

    public static void ServerEvent(NetEvent netEvent)
    {
        switch (netEvent.Type)
        {
            case NetEventType.ReceiveUnconnected:
                Console.WriteLine("ReceiveUnconnected: {0}", netEvent.DataReader.GetString(100));
                break;

            case NetEventType.Receive:
                //echo
                netEvent.Peer.Send(netEvent.DataReader.Data, SendOptions.Reliable);
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
                for (int i = 0; i < 10000; i++)
                {
                    byte[] data = new byte[1300];
                    FastBitConverter.GetBytes(data, 0, i + 1);
                    netEvent.Peer.Send(data, SendOptions.Reliable);
                }
                break;

            case NetEventType.Receive:
                int dt = netEvent.DataReader.GetInt();
                _messagesReceivedCount++;
                if(_messagesReceivedCount % 100 == 0)
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
        server.AddFlowMode(1, 2000);
        server.AddFlowMode(10, 800);
        server.AddFlowMode(100, 10);
        server.UnconnectedMessagesEnabled = true;
        server.Start(9050);

        NetClient client = new NetClient();
        client.AddFlowMode(1, 2000);
        client.AddFlowMode(10, 800);
        client.AddFlowMode(100, 10);
        client.UnconnectedMessagesEnabled = true;
        client.Start();
        client.Connect("localhost", 9050);
        client.Stop();
        client.Start();
        client.Connect("localhost", 9050);

        NetDataWriter dw = new NetDataWriter();
        dw.Put("HELLO! ПРИВЕТ!");
        client.SendUnconnectedMessage(dw.CopyData(), new NetEndPoint("localhost", 9050));

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
