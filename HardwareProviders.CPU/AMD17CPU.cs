// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/. 
// Copyright (C) 2016-2017 Sebastian Grams <https://github.com/sebastian-dev> 
// Copyright (C) 2016-2017 Aqua Computer <https://github.com/aquacomputer, info@aqua-computer.de> 

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenHardwareMonitor.Hardware;

namespace HardwareProviders.CPU
{
    internal sealed class Amd17Cpu : Amdcpu
    {
        // register index names for CPUID[] 
        private const int Eax = 0;
        private const int Ebx = 1;
        private const int Ecx = 2;
        private const int Edx = 3;

        private readonly Processor _ryzen;
        private int _sensorClock;
        private int _sensorMulti;

        private int _sensorPower;

        // counter, to create sensor index values 
        private int _sensorTemperatures;
        private int _sensorVoltage;

        public Amd17Cpu(int processorIndex, Cpuid[][] cpuid)
            : base(processorIndex, cpuid)
        {
            // add all numa nodes 
            // Register ..1E_ECX, [10:8] + 1 
            _ryzen = new Processor(this);
            var nodesPerProcessor = 1 + (int) ((cpuid[0][0].ExtData[0x1e, Ecx] >> 8) & 0x7);

            // add all numa nodes
            foreach (var cpu in cpuid)
            {
                var thread = cpu[0];

                // coreID 
                // Register ..1E_EBX, [7:0] 
                var coreId = (int) (thread.ExtData[0x1e, Ebx] & 0xff);

                // nodeID 
                // Register ..1E_ECX, [7:0] 
                var nodeId = (int) (thread.ExtData[0x1e, Ecx] & 0xff);

                _ryzen.AppendThread(null, nodeId, coreId);
            }

            // add all threads to numa nodes and specific core 
            foreach (var cpu in cpuid)
            {
                var thread = cpu[0];

                // coreID 
                // Register ..1E_EBX, [7:0] 
                var coreId = (int) (thread.ExtData[0x1e, Ebx] & 0xff);

                // nodeID 
                // Register ..1E_ECX, [7:0] 
                var nodeId = (int) (thread.ExtData[0x1e, Ecx] & 0xff);

                _ryzen.AppendThread(thread, nodeId, coreId);
            }

            Update();
        }

        protected override uint[] GetMsRs()
        {
            return new[] {PerfCtl0, PerfCtr0, Hwcr, MsrPstate0, CofvidStatus};
        }

        public override string GetReport()
        {
            var r = new StringBuilder();
            r.Append(base.GetReport());
            r.Append("Ryzen");
            return r.ToString();
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

            _ryzen.UpdateSensors();
            foreach (var node in _ryzen.Nodes)
            {
                node.UpdateSensors();

                foreach (var c in node.Cores) c.UpdateSensors();
            }
        }

        public override void Close()
        {
            base.Close();
        }

        #region Processor

        private class Processor
        {
            private readonly Sensor _coreTemperatureTctl;
            private readonly Sensor _coreTemperatureTdie;
            private readonly Sensor _coreVoltage;
            private readonly Amd17Cpu _hw;
            private DateTime _lastPwrTime = new DateTime(0);
            private uint _lastPwrValue;
            private readonly Sensor _packagePower;
            private readonly Sensor _socVoltage;

            public Processor(Hardware hw)
            {
                _hw = (Amd17Cpu) hw;
                Nodes = new List<NumaNode>();

                _packagePower = new Sensor("Package Power", _hw._sensorPower++, SensorType.Power, _hw);
                _coreTemperatureTctl = new Sensor("Core (Tctl)", _hw._sensorTemperatures++, SensorType.Temperature, _hw);
                _coreTemperatureTdie = new Sensor("Core (Tdie)", _hw._sensorTemperatures++, SensorType.Temperature, _hw);
                _coreVoltage = new Sensor("Core (SVI2)", _hw._sensorVoltage++, SensorType.Voltage, _hw);
                _socVoltage = new Sensor("SoC (SVI2)", _hw._sensorVoltage++, SensorType.Voltage, _hw);

                _hw.ActivateSensor(_packagePower);
                _hw.ActivateSensor(_coreTemperatureTctl);
                _hw.ActivateSensor(_coreTemperatureTdie);
                _hw.ActivateSensor(_coreVoltage);
            }

            public List<NumaNode> Nodes { get; }

            #region UpdateSensors

            public void UpdateSensors()
            {
                var node = Nodes[0];
                if (node == null)
                    return;
                var core = node.Cores[0];
                if (core == null)
                    return;
                var cpu = core.Threads[0];
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

                // MSRC001_029B 
                // total_energy [31:0] 
                var sampleTime = DateTime.Now;
                Ring0.Rdmsr(MsrPkgEnergyStat, out eax, out edx);
                var totalEnergy = eax;

                // THM_TCON_CUR_TMP 
                // CUR_TEMP [31:21] 
                uint temperature = 0;
                Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                    F17HM01HThmTconCurTmp);
                Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4, out temperature);

                // SVI0_TFN_PLANE0 [0] 
                // SVI0_TFN_PLANE1 [1] 
                uint smusvi0Tfn = 0;
                Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                    F17HM01HSvi + 0x8);
                Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4, out smusvi0Tfn);

                // SVI0_PLANE0_VDDCOR [24:16] 
                // SVI0_PLANE0_IDDCOR [7:0] 
                uint smusvi0TelPlane0 = 0;
                Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                    F17HM01HSvi + 0xc);
                Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4,
                    out smusvi0TelPlane0);

                // SVI0_PLANE1_VDDCOR [24:16] 
                // SVI0_PLANE1_IDDCOR [7:0] 
                uint smusvi0TelPlane1 = 0;
                Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                    F17HM01HSvi + 0x10);
                Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4,
                    out smusvi0TelPlane1);

                Ring0.ThreadAffinitySet(mask);

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

                _packagePower.Value = (float) energy;

                // current temp Bit [31:21] 
                temperature = (temperature >> 21) * 125;
                var offset = 0.0f;
                if (cpu.Name != null && (cpu.Name.Contains("1600X") || cpu.Name.Contains("1700X") ||
                                         cpu.Name.Contains("1800X")))
                    offset = -20.0f;
                else if (cpu.Name != null && (cpu.Name.Contains("1920X") || cpu.Name.Contains("1950X")))
                    offset = -27.0f;
                else if (cpu.Name != null && (cpu.Name.Contains("1910") || cpu.Name.Contains("1920")))
                    offset = -10.0f;

                _coreTemperatureTctl.Value = temperature * 0.001f;
                _coreTemperatureTdie.Value = temperature * 0.001f + offset;

                // voltage 
                var vidStep = 0.00625;
                double vcc;
                uint svi0PlaneXVddcor;
                uint svi0PlaneXIddcor;

                //Core
                if ((smusvi0Tfn & 0x01) == 0)
                {
                    svi0PlaneXVddcor = (smusvi0TelPlane0 >> 16) & 0xff;
                    svi0PlaneXIddcor = smusvi0TelPlane0 & 0xff;
                    vcc = 1.550 - vidStep * svi0PlaneXVddcor;
                    _coreVoltage.Value = (float) vcc;
                }

                // SoC 
                // not every zen cpu has this voltage 
                if ((smusvi0Tfn & 0x02) == 0)
                {
                    svi0PlaneXVddcor = (smusvi0TelPlane1 >> 16) & 0xff;
                    svi0PlaneXIddcor = smusvi0TelPlane1 & 0xff;
                    vcc = 1.550 - vidStep * svi0PlaneXVddcor;
                    _socVoltage.Value = (float) vcc;
                    _hw.ActivateSensor(_socVoltage);
                }
            }

            #endregion

            public void AppendThread(Cpuid thread, int numaId, int coreId)
            {
                NumaNode node = null;
                foreach (var n in Nodes)
                    if (n.NodeId == numaId)
                        node = n;
                if (node == null)
                {
                    node = new NumaNode(_hw, numaId);
                    Nodes.Add(node);
                }

                if (thread != null)
                    node.AppendThread(thread, coreId);
            }
        }

        #endregion

        #region NumaNode

        private class NumaNode
        {
            private readonly Amd17Cpu _hw;

            public NumaNode(Hardware hw, int id)
            {
                Cores = new List<Core>();
                NodeId = id;
                _hw = (Amd17Cpu) hw;
            }

            public int NodeId { get; }
            public List<Core> Cores { get; }

            public void AppendThread(Cpuid thread, int coreId)
            {
                Core core = null;
                foreach (var c in Cores)
                    if (c.CoreId == coreId)
                        core = c;
                if (core == null)
                {
                    core = new Core(_hw, coreId);
                    Cores.Add(core);
                }

                if (thread != null)
                    core.Threads.Add(thread);
            }

            #region UpdateSensors

            public void UpdateSensors()
            {
            }

            #endregion
        }

        #endregion

        #region Core

        private class Core
        {
            private readonly Sensor _clock;
            private readonly Amd17Cpu _hw;
            private DateTime _lastPwrTime = new DateTime(0);
            private uint _lastPwrValue;
            private readonly Sensor _multiplier;
            private readonly Sensor _power;
            private readonly Sensor _vcore;

            public Core(Hardware hw, int id)
            {
                Threads = new List<Cpuid>();
                CoreId = id;
                _hw = (Amd17Cpu) hw;
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

        #endregion

        #region amd zen registers 

        private const uint PerfCtl0 = 0xC0010000;
        private const uint PerfCtr0 = 0xC0010004;
        private const uint Hwcr = 0xC0010015;

        private const uint MsrPstateL = 0xC0010061;
        private const uint MsrPstateC = 0xC0010062;
        private const uint MsrPstateS = 0xC0010063;
        private const uint MsrPstate0 = 0xC0010064;

        private const uint MsrPwrUnit = 0xC0010299;
        private const uint MsrCoreEnergyStat = 0xC001029A;
        private const uint MsrPkgEnergyStat = 0xC001029B;
        private const uint MsrHardwarePstateStatus = 0xC0010293;
        private const uint CofvidStatus = 0xC0010071;
        private const uint Family17HPciControlRegister = 0x60;
        private const uint Family17HModel01MiscControlDeviceId = 0x1463;
        private const uint F17HM01HThmTconCurTmp = 0x00059800;
        private const uint F17HM01HSvi = 0x0005A000;

        #endregion
    }
}