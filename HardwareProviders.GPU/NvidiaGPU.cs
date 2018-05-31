/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2011 Christian Vallières
 
*/

using System;
using System.Globalization;
using System.Text;
using HardwareProviders.GPU.Nvidia;
using OpenHardwareMonitor.Hardware;

namespace HardwareProviders.GPU
{
    internal class NvidiaGpu : Hardware
    {
        public int AdapterIndex;
        public NvDisplayHandle? DisplayHandle;
        public NvPhysicalGpuHandle Handle;

        public NvidiaGpu(int adapterIndex, NvPhysicalGpuHandle handle,
            NvDisplayHandle? displayHandle)
            : base(GetName(handle), new Identifier("nvidiagpu",
                adapterIndex.ToString(CultureInfo.InvariantCulture)))
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

                Temperatures[i] = new Sensor(name, i, SensorType.Temperature, this,
                    new Parameter[0]);
                ActivateSensor(Temperatures[i]);
            }

            int value;
            if (NVAPI.NvAPI_GPU_GetTachReading != null &&
                NVAPI.NvAPI_GPU_GetTachReading(handle, out value) == NvStatus.OK)
                if (value >= 0)
                {
                    Fan = new Sensor("GPU", 0, SensorType.Fan, this);
                    ActivateSensor(Fan);
                }

            Clocks = new Sensor[3];
            Clocks[0] = new Sensor("GPU Core", 0, SensorType.Clock, this);
            Clocks[1] = new Sensor("GPU Memory", 1, SensorType.Clock, this);
            Clocks[2] = new Sensor("GPU Shader", 2, SensorType.Clock, this);
            for (var i = 0; i < Clocks.Length; i++)
                ActivateSensor(Clocks[i]);

            Loads = new Sensor[3];
            Loads[0] = new Sensor("GPU Core", 0, SensorType.Load, this);
            Loads[1] = new Sensor("GPU Memory Controller", 1, SensorType.Load, this);
            Loads[2] = new Sensor("GPU Video Engine", 2, SensorType.Load, this);
            MemoryLoad = new Sensor("GPU Memory", 3, SensorType.Load, this);
            MemoryFree = new Sensor("GPU Memory Free", 1, SensorType.SmallData, this);
            MemoryUsed = new Sensor("GPU Memory Used", 2, SensorType.SmallData, this);
            MemoryAvail = new Sensor("GPU Memory Total", 3, SensorType.SmallData, this);
            Control = new Sensor("GPU Fan", 0, SensorType.Control, this);

            var coolerSettings = GetCoolerSettings();
            if (coolerSettings.Count > 0)
            {
                FanControl = new Control(Control,
                    coolerSettings.Cooler[0].DefaultMin,
                    coolerSettings.Cooler[0].DefaultMax);
                FanControl.ControlModeChanged += ControlModeChanged;
                FanControl.SoftwareControlValueChanged += SoftwareControlValueChanged;
                ControlModeChanged(FanControl);
                Control.Control = FanControl;
            }

            Update();
        }

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

        public override HardwareType HardwareType => HardwareType.GpuNvidia;

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

        public override void Update()
        {
            var settings = GetThermalSettings();
            foreach (var sensor in Temperatures)
                sensor.Value = settings.Sensor[sensor.Index].CurrentTemp;

            if (Fan != null)
            {
                var value = 0;
                NVAPI.NvAPI_GPU_GetTachReading(Handle, out value);
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
                        ActivateSensor(Loads[i]);
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
                    for (var i = 0; i < 3; i++)
                        ActivateSensor(Loads[i]);
                }
            }


            var coolerSettings = GetCoolerSettings();
            if (coolerSettings.Count > 0)
            {
                Control.Value = coolerSettings.Cooler[0].CurrentLevel;
                ActivateSensor(Control);
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
                MemoryFree.Value = (float) freeMemory / 1024;
                MemoryAvail.Value = (float) totalMemory / 1024;
                MemoryUsed.Value = usedMemory / 1024;
                MemoryLoad.Value = 100f * usedMemory / totalMemory;
                ActivateSensor(MemoryAvail);
                ActivateSensor(MemoryUsed);
                ActivateSensor(MemoryFree);
                ActivateSensor(MemoryLoad);
            }
        }

        public override string GetReport()
        {
            var r = new StringBuilder();

            r.AppendLine("Nvidia GPU");
            r.AppendLine();

            r.AppendFormat("Name: {0}{1}", Name, Environment.NewLine);
            r.AppendFormat("Index: {0}{1}", AdapterIndex, Environment.NewLine);

            if (DisplayHandle.HasValue && NVAPI.NvAPI_GetDisplayDriverVersion != null)
            {
                var driverVersion = new NvDisplayDriverVersion {Version = NVAPI.DISPLAY_DRIVER_VERSION_VER};
                if (NVAPI.NvAPI_GetDisplayDriverVersion(DisplayHandle.Value,
                        ref driverVersion) == NvStatus.OK)
                {
                    r.Append("Driver Version: ");
                    r.Append(driverVersion.DriverVersion / 100);
                    r.Append(".");
                    r.Append((driverVersion.DriverVersion % 100).ToString("00",
                        CultureInfo.InvariantCulture));
                    r.AppendLine();
                    r.Append("Driver Branch: ");
                    r.AppendLine(driverVersion.BuildBranch);
                }
            }

            r.AppendLine();

            if (NVAPI.NvAPI_GPU_GetPCIIdentifiers != null)
            {
                var status = NVAPI.NvAPI_GPU_GetPCIIdentifiers(Handle,
                    out var deviceId, out var subSystemId, out var revisionId, out var extDeviceId);

                if (status == NvStatus.OK)
                {
                    r.Append("DeviceID: 0x");
                    r.AppendLine(deviceId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("SubSystemID: 0x");
                    r.AppendLine(subSystemId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("RevisionID: 0x");
                    r.AppendLine(revisionId.ToString("X", CultureInfo.InvariantCulture));
                    r.Append("ExtDeviceID: 0x");
                    r.AppendLine(extDeviceId.ToString("X", CultureInfo.InvariantCulture));
                    r.AppendLine();
                }
            }

            if (NVAPI.NvAPI_GPU_GetThermalSettings != null)
            {
                var settings = new NvGPUThermalSettings
                {
                    Version = NVAPI.GPU_THERMAL_SETTINGS_VER,
                    Count = NVAPI.MAX_THERMAL_SENSORS_PER_GPU,
                    Sensor = new NvSensor[NVAPI.MAX_THERMAL_SENSORS_PER_GPU]
                };

                var status = NVAPI.NvAPI_GPU_GetThermalSettings(Handle,
                    (int) NvThermalTarget.ALL, ref settings);

                r.AppendLine("Thermal Settings");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < settings.Count; i++)
                    {
                        r.AppendFormat(" Sensor[{0}].Controller: {1}{2}", i,
                            settings.Sensor[i].Controller, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].DefaultMinTemp: {1}{2}", i,
                            settings.Sensor[i].DefaultMinTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].DefaultMaxTemp: {1}{2}", i,
                            settings.Sensor[i].DefaultMaxTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].CurrentTemp: {1}{2}", i,
                            settings.Sensor[i].CurrentTemp, Environment.NewLine);
                        r.AppendFormat(" Sensor[{0}].Target: {1}{2}", i,
                            settings.Sensor[i].Target, Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetAllClocks != null)
            {
                var allClocks = new NvClocks
                {
                    Version = NVAPI.GPU_CLOCKS_VER,
                    Clock = new uint[NVAPI.MAX_CLOCKS_PER_GPU]
                };
                var status = NVAPI.NvAPI_GPU_GetAllClocks(Handle, ref allClocks);

                r.AppendLine("Clocks");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < allClocks.Clock.Length; i++)
                        if (allClocks.Clock[i] > 0)
                            r.AppendFormat(" Clock[{0}]: {1}{2}", i, allClocks.Clock[i],
                                Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetTachReading != null)
            {
                var status = NVAPI.NvAPI_GPU_GetTachReading(Handle, out var tachValue);

                r.AppendLine("Tachometer");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    r.AppendFormat(" Value: {0}{1}", tachValue, Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetPStates != null)
            {
                var states = new NvPStates
                {
                    Version = NVAPI.GPU_PSTATES_VER,
                    PStates = new NvPState[NVAPI.MAX_PSTATES_PER_GPU]
                };
                var status = NVAPI.NvAPI_GPU_GetPStates(Handle, ref states);

                r.AppendLine("P-States");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < states.PStates.Length; i++)
                        if (states.PStates[i].Present)
                            r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
                                states.PStates[i].Percentage, Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetUsages != null)
            {
                var usages = new NvUsages
                {
                    Version = NVAPI.GPU_USAGES_VER,
                    Usage = new uint[NVAPI.MAX_USAGES_PER_GPU]
                };
                var status = NVAPI.NvAPI_GPU_GetUsages(Handle, ref usages);

                r.AppendLine("Usages");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < usages.Usage.Length; i++)
                        if (usages.Usage[i] > 0)
                            r.AppendFormat(" Usage[{0}]: {1}{2}", i,
                                usages.Usage[i], Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetCoolerSettings != null)
            {
                var settings = new NvGPUCoolerSettings
                {
                    Version = NVAPI.GPU_COOLER_SETTINGS_VER,
                    Cooler = new NvCooler[NVAPI.MAX_COOLER_PER_GPU]
                };
                var status =
                    NVAPI.NvAPI_GPU_GetCoolerSettings(Handle, 0, ref settings);

                r.AppendLine("Cooler Settings");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < settings.Count; i++)
                    {
                        r.AppendFormat(" Cooler[{0}].Type: {1}{2}", i,
                            settings.Cooler[i].Type, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Controller: {1}{2}", i,
                            settings.Cooler[i].Controller, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultMin: {1}{2}", i,
                            settings.Cooler[i].DefaultMin, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultMax: {1}{2}", i,
                            settings.Cooler[i].DefaultMax, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentMin: {1}{2}", i,
                            settings.Cooler[i].CurrentMin, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentMax: {1}{2}", i,
                            settings.Cooler[i].CurrentMax, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentLevel: {1}{2}", i,
                            settings.Cooler[i].CurrentLevel, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].DefaultPolicy: {1}{2}", i,
                            settings.Cooler[i].DefaultPolicy, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].CurrentPolicy: {1}{2}", i,
                            settings.Cooler[i].CurrentPolicy, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Target: {1}{2}", i,
                            settings.Cooler[i].Target, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].ControlType: {1}{2}", i,
                            settings.Cooler[i].ControlType, Environment.NewLine);
                        r.AppendFormat(" Cooler[{0}].Active: {1}{2}", i,
                            settings.Cooler[i].Active, Environment.NewLine);
                    }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            if (NVAPI.NvAPI_GPU_GetMemoryInfo != null && DisplayHandle.HasValue)
            {
                var memoryInfo = new NvMemoryInfo
                {
                    Version = NVAPI.GPU_MEMORY_INFO_VER,
                    Values = new uint[NVAPI.MAX_MEMORY_VALUES_PER_GPU]
                };
                var status = NVAPI.NvAPI_GPU_GetMemoryInfo(DisplayHandle.Value,
                    ref memoryInfo);

                r.AppendLine("Memory Info");
                r.AppendLine();
                if (status == NvStatus.OK)
                {
                    for (var i = 0; i < memoryInfo.Values.Length; i++)
                        r.AppendFormat(" Value[{0}]: {1}{2}", i,
                            memoryInfo.Values[i], Environment.NewLine);
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }

                r.AppendLine();
            }

            return r.ToString();
        }

        private void SoftwareControlValueChanged(IControl control)
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

        private void ControlModeChanged(IControl control)
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

        public override void Close()
        {
            if (FanControl != null)
            {
                FanControl.ControlModeChanged -= ControlModeChanged;
                FanControl.SoftwareControlValueChanged -=
                    SoftwareControlValueChanged;

                if (FanControl.ControlMode != ControlMode.Undefined)
                    SetDefaultFanSpeed();
            }

            base.Close();
        }
    }
}