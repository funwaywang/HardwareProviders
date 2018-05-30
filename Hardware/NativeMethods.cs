using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware
{
    public static class NativeMethods
    {
        private const string KERNEL = "kernel32.dll";

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
            Opcode.AllocationType flAllocationType, Opcode.MemoryProtection flProtect);

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize,
            Opcode.FreeType dwFreeType);

        [DllImport("ntdll.dll")]
        public static extern int NtQuerySystemInformation(SystemInformationClass informationClass, [Out] SystemProcessorPerformanceInformation[] informations, int structSize, out IntPtr returnLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct SystemProcessorPerformanceInformation
        {
            public long IdleTime;
            public long KernelTime;
            public long UserTime;
            public long Reserved0;
            public long Reserved1;
            public ulong Reserved2;
        }

        public enum SystemInformationClass
        {
            SystemBasicInformation = 0,
            SystemCpuInformation = 1,
            SystemPerformanceInformation = 2,
            SystemTimeOfDayInformation = 3,
            SystemProcessInformation = 5,
            SystemProcessorPerformanceInformation = 8
        }
    }
}
