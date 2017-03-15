using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
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

        var allRunnableTypes =
            Assembly.GetEntryAssembly().GetTypes().Where(t => !t.IsAbstract && typeof(IRunnable).IsAssignableFrom(t)).ToArray();
        
        do
        {
            Console.WriteLine("LiteNetLib Tests:");
            for (var i = 0; i < allRunnableTypes.Length; i++)
            {
                Console.WriteLine($"{1 + i} - {allRunnableTypes[i].Name}");
            }
            Console.WriteLine("Q - Exit");
            Console.WriteLine();
            Console.Write("Input: ");
            var input = Console.ReadLine();

            if (!string.IsNullOrEmpty(input) && input.ToLower() == "q")
            {
                break;
            }
            
            int inputInt;
            if (int.TryParse(input, out inputInt) && inputInt > 0 && inputInt <= allRunnableTypes.Length)
            {
                var testType = allRunnableTypes[inputInt-1];
                Console.WriteLine($"Run test: {testType.FullName}");
                Console.WriteLine($"=============[Start]===========[{testType.Name}]====================================");
                var runnable = (IRunnable)Activator.CreateInstance(testType);
                runnable.Run();
                Console.WriteLine($"=============[End]=============[{testType.Name}]====================================");

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                break;
            }
            else
            {
                Console.WriteLine("Wrong input");
            }
        } while (true);
    }
}
