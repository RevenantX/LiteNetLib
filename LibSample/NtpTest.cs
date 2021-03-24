using System;
using System.Threading;
using LiteNetLib;

namespace LibSample
{
    public class NtpTest : IExample
    {
        public void Run()
        {
            RequestNtpService("0.pool.ntp.org");
        }

        private void RequestNtpService(string ntpService)
        {
            Console.WriteLine($"Request time from \"{ntpService}\" service");
            var ntpComplete = false;
            EventBasedNetListener listener = new EventBasedNetListener();
            listener.NtpResponseEvent += ntpPacket =>
            {
                if (ntpPacket != null)
                    Console.WriteLine("[MAIN] NTP time test offset: " + ntpPacket.CorrectionOffset);
                else
                    Console.WriteLine("[MAIN] NTP time error");
                ntpComplete = true;
            };

            NetManager nm = new NetManager(listener);
            nm.Start();
            nm.CreateNtpRequest(ntpService);

            while (!ntpComplete)
            {
                Thread.Yield();
            }
        }
    }
}
