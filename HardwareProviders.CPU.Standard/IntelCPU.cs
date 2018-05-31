/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2017 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Threading;
using HardwareProviders.CPU.Internals;

namespace HardwareProviders.CPU
{
    internal sealed class IntelCpu : Cpu
    {
        private const uint Ia32ThermStatusMsr = 0x019C;
        private const uint Ia32TemperatureTarget = 0x01A2;
        private const uint Ia32PerfStatus = 0x0198;
        private const uint MsrPlatformInfo = 0xCE;
        private const uint Ia32PackageThermStatus = 0x1B1;
        private const uint MsrRaplPowerUnit = 0x606;
        private const uint MsrPkgEneryStatus = 0x611;
        private const uint MsrDramEnergyStatus = 0x619;
        private const uint MsrPp0EneryStatus = 0x639;
        private const uint MsrPp1EneryStatus = 0x641;


        public IntelMicroarchitecture Microarchitecture { get; }

        private readonly string[] _powerSensorLabels =
            {"CPU Package", "CPU Cores", "CPU Graphics", "CPU DRAM"};

        private readonly double _timeStampCounterMultiplier;
        private readonly float _energyUnitMultiplier;
        private readonly uint[] _lastEnergyConsumed;
        private readonly DateTime[] _lastEnergyTime;

        private readonly uint[] _energyStatusMsRs =
        {
            MsrPkgEneryStatus,
            MsrPp0EneryStatus, MsrPp1EneryStatus, MsrDramEnergyStatus
        };

        public IntelCpu(int processorIndex, Cpuid[][] cpuid)
            : base(processorIndex, cpuid)
        {
            // set tjMax
            float[] tjMax;
            switch (Family)
            {
                case 0x06:
                {
                    switch (Model)
                    {
                        case 0x0F: // Intel Core 2 (65nm)
                            Microarchitecture = IntelMicroarchitecture.Core;
                            switch (Stepping)
                            {
                                case 0x06: // B2
                                    switch (CoreCount)
                                    {
                                        case 2:
                                            tjMax = Floats(80 + 10);
                                            break;
                                        case 4:
                                            tjMax = Floats(90 + 10);
                                            break;
                                        default:
                                            tjMax = Floats(85 + 10);
                                            break;
                                    }

                                    tjMax = Floats(80 + 10);
                                    break;
                                case 0x0B: // G0
                                    tjMax = Floats(90 + 10);
                                    break;
                                case 0x0D: // M0
                                    tjMax = Floats(85 + 10);
                                    break;
                                default:
                                    tjMax = Floats(85 + 10);
                                    break;
                            }

                            break;
                        case 0x17: // Intel Core 2 (45nm)
                            Microarchitecture = IntelMicroarchitecture.Core;
                            tjMax = Floats(100);
                            break;
                        case 0x1C: // Intel Atom (45nm)
                            Microarchitecture = IntelMicroarchitecture.Atom;
                            switch (Stepping)
                            {
                                case 0x02: // C0
                                    tjMax = Floats(90);
                                    break;
                                case 0x0A: // A0, B0
                                    tjMax = Floats(100);
                                    break;
                                default:
                                    tjMax = Floats(90);
                                    break;
                            }

                            break;
                        case 0x1A: // Intel Core i7 LGA1366 (45nm)
                        case 0x1E: // Intel Core i5, i7 LGA1156 (45nm)
                        case 0x1F: // Intel Core i5, i7 
                        case 0x25: // Intel Core i3, i5, i7 LGA1156 (32nm)
                        case 0x2C: // Intel Core i7 LGA1366 (32nm) 6 Core
                        case 0x2E: // Intel Xeon Processor 7500 series (45nm)
                        case 0x2F: // Intel Xeon Processor (32nm)
                            Microarchitecture = IntelMicroarchitecture.Nehalem;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x2A: // Intel Core i5, i7 2xxx LGA1155 (32nm)
                        case 0x2D: // Next Generation Intel Xeon, i7 3xxx LGA2011 (32nm)
                            Microarchitecture = IntelMicroarchitecture.SandyBridge;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x3A: // Intel Core i5, i7 3xxx LGA1155 (22nm)
                        case 0x3E: // Intel Core i7 4xxx LGA2011 (22nm)
                            Microarchitecture = IntelMicroarchitecture.IvyBridge;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x3C: // Intel Core i5, i7 4xxx LGA1150 (22nm)              
                        case 0x3F: // Intel Xeon E5-2600/1600 v3, Core i7-59xx
                        // LGA2011-v3, Haswell-E (22nm)
                        case 0x45: // Intel Core i5, i7 4xxxU (22nm)
                        case 0x46:
                            Microarchitecture = IntelMicroarchitecture.Haswell;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x3D: // Intel Core M-5xxx (14nm)
                        case 0x47: // Intel i5, i7 5xxx, Xeon E3-1200 v4 (14nm)
                        case 0x4F: // Intel Xeon E5-26xx v4
                        case 0x56: // Intel Xeon D-15xx
                            Microarchitecture = IntelMicroarchitecture.Broadwell;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x36: // Intel Atom S1xxx, D2xxx, N2xxx (32nm)
                            Microarchitecture = IntelMicroarchitecture.Atom;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x37: // Intel Atom E3xxx, Z3xxx (22nm)
                        case 0x4A:
                        case 0x4D: // Intel Atom C2xxx (22nm)
                        case 0x5A:
                        case 0x5D:
                            Microarchitecture = IntelMicroarchitecture.Silvermont;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x4E:
                        case 0x5E: // Intel Core i5, i7 6xxxx LGA1151 (14nm)
                        case 0x55: // Intel Core X i7, i9 7xxx LGA2066 (14nm)
                            Microarchitecture = IntelMicroarchitecture.Skylake;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x4C:
                            Microarchitecture = IntelMicroarchitecture.Airmont;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x8E:
                        case 0x9E: // Intel Core i5, i7 7xxxx (14nm)
                            Microarchitecture = IntelMicroarchitecture.KabyLake;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0x5C: // Intel ApolloLake
                            Microarchitecture = IntelMicroarchitecture.ApolloLake;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        case 0xAE: // Intel Core i5, i7 8xxxx (14nm++)
                            Microarchitecture = IntelMicroarchitecture.CoffeeLake;
                            tjMax = GetTjMaxFromMsr();
                            break;
                        default:
                            Microarchitecture = IntelMicroarchitecture.Unknown;
                            tjMax = Floats(100);
                            break;
                    }
                }
                    break;
                case 0x0F:
                {
                    switch (Model)
                    {
                        case 0x00: // Pentium 4 (180nm)
                        case 0x01: // Pentium 4 (130nm)
                        case 0x02: // Pentium 4 (130nm)
                        case 0x03: // Pentium 4, Celeron D (90nm)
                        case 0x04: // Pentium 4, Pentium D, Celeron D (90nm)
                        case 0x06: // Pentium 4, Pentium D, Celeron D (65nm)
                            Microarchitecture = IntelMicroarchitecture.NetBurst;
                            tjMax = Floats(100);
                            break;
                        default:
                            Microarchitecture = IntelMicroarchitecture.Unknown;
                            tjMax = Floats(100);
                            break;
                    }
                }
                    break;
                default:
                    Microarchitecture = IntelMicroarchitecture.Unknown;
                    tjMax = Floats(100);
                    break;
            }

            // set timeStampCounterMultiplier
            switch (Microarchitecture)
            {
                case IntelMicroarchitecture.NetBurst:
                case IntelMicroarchitecture.Atom:
                case IntelMicroarchitecture.Core:
                {
                    uint eax, edx;
                    if (Ring0.Rdmsr(Ia32PerfStatus, out eax, out edx))
                        _timeStampCounterMultiplier =
                            ((edx >> 8) & 0x1f) + 0.5 * ((edx >> 14) & 1);
                }
                    break;
                case IntelMicroarchitecture.Nehalem:
                case IntelMicroarchitecture.SandyBridge:
                case IntelMicroarchitecture.IvyBridge:
                case IntelMicroarchitecture.Haswell:
                case IntelMicroarchitecture.Broadwell:
                case IntelMicroarchitecture.Silvermont:
                case IntelMicroarchitecture.Skylake:
                case IntelMicroarchitecture.Airmont:
                case IntelMicroarchitecture.ApolloLake:
                case IntelMicroarchitecture.KabyLake:
                case IntelMicroarchitecture.CoffeeLake:
                {
                    uint eax, edx;
                    if (Ring0.Rdmsr(MsrPlatformInfo, out eax, out edx))
                        _timeStampCounterMultiplier = (eax >> 8) & 0xff;
                }
                    break;
                default:
                    _timeStampCounterMultiplier = 0;
                    break;
            }

            // check if processor supports a digital thermal sensor at core level
            if (cpuid[0][0].Data.GetLength(0) > 6 &&
                (cpuid[0][0].Data[6, 0] & 1) != 0 &&
                Microarchitecture != IntelMicroarchitecture.Unknown)
            {
                CoreTemperatures = new Sensor[CoreCount];
                for (var i = 0; i < CoreTemperatures.Length; i++)
                {
                    CoreTemperatures[i] = new Sensor(CoreString(i),
                        (SensorType) SensorType.Temperature, new[]
                        {
                            new Parameter(
                                "TjMax [°C]", "TjMax temperature of the core sensor.\n" +
                                              "Temperature = TjMax - TSlope * Value.", tjMax[i]),
                            new Parameter("TSlope [°C]",
                                "Temperature slope of the digital thermal sensor.\n" +
                                "Temperature = TjMax - TSlope * Value.", 1)
                        });
                }
            }
            else
            {
                CoreTemperatures = new Sensor[0];
            }

            // check if processor supports a digital thermal sensor at package level
            if (cpuid[0][0].Data.GetLength(0) > 6 &&
                (cpuid[0][0].Data[6, 0] & 0x40) != 0 &&
                Microarchitecture != IntelMicroarchitecture.Unknown)
            {
                PackageTemperature = new Sensor((string) "CPU Package", (SensorType) SensorType.Temperature, new[]
                    {
                        new Parameter(
                            "TjMax [°C]", "TjMax temperature of the package sensor.\n" +
                                          "Temperature = TjMax - TSlope * Value.", tjMax[0]),
                        new Parameter("TSlope [°C]",
                            "Temperature slope of the digital thermal sensor.\n" +
                            "Temperature = TjMax - TSlope * Value.", 1)
                    });
            }

            BusClock = new Sensor("Bus Speed", SensorType.Clock);
            CoreClocks = new Sensor[CoreCount];
            for (var i = 0; i < CoreClocks.Length; i++)
            {
                CoreClocks[i] = new Sensor(CoreString(i), SensorType.Clock);
            }

            if (Microarchitecture == IntelMicroarchitecture.SandyBridge ||
                Microarchitecture == IntelMicroarchitecture.IvyBridge ||
                Microarchitecture == IntelMicroarchitecture.Haswell ||
                Microarchitecture == IntelMicroarchitecture.Broadwell ||
                Microarchitecture == IntelMicroarchitecture.Skylake ||
                Microarchitecture == IntelMicroarchitecture.Silvermont ||
                Microarchitecture == IntelMicroarchitecture.Airmont ||
                Microarchitecture == IntelMicroarchitecture.KabyLake ||
                Microarchitecture == IntelMicroarchitecture.ApolloLake)
            {
                CorePowers = new Sensor[_energyStatusMsRs.Length];
                _lastEnergyTime = new DateTime[_energyStatusMsRs.Length];
                _lastEnergyConsumed = new uint[_energyStatusMsRs.Length];

                uint eax, edx;
                if (Ring0.Rdmsr(MsrRaplPowerUnit, out eax, out edx))
                    switch (Microarchitecture)
                    {
                        case IntelMicroarchitecture.Silvermont:
                        case IntelMicroarchitecture.Airmont:
                            _energyUnitMultiplier = 1.0e-6f * (1 << (int) ((eax >> 8) & 0x1F));
                            break;
                        default:
                            _energyUnitMultiplier = 1.0f / (1 << (int) ((eax >> 8) & 0x1F));
                            break;
                    }
                if (_energyUnitMultiplier != 0)
                    for (var i = 0; i < _energyStatusMsRs.Length; i++)
                    {
                        if (!Ring0.Rdmsr(_energyStatusMsRs[i], out eax, out edx))
                            continue;

                        _lastEnergyTime[i] = DateTime.UtcNow;
                        _lastEnergyConsumed[i] = eax;
                        CorePowers[i] = new Sensor(_powerSensorLabels[i],SensorType.Power);
                    }
            }

            Update();
        }


        private float[] Floats(float f)
        {
            var result = new float[CoreCount];
            for (var i = 0; i < CoreCount; i++)
                result[i] = f;
            return result;
        }

        private float[] GetTjMaxFromMsr()
        {
            var result = new float[CoreCount];
            for (var i = 0; i < CoreCount; i++)
                if (Ring0.RdmsrTx(Ia32TemperatureTarget, out var eax,
                    out _, 1UL << Cpuid[i][0].Thread))
                    result[i] = (eax >> 16) & 0xFF;
                else
                    result[i] = 100;
            return result;
        }

        protected override uint[] GetMsRs()
        {
            return new[]
            {
                MsrPlatformInfo,
                Ia32PerfStatus,
                Ia32ThermStatusMsr,
                Ia32TemperatureTarget,
                Ia32PackageThermStatus,
                MsrRaplPowerUnit,
                MsrPkgEneryStatus,
                MsrDramEnergyStatus,
                MsrPp0EneryStatus,
                MsrPp1EneryStatus
            };
        }

        public override void Update()
        {
            base.Update();

            for (var i = 0; i < CoreTemperatures.Length; i++)
            {
                // if reading is valid
                if (Ring0.RdmsrTx(Ia32ThermStatusMsr, out var eax, out _,
                        1UL << Cpuid[i][0].Thread) && (eax & 0x80000000) != 0)
                {
                    // get the dist from tjMax from bits 22:16
                    float deltaT = (eax & 0x007F0000) >> 16;
                    var tjMax = CoreTemperatures[i].Parameters[0].Value;
                    var tSlope = CoreTemperatures[i].Parameters[1].Value;
                    CoreTemperatures[i].Value = tjMax - tSlope * deltaT;
                }
                else
                {
                    CoreTemperatures[i].Value = null;
                }
            }

            if (PackageTemperature != null)
            {
                uint eax, edx;
                // if reading is valid
                if (Ring0.RdmsrTx(Ia32PackageThermStatus, out eax, out edx,
                        1UL << Cpuid[0][0].Thread) && (eax & 0x80000000) != 0)
                {
                    // get the dist from tjMax from bits 22:16
                    float deltaT = (eax & 0x007F0000) >> 16;
                    var tjMax = PackageTemperature.Parameters[0].Value;
                    var tSlope = PackageTemperature.Parameters[1].Value;
                    PackageTemperature.Value = tjMax - tSlope * deltaT;
                }
                else
                {
                    PackageTemperature.Value = null;
                }
            }

            if (HasTimeStampCounter && _timeStampCounterMultiplier > 0)
            {
                double newBusClock = 0;
                uint eax, edx;
                for (var i = 0; i < CoreClocks.Length; i++)
                {
                    Thread.Sleep(1);
                    if (Ring0.RdmsrTx(Ia32PerfStatus, out eax, out edx,
                        1UL << Cpuid[i][0].Thread))
                    {
                        newBusClock =
                            TimeStampCounterFrequency / _timeStampCounterMultiplier;
                        switch (Microarchitecture)
                        {
                            case IntelMicroarchitecture.Nehalem:
                            {
                                var multiplier = eax & 0xff;
                                CoreClocks[i].Value = (float) (multiplier * newBusClock);
                            }
                                break;
                            case IntelMicroarchitecture.SandyBridge:
                            case IntelMicroarchitecture.IvyBridge:
                            case IntelMicroarchitecture.Haswell:
                            case IntelMicroarchitecture.Broadwell:
                            case IntelMicroarchitecture.Silvermont:
                            case IntelMicroarchitecture.Skylake:
                            case IntelMicroarchitecture.ApolloLake:
                            case IntelMicroarchitecture.KabyLake:
                            case IntelMicroarchitecture.CoffeeLake:
                            {
                                var multiplier = (eax >> 8) & 0xff;
                                CoreClocks[i].Value = (float) (multiplier * newBusClock);
                            }
                                break;
                            default:
                            {
                                var multiplier =
                                    ((eax >> 8) & 0x1f) + 0.5 * ((eax >> 14) & 1);
                                CoreClocks[i].Value = (float) (multiplier * newBusClock);
                            }
                                break;
                        }
                    }
                    else
                    {
                        // if IA32_PERF_STATUS is not available, assume TSC frequency
                        CoreClocks[i].Value = (float) TimeStampCounterFrequency;
                    }
                }

                if (newBusClock > 0)
                {
                    BusClock.Value = (float) newBusClock;
                }
            }

            if (CorePowers == null) return;

            for (var index = 0; index < CorePowers.Length; index++)
            {
                var sensor = CorePowers[index];
                if (sensor == null)
                    continue;

                if (!Ring0.Rdmsr(_energyStatusMsRs[index], out var eax, out _))
                    continue;

                var time = DateTime.UtcNow;
                var energyConsumed = eax;
                var deltaTime =
                    (float) (time - _lastEnergyTime[index]).TotalSeconds;
                if (deltaTime < 0.01)
                    continue;

                sensor.Value = _energyUnitMultiplier * unchecked(energyConsumed - _lastEnergyConsumed[index]) /
                               deltaTime;
                _lastEnergyTime[index] = time;
                _lastEnergyConsumed[index] = energyConsumed;
            }
        }
    }
}