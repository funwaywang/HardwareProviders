/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>
	
*/

using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.HDD
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Identify
    {
        public ushort GeneralConfiguration;
        public ushort NumberOfCylinders;
        public ushort Reserved;
        public ushort NumberOfHeads;
        public ushort UnformattedBytesPerTrack;
        public ushort UnformattedBytesPerSector;
        public ushort SectorsPerTrack;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] VendorUnique;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] SerialNumber;

        public ushort BufferType;
        public ushort BufferSectorSize;
        public ushort NumberOfEccBytes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] FirmwareRevision;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] ModelNumber;

        public ushort MoreVendorUnique;
        public ushort DoubleWordIo;
        public ushort Capabilities;
        public ushort MoreReserved;
        public ushort PioCycleTimingMode;
        public ushort DmaCycleTimingMode;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 406)]
        public byte[] More;
    }
}