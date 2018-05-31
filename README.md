# Hardware Providers

Collection of libraries to explore hardware installed on the machine and relative parameters, including: clock, voltages and temperatures.
For Dotnet Standard and 4.6

| Component        | Purpose  |  Standard  | .Net 4.6      |
| ------------- | ------------- | ------------- | ------------- |
| HardwareProviders  | Contains base classes and interface to read and write directly on pc ports  | ✓  | ✓
| HardwareProviders.CPU  | Retrieves Intel and AMD processors installed and relative values | ✓  | ✓
| HardwareProviders.GPU | Retrieves Nvidia and ATI graphic cards installed and relative values  | June 2018  | June 2018
| HardwareProviders.Cooling | Retrieves installed cooling devices and fans  | June 2018  | June 2018
| HardwareProviders.HDD | Retrieves hard drives and relative values  | July 2018  | July 2018

This project contains code extracted from [Open Hardware Monitor](https://github.com/openhardwaremonitor) and is released under the same  [license](https://github.com/matteofabbri/HardwareProviders/blob/master/LICENSE)
