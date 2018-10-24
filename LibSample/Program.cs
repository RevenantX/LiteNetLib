using System;
using LiteNetLib.Ntp;

namespace LibSample
{
    class Program
    {
        static void Main(string[] args)
        {
            //Test ntp
            NtpRequest ntpRequest = null;
            ntpRequest = NtpRequest.Create("pool.ntp.org", ntpPacket =>
            {
                ntpRequest.Close();
                if (ntpPacket != null)
                    Console.WriteLine("[MAIN] NTP time test offset: " + ntpPacket.CorrectionOffset);
                else
                    Console.WriteLine("[MAIN] NTP time error");
            });
            ntpRequest.Send();

            //new EchoMessagesTest().Run();
            //new HolePunchServerTest().Run();
            //new BroadcastTest().Run();
            //new BenchmarkTest.TestHost().Run();
            //new SerializerBenchmark().Run();
            new SpeedBench().Run();
            //new PacketProcessorExample().Run();
        }
    }
}

