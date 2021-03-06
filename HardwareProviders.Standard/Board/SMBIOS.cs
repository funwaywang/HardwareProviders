/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;

namespace HardwareProviders.Board
{
    public class Smbios
    {
        private readonly byte[] raw;
        private readonly Structure[] table;

        private readonly Version version;

        public Smbios()
        {
            if (OperatingSystem.IsLinux)
            {
                raw = null;
                table = null;

                var boardVendor = ReadSysFS("/sys/class/dmi/id/board_vendor");
                var boardName = ReadSysFS("/sys/class/dmi/id/board_name");
                var boardVersion = ReadSysFS("/sys/class/dmi/id/board_version");
                var boardSerial = ReadSysFS("/sys/class/dmi/id/board_serial");
                Board = new BaseBoardInformation(
                    boardVendor, boardName, boardVersion, boardSerial);

                var systemVendor = ReadSysFS("/sys/class/dmi/id/sys_vendor");
                var productName = ReadSysFS("/sys/class/dmi/id/product_name");
                var productVersion = ReadSysFS("/sys/class/dmi/id/product_version");
                var productSerial = ReadSysFS("/sys/class/dmi/id/product_serial");
                System = new SystemInformation(systemVendor,
                    productName, productVersion, productSerial, null);

                var biosVendor = ReadSysFS("/sys/class/dmi/id/bios_vendor");
                var biosVersion = ReadSysFS("/sys/class/dmi/id/bios_version");
                BIOS = new BIOSInformation(biosVendor, biosVersion);

                MemoryDevices = new MemoryDevice[0];
            }
            else
            {
                var structureList = new List<Structure>();
                var memoryDeviceList = new List<MemoryDevice>();

                raw = null;
                byte majorVersion = 0;
                byte minorVersion = 0;
                try
                {
                    ManagementObjectCollection collection;
                    using (var searcher =
                        new ManagementObjectSearcher("root\\WMI",
                            "SELECT * FROM MSSMBios_RawSMBiosTables"))
                    {
                        collection = searcher.Get();
                    }

                    foreach (ManagementObject mo in collection)
                    {
                        raw = (byte[])mo["SMBiosData"];
                        majorVersion = (byte)mo["SmbiosMajorVersion"];
                        minorVersion = (byte)mo["SmbiosMinorVersion"];
                        break;
                    }
                }
                catch
                {
                }

                if (majorVersion > 0 || minorVersion > 0)
                {
                    version = new Version(majorVersion, minorVersion);
                }

                if (raw != null && raw.Length > 0)
                {
                    var offset = 0;
                    var type = raw[offset];
                    while (offset + 4 < raw.Length && type != 127)
                    {
                        type = raw[offset];
                        int length = raw[offset + 1];
                        var handle = (ushort)((raw[offset + 2] << 8) | raw[offset + 3]);

                        if (offset + length > raw.Length)
                        {
                            break;
                        }

                        var data = new byte[length];
                        Array.Copy(raw, offset, data, 0, length);
                        offset += length;

                        var stringsList = new List<string>();
                        if (offset < raw.Length && raw[offset] == 0)
                        {
                            offset++;
                        }

                        while (offset < raw.Length && raw[offset] != 0)
                        {
                            var sb = new StringBuilder();
                            while (offset < raw.Length && raw[offset] != 0)
                            {
                                sb.Append((char)raw[offset]);
                                offset++;
                            }

                            offset++;
                            stringsList.Add(sb.ToString());
                        }

                        offset++;
                        switch (type)
                        {
                            case 0x00:
                                BIOS = new BIOSInformation(
                                    type, handle, data, stringsList.ToArray());
                                structureList.Add(BIOS);
                                break;
                            case 0x01:
                                System = new SystemInformation(
                                    type, handle, data, stringsList.ToArray());
                                structureList.Add(System);
                                break;
                            case 0x02:
                                Board = new BaseBoardInformation(
                                    type, handle, data, stringsList.ToArray());
                                structureList.Add(Board);
                                break;
                            case 0x04:
                                Processor = new ProcessorInformation(
                                    type, handle, data, stringsList.ToArray());
                                structureList.Add(Processor);
                                break;
                            case 0x11:
                                var m = new MemoryDevice(
                                    type, handle, data, stringsList.ToArray());
                                memoryDeviceList.Add(m);
                                structureList.Add(m);
                                break;
                            default:
                                structureList.Add(new Structure(
                                    type, handle, data, stringsList.ToArray()));
                                break;
                        }
                    }
                }

                MemoryDevices = memoryDeviceList.ToArray();
                table = structureList.ToArray();
            }
        }

        public BIOSInformation BIOS { get; }

        public SystemInformation System { get; }

        public BaseBoardInformation Board { get; }


        public ProcessorInformation Processor { get; }

        public MemoryDevice[] MemoryDevices { get; }

        private static string ReadSysFS(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (var reader = new StreamReader(path))
                    {
                        return reader.ReadLine();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public class Structure
        {
            private readonly byte[] data;
            private readonly string[] strings;

            public Structure(byte type, ushort handle, byte[] data, string[] strings)
            {
                Type = type;
                Handle = handle;
                this.data = data;
                this.strings = strings;
            }

            public byte Type { get; }

            public ushort Handle { get; }

            protected int GetByte(int offset)
            {
                if (offset < data.Length && offset >= 0)
                {
                    return data[offset];
                }

                return 0;
            }

            protected int GetWord(int offset)
            {
                if (offset + 1 < data.Length && offset >= 0)
                {
                    return (data[offset + 1] << 8) | data[offset];
                }

                return 0;
            }

            protected string GetString(int offset)
            {
                if (offset < data.Length && data[offset] > 0 &&
                    data[offset] <= strings.Length)
                {
                    return strings[data[offset] - 1];
                }

                return "";
            }
        }

        public class BIOSInformation : Structure
        {
            public BIOSInformation(string vendor, string version)
                : base(0x00, 0, null, null)
            {
                Vendor = vendor;
                Version = version;
            }

            public BIOSInformation(byte type, ushort handle, byte[] data,
                string[] strings)
                : base(type, handle, data, strings)
            {
                Vendor = GetString(0x04);
                Version = GetString(0x05);
            }

            public string Vendor { get; }

            public string Version { get; }
        }

        public class SystemInformation : Structure
        {
            public SystemInformation(string manufacturerName, string productName,
                string version, string serialNumber, string family)
                : base(0x01, 0, null, null)
            {
                ManufacturerName = manufacturerName;
                ProductName = productName;
                Version = version;
                SerialNumber = serialNumber;
                Family = family;
            }

            public SystemInformation(byte type, ushort handle, byte[] data,
                string[] strings)
                : base(type, handle, data, strings)
            {
                ManufacturerName = GetString(0x04);
                ProductName = GetString(0x05);
                Version = GetString(0x06);
                SerialNumber = GetString(0x07);
                Family = GetString(0x1A);
            }

            public string ManufacturerName { get; }

            public string ProductName { get; }

            public string Version { get; }

            public string SerialNumber { get; }

            public string Family { get; }
        }

        public class BaseBoardInformation : Structure
        {
            public BaseBoardInformation(string manufacturerName, string productName,
                string version, string serialNumber)
                : base(0x02, 0, null, null)
            {
                ManufacturerName = manufacturerName;
                ProductName = productName;
                Version = version;
                SerialNumber = serialNumber;
            }

            public BaseBoardInformation(byte type, ushort handle, byte[] data,
                string[] strings)
                : base(type, handle, data, strings)
            {
                ManufacturerName = GetString(0x04).Trim();
                ProductName = GetString(0x05).Trim();
                Version = GetString(0x06).Trim();
                SerialNumber = GetString(0x07).Trim();
            }

            public string ManufacturerName { get; }

            public string ProductName { get; }

            public string Version { get; }

            public string SerialNumber { get; }
        }

        public class ProcessorInformation : Structure
        {
            public ProcessorInformation(byte type, ushort handle, byte[] data,
                string[] strings)
                : base(type, handle, data, strings)
            {
                ManufacturerName = GetString(0x07).Trim();
                Version = GetString(0x10).Trim();
                CoreCount = GetByte(0x23);
                CoreEnabled = GetByte(0x24);
                ThreadCount = GetByte(0x25);
                ExternalClock = GetWord(0x12);
            }

            public string ManufacturerName { get; }

            public string Version { get; }

            public int CoreCount { get; }

            public int CoreEnabled { get; }

            public int ThreadCount { get; }

            public int ExternalClock { get; }
        }

        public class MemoryDevice : Structure
        {
            public MemoryDevice(byte type, ushort handle, byte[] data,
                string[] strings)
                : base(type, handle, data, strings)
            {
                DeviceLocator = GetString(0x10).Trim();
                BankLocator = GetString(0x11).Trim();
                ManufacturerName = GetString(0x17).Trim();
                SerialNumber = GetString(0x18).Trim();
                PartNumber = GetString(0x1A).Trim();
                Speed = GetWord(0x15);
            }

            public string DeviceLocator { get; }

            public string BankLocator { get; }

            public string ManufacturerName { get; }

            public string SerialNumber { get; }

            public string PartNumber { get; }

            public int Speed { get; }
        }
    }
}