using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenHardwareMonitor.Hardware
{
    static class NativeMethods
    {
        private const string KERNEL = "kernel32.dll";

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
            Opcode.AllocationType flAllocationType, Opcode.MemoryProtection flProtect);

        [DllImport(KERNEL, CallingConvention = CallingConvention.Winapi)]
        public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize,
            Opcode.FreeType dwFreeType);
    }
}
