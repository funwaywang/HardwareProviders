using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;
using HardwareProviders.CPU;
using OpenHardwareMonitor.Hardware;

namespace TestAppp
{
    internal class Program
    {
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private static void Main(string[] args)
        {
            Ring0.Open();
            Opcode.Open();


            var cpus = Cpu.Discover().ToArray();

            while (true)
            {
                foreach (var cpu in cpus) cpu.Update();
                Task.Delay(1000);
            }
        }
    }
}