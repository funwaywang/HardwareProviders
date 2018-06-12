using HardwareProviders;
using HardwareProviders.CPU;

namespace Maddalena.Modules.Hardware
{
    public class CpuModel
    {
        public string Name { get; set; }
        public Vendor Vendor { get; set; }
        public Sensor[] CorePowers { get; set; }
        public Sensor[] CoreTemperatures { get; set; }
        public Sensor[] CoreClocks { get; set; }
        public Sensor BusClock { get; set; }
        public Sensor PackageTemperature { get; set; }
        public Sensor[] CoreLoads { get; set; }
        public bool HasTimeStampCounter { get; set; }
        public int CoreCount { get; set; }
        public Sensor TotalLoad { get; set; }
        public double TimeStampCounterFrequency { get; set; }
    }
}
