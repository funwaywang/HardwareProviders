using System.Linq;
using System.Security.Permissions;
using System.Threading.Tasks;
using HardwareProviders.CPU;

namespace TestApp.Standard
{
    class Program
    {
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        private static void Main(string[] args)
        {
            var cpus = Cpu.Discover().ToArray();

            while (true)
            {
                foreach (var cpu in cpus) cpu.Update();
                Task.Delay(2000).Wait();
            }
        }
    }
}
