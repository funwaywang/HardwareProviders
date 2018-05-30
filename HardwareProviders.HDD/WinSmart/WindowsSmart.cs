/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>
	
*/

using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.HDD
{
    internal partial class WindowsSmart : ISmart
    {
        private const byte SMART_LBA_MID = 0x4F;
        private const byte SMART_LBA_HI = 0xC2;

       

        public IntPtr InvalidHandle => (IntPtr) (-1);

        public IntPtr OpenDrive(int driveNumber)
        {
            return NativeMethods.CreateFile(@"\\.\PhysicalDrive" + driveNumber,
                AccessMode.Read | AccessMode.Write, ShareMode.Read | ShareMode.Write,
                IntPtr.Zero, CreationMode.OpenExisting, FileAttribute.Device,
                IntPtr.Zero);
        }

        public bool EnableSmart(IntPtr handle, int driveNumber)
        {
            var parameter = new DriveCommandParameter();
            DriveCommandResult result;
            uint bytesReturned;

            parameter.DriveNumber = (byte) driveNumber;
            parameter.Registers.Features = RegisterFeature.SmartEnableOperations;
            parameter.Registers.LBAMid = SMART_LBA_MID;
            parameter.Registers.LBAHigh = SMART_LBA_HI;
            parameter.Registers.Command = RegisterCommand.SmartCmd;

            return NativeMethods.DeviceIoControl(handle, DriveCommand.SendDriveCommand,
                ref parameter, Marshal.SizeOf(typeof(DriveCommandParameter)), out result,
                Marshal.SizeOf(typeof(DriveCommandResult)), out bytesReturned,
                IntPtr.Zero);
        }

        public DriveAttributeValue[] ReadSmartData(IntPtr handle, int driveNumber)
        {
            var parameter = new DriveCommandParameter();
            uint bytesReturned;

            parameter.DriveNumber = (byte) driveNumber;
            parameter.Registers.Features = RegisterFeature.SmartReadData;
            parameter.Registers.LBAMid = SMART_LBA_MID;
            parameter.Registers.LBAHigh = SMART_LBA_HI;
            parameter.Registers.Command = RegisterCommand.SmartCmd;

            var isValid = NativeMethods.DeviceIoControl(handle,
                DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
                out DriveSmartReadDataResult result, Marshal.SizeOf(typeof(DriveSmartReadDataResult)),
                out bytesReturned, IntPtr.Zero);

            return isValid ? result.Attributes : new DriveAttributeValue[0];
        }

        public DriveThresholdValue[] ReadSmartThresholds(IntPtr handle,
            int driveNumber)
        {
            var parameter = new DriveCommandParameter();
            uint bytesReturned = 0;

            parameter.DriveNumber = (byte) driveNumber;
            parameter.Registers.Features = RegisterFeature.SmartReadThresholds;
            parameter.Registers.LBAMid = SMART_LBA_MID;
            parameter.Registers.LBAHigh = SMART_LBA_HI;
            parameter.Registers.Command = RegisterCommand.SmartCmd;

            var isValid = NativeMethods.DeviceIoControl(handle,
                DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
                out DriveSmartReadThresholdsResult result, Marshal.SizeOf(typeof(DriveSmartReadThresholdsResult)),
                out bytesReturned, IntPtr.Zero);

            return isValid ? result.Thresholds : new DriveThresholdValue[0];
        }

        public bool ReadNameAndFirmwareRevision(IntPtr handle, int driveNumber,
            out string name, out string firmwareRevision)
        {
            var parameter = new DriveCommandParameter();
            uint bytesReturned;

            parameter.DriveNumber = (byte) driveNumber;
            parameter.Registers.Command = RegisterCommand.IdCmd;

            var valid = NativeMethods.DeviceIoControl(handle,
                DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
                out DriveIdentifyResult result, Marshal.SizeOf(typeof(DriveIdentifyResult)),
                out bytesReturned, IntPtr.Zero);

            if (!valid)
            {
                name = null;
                firmwareRevision = null;
                return false;
            }

            name = GetString(result.Identify.ModelNumber);
            firmwareRevision = GetString(result.Identify.FirmwareRevision);
            return true;
        }

        public void CloseHandle(IntPtr handle)
        {
            NativeMethods.CloseHandle(handle);
        }

        public string[] GetLogicalDrives(int driveIndex)
        {
            var list = new List<string>();
            try
            {
                using (var s = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT * FROM Win32_DiskPartition " +
                    "WHERE DiskIndex = " + driveIndex))
                using (var dpc = s.Get())
                {
                    foreach (ManagementObject dp in dpc)
                        using (var ldc =
                            dp.GetRelated("Win32_LogicalDisk"))
                        {
                            foreach (var ld in ldc)
                                list.Add(((string) ld["Name"]).TrimEnd(':'));
                        }
                }
            }
            catch
            {
            }

            return list.ToArray();
        }

        private string GetString(byte[] bytes)
        {
            var chars = new char[bytes.Length];
            for (var i = 0; i < bytes.Length; i += 2)
            {
                chars[i] = (char) bytes[i + 1];
                chars[i + 1] = (char) bytes[i];
            }

            return new string(chars).Trim(' ', '\0');
        }
    }
}