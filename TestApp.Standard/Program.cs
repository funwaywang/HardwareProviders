using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;
using HardwareProviders;
using HardwareProviders.CPU;

namespace TestApp.Standard
{
    class Program
    {
        static string SensorsToString(IEnumerable<Sensor> sensors) => string.Join(" ", sensors?.Select(x => x.ToString()) ?? new string[0]);

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private static void Main(string[] args)
        {
            var cpus = Cpu.Discover().ToArray();

            while (true)
            {
                foreach (var cpu in cpus)
                {
                    cpu.Update();
                    Console.WriteLine("CPU {0} by {1}", cpu.Name, cpu.Vendor);

                    Console.WriteLine("Bus clock {0}", cpu.BusClock);
                    Console.WriteLine("Core temperatures {0}", SensorsToString(cpu.CoreTemperatures));
                    Console.WriteLine("Core powers {0}", SensorsToString(cpu.CorePowers));
                    Console.WriteLine("Core clocks {0}", SensorsToString(cpu.CoreClocks));

                    Console.WriteLine();
                    Console.WriteLine("Core loads {0}", SensorsToString(cpu.CoreLoads));
                    Console.WriteLine("Total load {0}", cpu.TotalLoad);
                }
                
                Task.Delay(2000).Wait();
            }
        }
    }
}
