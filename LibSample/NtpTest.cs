using System;
using System.Threading;
using LiteNetLib.Utils;

namespace LibSample
{
    public class NtpTest : IExample
    {
        private readonly string[] _ntpServices = {
            "time.windows.com",
            "pool.ntp.org",
            "time.nist.gov"
        };

        public void Run()
        {
            foreach (var ntpService in _ntpServices)
            {
                RequestNtpService(ntpService);
            }
        }

        private void RequestNtpService(string ntpService)
        {
            Console.WriteLine($"Request time from \"{ntpService}\" service");
#if NETSTANDARD || NETCOREAPP
           var result = NtpRequest.RequestAsync(ntpService).GetAwaiter().GetResult();
            if (result != null)
            {
                Console.WriteLine("[MAIN] NTP time test offset: " + result.CorrectionOffset);
            }
            else
            {
                Console.WriteLine("[MAIN] NTP time error");
            }
#else
            var ntpComplete = false;
            //Test ntp
            NtpRequest ntpRequest = null;
            ntpRequest = NtpRequest.Create(ntpService, ntpPacket =>
            {
                ntpRequest.Close();
                if (ntpPacket != null)
                    Console.WriteLine("[MAIN] NTP time test offset: " + ntpPacket.CorrectionOffset);
                else
                    Console.WriteLine("[MAIN] NTP time error");
                ntpComplete = true;
            });
            ntpRequest.Send();
            while (!ntpComplete)
            {
                Thread.Yield();
            }
#endif
        }
    }
}