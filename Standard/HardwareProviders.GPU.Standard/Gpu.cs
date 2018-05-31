using System.Collections.Generic;
using System.Linq;

namespace HardwareProviders.GPU
{
    public abstract class Gpu : Hardware
    {
        protected Gpu(string name) : base(name)
        {
        }

        public static IEnumerable<Gpu> Discover()
        {
            return NvidiaGpu.Discover().Concat(AtiGpu.Discover().Cast<Gpu>());
        }
    }
}
