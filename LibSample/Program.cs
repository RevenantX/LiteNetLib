using System;
using LiteNetLib;

namespace LibSample
{
    class Program
    {
        static void Main(string[] args)
        {
            //Test ntp
            NetUtils.RequestTimeFromNTP("pool.ntp.org", 123, dateTime =>
            {
                if (dateTime.HasValue)
                {
                    Console.WriteLine("[MAIN] Synced time test: " + dateTime.Value);
                }
            });

            //new EchoMessagesTest().Run();
            //new HolePunchServerTest().Run();
            //new BroadcastTest().Run();
            //new BenchmarkTest.TestHost().Run();
            //new SerializerBenchmark().Run();
            //new SpeedBecnh().Run();
        }
    }
}

