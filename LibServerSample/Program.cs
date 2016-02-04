using System;
using System.Threading;
using LiteNetLib;

class Program
{
    private static void ServerEvent(NetEvent netEvent)
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
                break;

            case NetEventType.Error:
                Console.WriteLine("[Server] peer eror");
                break;
        }
    }

    static void Main(string[] args)
    {
        NtpSyncModule ntpSync = new NtpSyncModule("pool.ntp.org");
        ntpSync.GetNetworkTime();
        if (ntpSync.SyncedTime.HasValue)
        {
            Console.WriteLine("Synced time test: " + ntpSync.SyncedTime.Value);
        }

        NetServer server = new NetServer(2);
        server.UnconnectedMessagesEnabled = true;
        server.Start(9050);

        while (!Console.KeyAvailable)
        {
            NetEvent evt;
            while ((evt = server.GetNextEvent()) != null)
            {
                ServerEvent(evt);
                server.Recycle(evt);
            }
            Thread.Sleep(10);
        }

        server.Stop();
    }
}
