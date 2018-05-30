/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal class CPULoad
    {
        private readonly float[] coreLoads;

        private readonly CPUID[][] cpuid;

        private long[] idleTimes;

        private float totalLoad;
        private long[] totalTimes;

        public CPULoad(CPUID[][] cpuid)
        {
            this.cpuid = cpuid;
            coreLoads = new float[cpuid.Length];
            totalLoad = 0;
            try
            {
                GetTimes(out idleTimes, out totalTimes);
            }
            catch (Exception)
            {
                idleTimes = null;
                totalTimes = null;
            }

            if (idleTimes != null)
                IsAvailable = true;
        }

        public bool IsAvailable { get; }

        private static bool GetTimes(out long[] idle, out long[] total)
        {
            var informations = new
                NativeMethods.SystemProcessorPerformanceInformation[64];

            var size = Marshal.SizeOf(typeof(NativeMethods.SystemProcessorPerformanceInformation));

            idle = null;
            total = null;

            if (NativeMethods.NtQuerySystemInformation(
                    NativeMethods.SystemInformationClass.SystemProcessorPerformanceInformation,
                    informations, informations.Length * size, out var returnLength) != 0)
                return false;

            idle = new long[(int) returnLength / size];
            total = new long[(int) returnLength / size];

            for (var i = 0; i < idle.Length; i++)
            {
                idle[i] = informations[i].IdleTime;
                total[i] = informations[i].KernelTime + informations[i].UserTime;
            }

            return true;
        }

        public float GetTotalLoad()
        {
            return totalLoad;
        }

        public float GetCoreLoad(int core)
        {
            return coreLoads[core];
        }

        public void Update()
        {
            if (idleTimes == null)
                return;

            if (!GetTimes(out var newIdleTimes, out var newTotalTimes))
                return;

            for (var i = 0; i < Math.Min(newTotalTimes.Length, totalTimes.Length); i++)
                if (newTotalTimes[i] - totalTimes[i] < 100000)
                    return;

            if (newIdleTimes == null || newTotalTimes == null)
                return;

            float total = 0;
            var count = 0;
            for (var i = 0; i < cpuid.Length; i++)
            {
                float value = 0;
                for (var j = 0; j < cpuid[i].Length; j++)
                {
                    long index = cpuid[i][j].Thread;
                    if (index < newIdleTimes.Length && index < totalTimes.Length)
                    {
                        var idle =
                            (newIdleTimes[index] - idleTimes[index]) /
                            (float) (newTotalTimes[index] - totalTimes[index]);
                        value += idle;
                        total += idle;
                        count++;
                    }
                }

                value = 1.0f - value / cpuid[i].Length;
                value = value < 0 ? 0 : value;
                coreLoads[i] = value * 100;
            }

            if (count > 0)
            {
                total = 1.0f - total / count;
                total = total < 0 ? 0 : total;
            }
            else
            {
                total = 0;
            }

            totalLoad = total * 100;

            totalTimes = newTotalTimes;
            idleTimes = newIdleTimes;
        }
    }
}