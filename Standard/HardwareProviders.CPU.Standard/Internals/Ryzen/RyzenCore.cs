// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/. 
// Copyright (C) 2016-2017 Sebastian Grams <https://github.com/sebastian-dev> 
// Copyright (C) 2016-2017 Aqua Computer <https://github.com/aquacomputer, info@aqua-computer.de> 

using System;
using System.Collections.Generic;

namespace HardwareProviders.CPU.Internals.Ryzen
{
    internal class RyzenCore
    {
        private const uint MsrPwrUnit = 0xC0010299;
        private const uint MsrCoreEnergyStat = 0xC001029A;
        private const uint MsrPkgEnergyStat = 0xC001029B;
        private const uint MsrHardwarePstateStatus = 0xC0010293;

        private readonly Sensor _clock;
        private readonly AmdCpu17 _hw;
        private readonly Sensor _multiplier;
        private readonly Sensor _power;
        private readonly Sensor _vcore;
        private DateTime _lastPwrTime = new DateTime(0);
        private uint _lastPwrValue;

        public RyzenCore(Hardware hw, int id)
        {
            Threads = new List<Cpuid>();
            CoreId = id;
            _hw = (AmdCpu17) hw;
            _clock = new Sensor("Core #" + CoreId, _hw._sensorClock++, SensorType.Clock, _hw);
            _multiplier = new Sensor("Core #" + CoreId, _hw._sensorMulti++, SensorType.Factor, _hw);
            _power = new Sensor("Core #" + CoreId + " (SMU)", _hw._sensorPower++, SensorType.Power, _hw);
            _vcore = new Sensor("Core #" + CoreId + " VID", _hw._sensorVoltage++, SensorType.Voltage, _hw);

            _hw.ActivateSensor(_clock);
            _hw.ActivateSensor(_multiplier);
            _hw.ActivateSensor(_power);
            _hw.ActivateSensor(_vcore);
        }

        public int CoreId { get; }
        public List<Cpuid> Threads { get; }

        #region UpdateSensors

        public void UpdateSensors()
        {
            // CPUID cpu = threads.FirstOrDefault(); 
            var cpu = Threads[0];
            if (cpu == null)
                return;
            uint eax, edx;
            var mask = Ring0.ThreadAffinitySet(1UL << cpu.Thread);

            // MSRC001_0299 
            // TU [19:16] 
            // ESU [12:8] -> Unit 15.3 micro Joule per increment 
            // PU [3:0] 
            Ring0.Rdmsr(MsrPwrUnit, out eax, out edx);
            var tu = (int) ((eax >> 16) & 0xf);
            var esu = (int) ((eax >> 12) & 0xf);
            var pu = (int) (eax & 0xf);

            // MSRC001_029A 
            // total_energy [31:0] 
            var sampleTime = DateTime.Now;
            Ring0.Rdmsr(MsrCoreEnergyStat, out eax, out edx);
            var totalEnergy = eax;

            // MSRC001_0293 
            // CurHwPstate [24:22] 
            // CurCpuVid [21:14] 
            // CurCpuDfsId [13:8] 
            // CurCpuFid [7:0] 
            Ring0.Rdmsr(MsrHardwarePstateStatus, out eax, out edx);
            var curHwPstate = (int) ((eax >> 22) & 0x3);
            var curCpuVid = (int) ((eax >> 14) & 0xff);
            var curCpuDfsId = (int) ((eax >> 8) & 0x3f);
            var curCpuFid = (int) (eax & 0xff);

            // MSRC001_0064 + x 
            // IddDiv [31:30] 
            // IddValue [29:22] 
            // CpuVid [21:14] 
            // CpuDfsId [13:8] 
            // CpuFid [7:0] 
            // Ring0.Rdmsr(MSR_PSTATE_0 + (uint)CurHwPstate, out eax, out edx); 
            // int IddDiv = (int)((eax >> 30) & 0x03); 
            // int IddValue = (int)((eax >> 22) & 0xff); 
            // int CpuVid = (int)((eax >> 14) & 0xff); 
            Ring0.ThreadAffinitySet(mask);

            // clock 
            // CoreCOF is (Core::X86::Msr::PStateDef[CpuFid[7:0]] / Core::X86::Msr::PStateDef[CpuDfsId]) * 200 
            _clock.Value = (float) (curCpuFid / (double) curCpuDfsId * 200.0);

            // multiplier 
            _multiplier.Value = (float) (curCpuFid / (double) curCpuDfsId * 2.0);

            // Voltage 
            var vidStep = 0.00625;
            var vcc = 1.550 - vidStep * curCpuVid;
            _vcore.Value = (float) vcc;

            // power consumption 
            // power.Value = (float) ((double)pu * 0.125); 
            // esu = 15.3 micro Joule per increment 
            if (_lastPwrTime.Ticks == 0)
            {
                _lastPwrTime = sampleTime;
                _lastPwrValue = totalEnergy;
            }

            // ticks diff 
            var time = sampleTime - _lastPwrTime;
            long pwr;
            if (_lastPwrValue <= totalEnergy)
                pwr = totalEnergy - _lastPwrValue;
            else
                pwr = 0xffffffff - _lastPwrValue + totalEnergy;

            // update for next sample 
            _lastPwrTime = sampleTime;
            _lastPwrValue = totalEnergy;

            var energy = 15.3e-6 * pwr;
            energy /= time.TotalSeconds;

            _power.Value = (float) energy;
        }

        #endregion
    }
}