/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware
{
    public static class ThreadAffinity
    {
        private const string KERNEL = "kernel32.dll";

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        static extern UIntPtr SetThreadAffinityMask(IntPtr handle, UIntPtr mask);

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        static extern IntPtr GetCurrentThread();

        public static ulong Set(ulong mask)
        {
            if (mask == 0)
                return 0;

            UIntPtr uIntPtrMask;
            try
            {
                uIntPtrMask = (UIntPtr) mask;
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(mask));
            }

            return (ulong) SetThreadAffinityMask(GetCurrentThread(), uIntPtrMask);
        }
    }
}