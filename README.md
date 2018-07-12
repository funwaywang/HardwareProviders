# Hardware Providers

Collection of libraries to explore hardware installed on the machine and relative parameters, including: clock, voltages and temperatures.
For Dotnet Standard and 4.6

| Component        | Purpose  |  Standard  | .Net 4.6      |
| ------------- | ------------- | ------------- | ------------- |
| HardwareProviders  | Contains base classes and interface to read and write directly on pc ports  | ✓  | ✓
| HardwareProviders.CPU  | Retrieves Intel and AMD processors installed and relative values | ✓  | ✓
| HardwareProviders.GPU | Retrieves Nvidia and ATI graphic cards installed and relative values  | June 2018  | June 2018
| HardwareProviders.Cooling | Retrieves installed cooling devices and fans  | June 2018  | June 2018
| HardwareProviders.HDD | Retrieves hard drives and relative values  | July 2018  | July 2018

This project contains code extracted from [Open Hardware Monitor](https://github.com/openhardwaremonitor) and is released under the same  [license](https://github.com/matteofabbri/HardwareProviders/blob/master/LICENSE)


## CPU library Usage
Retrieving information about the current state of CPUs is incredibly simple.

TROUBLESHOOTING:
Depending on Windows version you may need to run it as administrator to retrieve all values.



```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using HardwareProviders;
using HardwareProviders.CPU;

namespace Maddalena
{
    public class Program
    {
        static string SensorsToString(IEnumerable<Sensor> sensors) => string.Join(" ", sensors?.Select(x => x.ToString()) ?? new string[0]);

        public static void Main(string[] args)
        {
            var cpus = new CpuCollection();

            while (true)
            {
                //Read current values of every vpu
                cpus.Update();

                foreach (var cpu in cpus)
                {
                    Console.WriteLine("CPU {0} by {1}", cpu.Name, cpu.Vendor);

                    Console.WriteLine("Bus clock {0}", cpu.BusClock);
                    Console.WriteLine("Core temperatures {0}", SensorsToString(cpu.CoreTemperatures));
                    Console.WriteLine("Core powers {0}", SensorsToString(cpu.CorePowers));
                    Console.WriteLine("Core clocks {0}", SensorsToString(cpu.CoreClocks));

                    Console.WriteLine();
                    Console.WriteLine("Core loads {0}", SensorsToString(cpu.CoreLoads));
                    Console.WriteLine("Total load {0}", cpu.TotalLoad);
                }

                Task.Delay(1000).Wait();
            }
        }
}
```
