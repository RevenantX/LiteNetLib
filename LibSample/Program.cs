using System;
using LibSample;
using LiteNetLib;

class Program
{
    static void Main(string[] args)
    {
        //Test ntp
        NtpSyncModule ntpSync = new NtpSyncModule("pool.ntp.org");
        ntpSync.GetNetworkTime();
        if (ntpSync.SyncedTime.HasValue)
        {
            Console.WriteLine("Synced time test: " + ntpSync.SyncedTime.Value);
        }

        FragmentTest fragmentTest = new FragmentTest();
        HolePunchServerTest holePunchServerTest = new HolePunchServerTest();
        EchoMessagesTest echoMessagesTest = new EchoMessagesTest();

        holePunchServerTest.Run();
    }
}
