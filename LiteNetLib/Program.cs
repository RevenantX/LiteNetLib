using System;
using System.Text;
using LiteNetLib;

class Program
{
    private static int _messagesReceivedCount = 0;

    public static void ServerEvent(NetServer sender, NetEvent netEvent)
    {
        if (netEvent.type == NetEventType.Receive)
        {
            netEvent.peer.Send(netEvent.data, SendOptions.Reliable);
        }
        else if (netEvent.type == NetEventType.Disconnect)
        {
            Console.WriteLine("peer disconnected!");
        }
    }

    public static void ClientEvent(NetClient sender, NetEvent netEvent)
    {
        if (netEvent.type == NetEventType.Connect)
        {
            Console.WriteLine("CliConnected");

            for (int i = 0; i < 2000; i++)
            {
                byte[] data = new byte[1300];
                netEvent.peer.Send(data, SendOptions.Reliable);
            }
        }
        else if (netEvent.type == NetEventType.Receive)
        {
            _messagesReceivedCount++;
            Console.WriteLine("CNT: {0}", _messagesReceivedCount);
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
        server.AddNetEventListener(ServerEvent);
        server.Start(9050);

        NetClient client = new NetClient();
        client.AddNetEventListener(ClientEvent);
        client.Start(9051);
        client.Connect("localhost", 9050);

        Console.ReadLine();

        server.Stop();

        Console.ReadLine();
        client.Stop();
    }
}