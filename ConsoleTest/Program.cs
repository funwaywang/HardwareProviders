using HardwareProviders.HDD;
using System;
using System.Management;

namespace ConsoleTest
{
    class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hard Disks");
            var hds = HardDrive.Discover();
            foreach (var hd in hds)
            {
                Console.WriteLine($"{hd.Name} {hd.Identifier}");
            }

            var identity = ComputerIdentity.Collect();

            Console.WriteLine(identity.MachineName);

            Console.WriteLine();
            if (identity.Os != null)
            {
                Console.WriteLine("OS:");
                Console.WriteLine(identity.Os.Name);
                Console.WriteLine(identity.Os.Version);
            }

            Console.WriteLine();
            if (identity.Board != null)
            {
                Console.WriteLine("Board:");
                Console.WriteLine(identity.Board.Name);
                Console.WriteLine(identity.Board.Model);
                Console.WriteLine(identity.Board.Manufacturer);
                Console.WriteLine(identity.Board.SerialNumber);
            }

            Console.WriteLine();
            if (identity.Cpus != null)
            {
                Console.WriteLine("CPUs:");
                foreach (var cpu in identity.Cpus)
                {
                    Console.WriteLine($"{cpu.Name} {cpu.Vendor} {cpu.Identifier}");
                }
            }

            Console.WriteLine();
            if (identity.NetworkInterfaces != null)
            {
                Console.WriteLine("Network Interfaces:");
                foreach (var ni in identity.NetworkInterfaces)
                {
                    Console.WriteLine($"{ni.PhysicalAddress} {ni.Name} {ni.Id}");
                }
            }

            Console.ReadKey();
        }
    }
}
