/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2011 Christian Vallières
 
*/

using System;
using System.Collections.Generic;
using HardwareProviders.GPU.Internals;

namespace HardwareProviders.GPU
{
    public class NvidiaGpu : Gpu
    {
        internal int AdapterIndex;
        internal NvDisplayHandle? DisplayHandle;
        internal NvPhysicalGpuHandle Handle;

        public Sensor[] Clocks { get; }
        public Sensor Control { get; }
        public Sensor Fan { get; }
        public Control FanControl { get; }
        public Sensor[] Loads { get; }
        public Sensor MemoryAvail { get; }
        public Sensor MemoryFree { get; }
        public Sensor MemoryLoad { get; }
        public Sensor MemoryUsed { get; }
        public Sensor[] Temperatures { get; }

        internal NvidiaGpu(int adapterIndex, NvPhysicalGpuHandle handle,NvDisplayHandle? displayHandle) : base(GetName(handle))
        {
            AdapterIndex = adapterIndex;
            Handle = handle;
            DisplayHandle = displayHandle;

            var thermalSettings = GetThermalSettings();
            Temperatures = new Sensor[thermalSettings.Count];
            for (var i = 0; i < Temperatures.Length; i++)
            {
                var sensor = thermalSettings.Sensor[i];
                string name;
                switch (sensor.Target)
                {
                    case NvThermalTarget.BOARD:
                        name = "GPU Board";
                        break;
                    case NvThermalTarget.GPU:
                        name = "GPU Core";
                        break;
                    case NvThermalTarget.MEMORY:
                        name = "GPU Memory";
                        break;
                    case NvThermalTarget.POWER_SUPPLY:
                        name = "GPU Power Supply";
                        break;
                    case NvThermalTarget.UNKNOWN:
                        name = "GPU Unknown";
                        break;
                    default:
                        name = "GPU";
                        break;
                }

                Temperatures[i] = new Sensor(name, SensorType.Temperature);
            }

            if (NVAPI.NvAPI_GPU_GetTachReading != null &&
                NVAPI.NvAPI_GPU_GetTachReading(handle, out var value) == NvStatus.OK)
                if (value >= 0)
                {
                    Fan = new Sensor("GPU", 0);
                }

            Clocks = new Sensor[3];
            Clocks[0] = new Sensor("GPU Core", 0);
            Clocks[1] = new Sensor("GPU Memory", (SensorType) 1);
            Clocks[2] = new Sensor("GPU Shader", (SensorType) 2);

            Loads = new Sensor[3];
            Loads[0] = new Sensor("GPU Core", 0);
            Loads[1] = new Sensor("GPU Memory Controller", (SensorType) 1);
            Loads[2] = new Sensor("GPU Video Engine", (SensorType) 2);
            MemoryLoad = new Sensor("GPU Memory", (SensorType) 3);
            MemoryFree = new Sensor("GPU Memory Free", (SensorType) 1);
            MemoryUsed = new Sensor("GPU Memory Used", (SensorType) 2);
            MemoryAvail = new Sensor("GPU Memory Total", (SensorType) 3);
            Control = new Sensor("GPU Fan", 0);

            GetCoolerSettings();

            Update();
        }

        public override void Update()
        {
            var settings = GetThermalSettings();
            for (var index = 0; index < Temperatures.Length; index++)
            {
                var sensor = Temperatures[index];
                sensor.Value = settings.Sensor[index].CurrentTemp;
            }

            if (Fan != null)
            {
                NVAPI.NvAPI_GPU_GetTachReading(Handle, out var value);
                Fan.Value = value;
            }

            var values = GetClocks();
            if (values != null)
            {
                Clocks[1].Value = 0.001f * values[8];
                if (values[30] != 0)
                {
                    Clocks[0].Value = 0.0005f * values[30];
                    Clocks[2].Value = 0.001f * values[30];
                }
                else
                {
                    Clocks[0].Value = 0.001f * values[0];
                    Clocks[2].Value = 0.001f * values[14];
                }
            }

            var states = new NvPStates
            {
                Version = NVAPI.GPU_PSTATES_VER,
                PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
            };
            if (NVAPI.NvAPI_GPU_GetPStates != null &&
                NVAPI.NvAPI_GPU_GetPStates(Handle, ref states) == NvStatus.OK)
            {
                for (var i = 0; i < 3; i++)
                    if (states.PStates[i].Present)
                    {
                        Loads[i].Value = states.PStates[i].Percentage;
                    }
            }
            else
            {
                var usages = new NvUsages
                {
                    Version = NVAPI.GPU_USAGES_VER,
                    Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
                };
                if (NVAPI.NvAPI_GPU_GetUsages != null &&
                    NVAPI.NvAPI_GPU_GetUsages(Handle, ref usages) == NvStatus.OK)
                {
                    Loads[0].Value = usages.Usage[2];
                    Loads[1].Value = usages.Usage[6];
                    Loads[2].Value = usages.Usage[10];
                }
            }


            var coolerSettings = GetCoolerSettings();
            if (coolerSettings.Count > 0)
            {
                Control.Value = coolerSettings.Cooler[0].CurrentLevel;
            }

            var memoryInfo = new NvMemoryInfo
            {
                Version = NVAPI.GPU_MEMORY_INFO_VER,
                Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
            };
            if (NVAPI.NvAPI_GPU_GetMemoryInfo != null && DisplayHandle.HasValue &&
                NVAPI.NvAPI_GPU_GetMemoryInfo(DisplayHandle.Value, ref memoryInfo) ==
                NvStatus.OK)
            {
                var totalMemory = memoryInfo.Values[0];
                var freeMemory = memoryInfo.Values[4];
                float usedMemory = Math.Max(totalMemory - freeMemory, 0);
                MemoryFree.Value = (float)freeMemory / 1024;
                MemoryAvail.Value = (float)totalMemory / 1024;
                MemoryUsed.Value = usedMemory / 1024;
                MemoryLoad.Value = 100f * usedMemory / totalMemory;
            }
        }

        public new static IEnumerable<NvidiaGpu> Discover()
        {
            if (!NVAPI.IsAvailable || NVAPI.NvAPI_GetInterfaceVersionString(out var version) != NvStatus.OK)
                yield break;

            var handles = new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
            if (NVAPI.NvAPI_EnumPhysicalGPUs == null)
            {
                //report.AppendLine("Error: NvAPI_EnumPhysicalGPUs not available");
                yield break;
            }

            if (NVAPI.NvAPI_EnumPhysicalGPUs(handles, out var count) != NvStatus.OK)
            {
                //report.AppendLine("Status: " + status);
                //report.AppendLine();
                yield break;
            }

            IDictionary<NvPhysicalGpuHandle, NvDisplayHandle> displayHandles = new Dictionary<NvPhysicalGpuHandle, NvDisplayHandle>();

            if (NVAPI.NvAPI_EnumNvidiaDisplayHandle != null && NVAPI.NvAPI_GetPhysicalGPUsFromDisplay != null)
            {
                var status = NvStatus.OK;
                var i = 0;
                while (status == NvStatus.OK)
                {
                    var displayHandle = new NvDisplayHandle();
                    status = NVAPI.NvAPI_EnumNvidiaDisplayHandle(i, ref displayHandle);
                    i++;

                    if (status == NvStatus.OK)
                    {
                        var handlesFromDisplay = new NvPhysicalGpuHandle[NVAPI.MAX_PHYSICAL_GPUS];
                        if (NVAPI.NvAPI_GetPhysicalGPUsFromDisplay(displayHandle, handlesFromDisplay, out var countFromDisplay) == NvStatus.OK)
                            for (var j = 0; j < countFromDisplay; j++)
                                if (!displayHandles.ContainsKey(handlesFromDisplay[j]))
                                    displayHandles.Add(handlesFromDisplay[j], displayHandle);
                    }
                }
            }

            //report.Append("Number of GPUs: ");
            //report.AppendLine(count.ToString(CultureInfo.InvariantCulture));

            for (var i = 0; i < count; i++)
            {
                displayHandles.TryGetValue(handles[i], out var displayHandle);
                yield return (new NvidiaGpu(i, handles[i], displayHandle));
            }
        }

        private static string GetName(NvPhysicalGpuHandle handle)
        {
            return NVAPI.NvAPI_GPU_GetFullName(handle, out var gpuName) == NvStatus.OK
                ? "NVIDIA " + gpuName.Trim()
                : "NVIDIA";
        }

        private NvGPUThermalSettings GetThermalSettings()
        {
            var settings = new NvGPUThermalSettings
            {
                Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
                Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
                Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
            };
            if (!(NVAPI.NvAPI_GPU_GetThermalSettings != null &&
                  NVAPI.NvAPI_GPU_GetThermalSettings(Handle, (int) NvThermalTarget.ALL,
                      ref settings) == NvStatus.OK))
                settings.Count = 0;
            return settings;
        }

        private NvGPUCoolerSettings GetCoolerSettings()
        {
            var settings = new NvGPUCoolerSettings
            {
                Version = NVAPI.GPU_COOLER_SETTINGS_VER,
                Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
            };
            if (!(NVAPI.NvAPI_GPU_GetCoolerSettings != null &&
                  NVAPI.NvAPI_GPU_GetCoolerSettings(Handle, 0,
                      ref settings) == NvStatus.OK))
                settings.Count = 0;
            return settings;
        }

        private uint[] GetClocks()
        {
            var allClocks = new NvClocks
            {
                Version = NVAPI.GPU_CLOCKS_VER,
                Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
            };
            if (NVAPI.NvAPI_GPU_GetAllClocks != null &&
                NVAPI.NvAPI_GPU_GetAllClocks(Handle, ref allClocks) == NvStatus.OK)
                return allClocks.Clock;
            return null;
        }

        private void SoftwareControlValueChanged(Control control)
        {
            var coolerLevels = new NvGPUCoolerLevels
            {
                Version = NVAPI.GPU_COOLER_LEVELS_VER,
                Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU]
            };
            coolerLevels.Levels[0].Level = (int) control.SoftwareValue;
            coolerLevels.Levels[0].Policy = 1;
            NVAPI.NvAPI_GPU_SetCoolerLevels(Handle, 0, ref coolerLevels);
        }

        private void ControlModeChanged(Control control)
        {
            switch (control.ControlMode)
            {
                case ControlMode.Undefined:
                    return;
                case ControlMode.Default:
                    SetDefaultFanSpeed();
                    break;
                case ControlMode.Software:
                    SoftwareControlValueChanged(control);
                    break;
                default:
                    return;
            }
        }

        private void SetDefaultFanSpeed()
        {
            var coolerLevels = new NvGPUCoolerLevels
            {
                Version = NVAPI.GPU_COOLER_LEVELS_VER,
                Levels = new NvLevel[NVAPI.MAX_COOLER_PER_GPU]
            };
            coolerLevels.Levels[0].Policy = 0x20;
            NVAPI.NvAPI_GPU_SetCoolerLevels(Handle, 0, ref coolerLevels);
        }
    }
}