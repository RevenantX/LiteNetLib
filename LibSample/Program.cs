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

        //new EchoMessagesTest().Run();
        //new HolePunchServerTest().Run();
        //new BroadcastTest().Run();
        new BenchmarkTest.TestHost().Run();
    }
}
