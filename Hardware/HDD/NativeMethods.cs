/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>
	
*/

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.HDD
{
    internal partial class WindowsSmart
    {
        protected static class NativeMethods
        {
            private const string KERNEL = "kernel32.dll";

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi,
                CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFile(string fileName,
                AccessMode desiredAccess, ShareMode shareMode, IntPtr securityAttributes,
                CreationMode creationDisposition, FileAttribute flagsAndAttributes,
                IntPtr templateFilehandle);

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
            public static extern int CloseHandle(IntPtr handle);

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(IntPtr handle,
                DriveCommand command, ref DriveCommandParameter parameter,
                int parameterSize, out DriveSmartReadDataResult result, int resultSize,
                out uint bytesReturned, IntPtr overlapped);

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(IntPtr handle,
                DriveCommand command, ref DriveCommandParameter parameter,
                int parameterSize, out DriveSmartReadThresholdsResult result,
                int resultSize, out uint bytesReturned, IntPtr overlapped);

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(IntPtr handle,
                DriveCommand command, ref DriveCommandParameter parameter,
                int parameterSize, out DriveCommandResult result, int resultSize,
                out uint bytesReturned, IntPtr overlapped);

            [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(IntPtr handle,
                DriveCommand command, ref DriveCommandParameter parameter,
                int parameterSize, out DriveIdentifyResult result, int resultSize,
                out uint bytesReturned, IntPtr overlapped);
        }
    }
}