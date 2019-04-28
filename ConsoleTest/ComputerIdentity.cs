using HardwareProviders.Board;
using HardwareProviders.CPU;
using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace ConsoleTest
{
    public class ComputerIdentity
    {
        public string MachineName { get; set; }

        public OsIdentity Os { get; set; }

        public BoardIdentity Board { get; set; }

        public CpuIdentity[] Cpus { get; set; }

        public NetworkInterfaceIdentity[] NetworkInterfaces { get; set; }

        public static ComputerIdentity Collect()
        {
            var mainboard = new Mainboard();
            var cpus = Cpu.Discover();
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            var identity = new ComputerIdentity()
            {
                MachineName = Environment.MachineName,
                Os = new OsIdentity
                {
                    Name = Environment.OSVersion.Platform.ToString(),
                    Version = Environment.OSVersion.VersionString,
                },
                Board = new BoardIdentity
                {
                    Name = mainboard.Name,
                    Model = mainboard.Model.ToString(),
                    Manufacturer = mainboard.Manufacturer.ToString(),
                    SerialNumber = mainboard.Smbios?.Board?.SerialNumber
                },
                Cpus = (from cpu in cpus
                        select new CpuIdentity
                        {
                            Name = cpu.Name,
                            Identifier = cpu.Identifier,
                            Vendor = cpu.Vendor
                        }).ToArray(),
                NetworkInterfaces = (from ni in networkInterfaces
                                     where ni.OperationalStatus == OperationalStatus.Up
                                     let physicalAddress = ni.GetPhysicalAddress()?.ToString()
                                     where !string.IsNullOrWhiteSpace(physicalAddress)
                                     select new NetworkInterfaceIdentity
                                     {
                                         Name = ni.Name,
                                         Id = ni.Id,
                                         Type = ni.NetworkInterfaceType.ToString(),
                                         PhysicalAddress = physicalAddress
                                     }).ToArray()
            };

            return identity;
        }
    }

    public class OsIdentity
    {
        public string Name { get; set; }

        public string Version { get; set; }
    }

    public class BoardIdentity
    {
        public string Model { get; set; }

        public string Manufacturer { get; set; }

        public string Name { get; set; }

        public string SerialNumber { get; set; }
    }

    public class CpuIdentity
    {
        public string Name { get; set; }

        public string Identifier { get; set; }

        public Vendor Vendor { get; set; }
    }

    public class NetworkInterfaceIdentity
    {
        public string Name { get; set; }

        public string Id { get; set; }

        public string Type { get; set; }

        public string PhysicalAddress { get; set; }
    }
}
