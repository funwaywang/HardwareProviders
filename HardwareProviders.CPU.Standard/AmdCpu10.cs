/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2013 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using HardwareProviders.CPU.Internals;

namespace HardwareProviders.CPU
{
    public sealed class AmdCpu10 : AmdCpu
    {
        private const uint PerfCtl0 = 0xC0010000;
        private const uint PerfCtr0 = 0xC0010004;
        private const uint Hwcr = 0xC0010015;
        private const uint PState0 = 0xC0010064;
        private const uint CofvidStatus = 0xC0010071;

        private const byte MiscellaneousControlFunction = 3;
        private const ushort Family10HMiscellaneousControlDeviceId = 0x1203;
        private const ushort Family11HMiscellaneousControlDeviceId = 0x1303;
        private const ushort Family12HMiscellaneousControlDeviceId = 0x1703;
        private const ushort Family14HMiscellaneousControlDeviceId = 0x1703;
        private const ushort Family15HModel00MiscControlDeviceId = 0x1603;
        private const ushort Family15HModel10MiscControlDeviceId = 0x1403;
        private const ushort Family15HModel30MiscControlDeviceId = 0x141D;
        private const ushort Family15HModel60MiscControlDeviceId = 0x1573;
        private const ushort Family16HModel00MiscControlDeviceId = 0x1533;
        private const ushort Family16HModel30MiscControlDeviceId = 0x1583;
        private const ushort Family17HModel00MiscControlDeviceId = 0x1577;

        private const uint ReportedTemperatureControlRegister = 0xA4;
        private const uint ClockPowerTimingControl0Register = 0xD4;

        private const uint F15HM60HReportedTempCtrlOffset = 0xD8200CA4;

        public bool CorePerformanceBoostSupport { get; }

        private readonly uint _miscellaneousControlAddress;
        private readonly ushort _miscellaneousControlDeviceId;

        private readonly FileStream _temperatureStream;

        private readonly double _timeStampCounterMultiplier;

        internal AmdCpu10(int processorIndex, Cpuid[][] cpuid)
            : base(processorIndex, cpuid)
        {

            // AMD family 1Xh processors support only one temperature sensor
            CoreTemperatures = new Sensor[1];
            CoreTemperatures[0] = new Sensor(
                "Core" + (CoreCount > 1 ? " #1 - #" + CoreCount : ""),
                SensorType.Temperature, new[]
                {
                    new Parameter("Offset [°C]", "Temperature offset.", 0)
                });

            switch (Family)
            {
                case 0x10:
                    _miscellaneousControlDeviceId =
                        Family10HMiscellaneousControlDeviceId;
                    break;
                case 0x11:
                    _miscellaneousControlDeviceId =
                        Family11HMiscellaneousControlDeviceId;
                    break;
                case 0x12:
                    _miscellaneousControlDeviceId =
                        Family12HMiscellaneousControlDeviceId;
                    break;
                case 0x14:
                    _miscellaneousControlDeviceId =
                        Family14HMiscellaneousControlDeviceId;
                    break;
                case 0x15:
                    switch (Model & 0xF0)
                    {
                        case 0x00:
                            _miscellaneousControlDeviceId =
                                Family15HModel00MiscControlDeviceId;
                            break;
                        case 0x10:
                            _miscellaneousControlDeviceId =
                                Family15HModel10MiscControlDeviceId;
                            break;
                        case 0x30:
                            _miscellaneousControlDeviceId =
                                Family15HModel30MiscControlDeviceId;
                            break;
                        case 0x60:
                            _miscellaneousControlDeviceId =
                                Family15HModel60MiscControlDeviceId;
                            break;
                        default:
                            _miscellaneousControlDeviceId = 0;
                            break;
                    }

                    break;
                case 0x16:
                    switch (Model & 0xF0)
                    {
                        case 0x00:
                            _miscellaneousControlDeviceId =
                                Family16HModel00MiscControlDeviceId;
                            break;
                        case 0x30:
                            _miscellaneousControlDeviceId =
                                Family16HModel30MiscControlDeviceId;
                            break;
                        default:
                            _miscellaneousControlDeviceId = 0;
                            break;
                    }

                    break;
                case 0x17:
                    _miscellaneousControlDeviceId =
                        Family17HModel00MiscControlDeviceId;
                    break;
                default:
                    _miscellaneousControlDeviceId = 0;
                    break;
            }

            // get the pci address for the Miscellaneous Control registers 
            _miscellaneousControlAddress = GetPciAddress(
                MiscellaneousControlFunction, _miscellaneousControlDeviceId);

            BusClock = new Sensor("Bus Speed", SensorType.Clock);
            CoreClocks = new Sensor[CoreCount];
            for (var i = 0; i < CoreClocks.Length; i++)
            {
                CoreClocks[i] = new Sensor(CoreString(i), SensorType.Clock);
            }

            CorePerformanceBoostSupport = (cpuid[0][0].ExtData[7, 3] & (1 << 9)) > 0;

            // set affinity to the first thread for all frequency estimations     
            var mask = ThreadAffinity.Set(1UL << cpuid[0][0].Thread);

            // disable core performance boost  
            Ring0.Rdmsr(Hwcr, out var hwcrEax, out var hwcrEdx);
            if (CorePerformanceBoostSupport)
                Ring0.Wrmsr(Hwcr, hwcrEax | (1 << 25), hwcrEdx);

            Ring0.Rdmsr(PerfCtl0, out var ctlEax, out var ctlEdx);
            Ring0.Rdmsr(PerfCtr0, out var ctrEax, out var ctrEdx);

            _timeStampCounterMultiplier = EstimateTimeStampCounterMultiplier();

            // restore the performance counter registers
            Ring0.Wrmsr(PerfCtl0, ctlEax, ctlEdx);
            Ring0.Wrmsr(PerfCtr0, ctrEax, ctrEdx);

            // restore core performance boost
            if (CorePerformanceBoostSupport)
                Ring0.Wrmsr(Hwcr, hwcrEax, hwcrEdx);

            // restore the thread affinity.
            ThreadAffinity.Set(mask);

            // the file reader for lm-sensors support on Linux
            _temperatureStream = null;

            Update();
        }

        private double EstimateTimeStampCounterMultiplier()
        {
            // preload the function
            EstimateTimeStampCounterMultiplier(0);
            EstimateTimeStampCounterMultiplier(0);

            // estimate the multiplier
            var estimate = new List<double>(3);
            for (var i = 0; i < 3; i++)
                estimate.Add(EstimateTimeStampCounterMultiplier(0.025));
            estimate.Sort();
            return estimate[1];
        }

        private double EstimateTimeStampCounterMultiplier(double timeWindow)
        {
            uint eax, edx;

            // select event "076h CPU Clocks not Halted" and enable the counter
            Ring0.Wrmsr(PerfCtl0,
                (1 << 22) | // enable performance counter
                (1 << 17) | // count events in user mode
                (1 << 16) | // count events in operating-system mode
                0x76, 0x00000000);

            // set the counter to 0
            Ring0.Wrmsr(PerfCtr0, 0, 0);

            var ticks = (long) (timeWindow * Stopwatch.Frequency);

            var timeBegin = Stopwatch.GetTimestamp() +
                            (long) Math.Ceiling(0.001 * ticks);
            var timeEnd = timeBegin + ticks;
            while (Stopwatch.GetTimestamp() < timeBegin)
            {
            }

            Ring0.Rdmsr(PerfCtr0, out var lsbBegin, out var msbBegin);

            while (Stopwatch.GetTimestamp() < timeEnd)
            {
            }

            Ring0.Rdmsr(PerfCtr0, out var lsbEnd, out var msbEnd);
            Ring0.Rdmsr(CofvidStatus, out eax, out edx);
            var coreMultiplier = GetCoreMultiplier(eax);

            var countBegin = ((ulong) msbBegin << 32) | lsbBegin;
            var countEnd = ((ulong) msbEnd << 32) | lsbEnd;

            var coreFrequency = 1e-6 *
                                ((double) (countEnd - countBegin) * Stopwatch.Frequency) /
                                (timeEnd - timeBegin);

            var busFrequency = coreFrequency / coreMultiplier;

            return 0.25 * Math.Round(4 * TimeStampCounterFrequency / busFrequency);
        }

        protected override uint[] GetMsRs()
        {
            return new[]
            {
                PerfCtl0, PerfCtr0, Hwcr, PState0,
                CofvidStatus
            };
        }

        private double GetCoreMultiplier(uint cofvidEax)
        {
            switch (Family)
            {
                case 0x10:
                case 0x11:
                case 0x15:
                case 0x16:
                {
                    // 8:6 CpuDid: current core divisor ID
                    // 5:0 CpuFid: current core frequency ID
                    var cpuDid = (cofvidEax >> 6) & 7;
                    var cpuFid = cofvidEax & 0x1F;
                    return 0.5 * (cpuFid + 0x10) / (1 << (int) cpuDid);
                }
                case 0x12:
                {
                    // 8:4 CpuFid: current CPU core frequency ID
                    // 3:0 CpuDid: current CPU core divisor ID
                    var cpuFid = (cofvidEax >> 4) & 0x1F;
                    var cpuDid = cofvidEax & 0xF;
                    double divisor;
                    switch (cpuDid)
                    {
                        case 0:
                            divisor = 1;
                            break;
                        case 1:
                            divisor = 1.5;
                            break;
                        case 2:
                            divisor = 2;
                            break;
                        case 3:
                            divisor = 3;
                            break;
                        case 4:
                            divisor = 4;
                            break;
                        case 5:
                            divisor = 6;
                            break;
                        case 6:
                            divisor = 8;
                            break;
                        case 7:
                            divisor = 12;
                            break;
                        case 8:
                            divisor = 16;
                            break;
                        default:
                            divisor = 1;
                            break;
                    }

                    return (cpuFid + 0x10) / divisor;
                }
                case 0x14:
                {
                    // 8:4: current CPU core divisor ID most significant digit
                    // 3:0: current CPU core divisor ID least significant digit
                    var divisorIdMsd = (cofvidEax >> 4) & 0x1F;
                    var divisorIdLsd = cofvidEax & 0xF;
                    uint value = 0;
                    Ring0.ReadPciConfig(_miscellaneousControlAddress,
                        ClockPowerTimingControl0Register, out value);
                    var frequencyId = value & 0x1F;
                    return (frequencyId + 0x10) /
                           (divisorIdMsd + divisorIdLsd * 0.25 + 1);
                }
                default:
                    return 1;
            }
        }

        private string ReadFirstLine(Stream stream)
        {
            var sb = new StringBuilder();
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var b = stream.ReadByte();
                while (b != -1 && b != 10)
                {
                    sb.Append((char) b);
                    b = stream.ReadByte();
                }
            }
            catch
            {
            }

            return sb.ToString();
        }

        public override void Update()
        {
            base.Update();

            if (_temperatureStream == null)
            {
                if (_miscellaneousControlAddress != Ring0.InvalidPciAddress)
                {
                    uint value;
                    if (_miscellaneousControlAddress == Family15HModel60MiscControlDeviceId)
                    {
                        value = F15HM60HReportedTempCtrlOffset;
                        Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), 0xB8, value);
                        Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), 0xBC, out value);
                        CoreTemperatures[0].Value = ((value >> 21) & 0x7FF) * 0.125f + CoreTemperatures[0].Parameters[0].Value;

                        return;
                    }

                    if (Ring0.ReadPciConfig(_miscellaneousControlAddress,
                        ReportedTemperatureControlRegister, out value))
                    {
                        if (Family == 0x15 && (value & 0x30000) == 0x30000)
                        {
                            if ((Model & 0xF0) == 0x00)
                                CoreTemperatures[0].Value = ((value >> 21) & 0x7FC) / 8.0f +
                                                            CoreTemperatures[0].Parameters[0].Value - 49;
                            else
                                CoreTemperatures[0].Value = ((value >> 21) & 0x7FF) / 8.0f +
                                                            CoreTemperatures[0].Parameters[0].Value - 49;
                        }
                        else if (Family == 0x16 &&
                                 ((value & 0x30000) == 0x30000 || (value & 0x80000) == 0x80000))
                        {
                            CoreTemperatures[0].Value = ((value >> 21) & 0x7FF) / 8.0f +
                                                        CoreTemperatures[0].Parameters[0].Value - 49;
                        }
                        else
                        {
                            CoreTemperatures[0].Value = ((value >> 21) & 0x7FF) / 8.0f +
                                                        CoreTemperatures[0].Parameters[0].Value;
                        }
                    }
                }
            }
            else
            {
                var s = ReadFirstLine(_temperatureStream);
                try
                {
                    CoreTemperatures[0].Value = 0.001f * long.Parse(s, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            if (HasTimeStampCounter)
            {
                double newBusClock = 0;

                for (var i = 0; i < CoreClocks.Length; i++)
                {
                    Thread.Sleep(1);

                    uint curEdx;
                    if (Ring0.RdmsrTx(CofvidStatus, out var curEax, out curEdx,
                        1UL << Cpuid[i][0].Thread))
                    {
                        var multiplier = GetCoreMultiplier(curEax);

                        CoreClocks[i].Value =
                            (float) (multiplier * TimeStampCounterFrequency /
                                     _timeStampCounterMultiplier);
                        newBusClock =
                            (float) (TimeStampCounterFrequency / _timeStampCounterMultiplier);
                    }
                    else
                    {
                        CoreClocks[i].Value = (float) TimeStampCounterFrequency;
                    }
                }

                if (newBusClock > 0)
                {
                    BusClock.Value = (float) newBusClock;
                }
            }
        }
    }
}