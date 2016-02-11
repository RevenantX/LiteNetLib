using System;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;

namespace LibClientSample
{
    class Program
    {
        private static int _messagesReceivedCount = 0;

        private static void ClientEvent(NetEvent netEvent)
        {
            switch (netEvent.Type)
            {
                case NetEventType.Connect:
                    Console.WriteLine("[Client] connected: {0}:{1}", netEvent.Peer.EndPoint.Host, netEvent.Peer.EndPoint.Port);

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
                    break;

                case NetEventType.Error:
                    Console.WriteLine("[Client] connection error!");
                    break;

                case NetEventType.Disconnect:
                    Console.WriteLine("[Client] disconnected: " + netEvent.AdditionalInfo);
                    break;
            }
        }

        static void Main(string[] args)
        {
            NetClient client = new NetClient();
            client.UnconnectedMessagesEnabled = true;
            client.Start();
            client.Connect("localhost", 9050);
            client.Disconnect();
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
                Thread.Sleep(10);
            }

            client.Stop();
        }
    }
}
