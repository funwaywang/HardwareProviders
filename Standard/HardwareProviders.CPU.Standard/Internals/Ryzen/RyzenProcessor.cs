// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/. 
// Copyright (C) 2016-2017 Sebastian Grams <https://github.com/sebastian-dev> 
// Copyright (C) 2016-2017 Aqua Computer <https://github.com/aquacomputer, info@aqua-computer.de> 

using System;
using System.Collections.Generic;

namespace HardwareProviders.CPU.Internals.Ryzen
{
    internal class RyzenProcessor
    {
        private const uint MsrPwrUnit = 0xC0010299;
        private const uint MsrPkgEnergyStat = 0xC001029B;
        private const uint Family17HPciControlRegister = 0x60;
        private const uint F17HM01HThmTconCurTmp = 0x00059800;
        private const uint F17HM01HSvi = 0x0005A000;


        private readonly Sensor _coreTemperatureTctl;
        private readonly Sensor _coreTemperatureTdie;
        private readonly Sensor _coreVoltage;
        private readonly Sensor _packagePower;
        private readonly Sensor _socVoltage;
        private readonly AmdCpu17 mainCpu;
        private DateTime _lastPwrTime = new DateTime(0);
        private uint _lastPwrValue;

        public RyzenProcessor(AmdCpu17 mainCpu)
        {
            this.mainCpu = mainCpu;
            Nodes = new List<NumaNode>();

            _packagePower = new Sensor("Package Power", mainCpu._sensorPower++, SensorType.Power, mainCpu);
            _coreTemperatureTctl = new Sensor("Core (Tctl)", mainCpu._sensorTemperatures++, SensorType.Temperature, mainCpu);
            _coreTemperatureTdie = new Sensor("Core (Tdie)", mainCpu._sensorTemperatures++, SensorType.Temperature, mainCpu);
            _coreVoltage = new Sensor("Core (SVI2)", mainCpu._sensorVoltage++, SensorType.Voltage, mainCpu);
            _socVoltage = new Sensor("SoC (SVI2)", mainCpu._sensorVoltage++, SensorType.Voltage, mainCpu);

            mainCpu.ActivateSensor(_packagePower);
            mainCpu.ActivateSensor(_coreTemperatureTctl);
            mainCpu.ActivateSensor(_coreTemperatureTdie);
            mainCpu.ActivateSensor(_coreVoltage);
        }

        public List<NumaNode> Nodes { get; }

        #region UpdateSensors

        public void UpdateSensors()
        {
            var node = Nodes[0];
            var core = node?.Cores[0];
            var cpu = core?.Threads[0];
            if (cpu == null)
                return;

            var mask = Ring0.ThreadAffinitySet(1UL << cpu.Thread);

            // MSRC001_0299 
            // TU [19:16] 
            // ESU [12:8] -> Unit 15.3 micro Joule per increment 
            // PU [3:0] 
            Ring0.Rdmsr(MsrPwrUnit, out var eax, out _);
            var tu = (int) ((eax >> 16) & 0xf);
            var esu = (int) ((eax >> 12) & 0xf);
            var pu = (int) (eax & 0xf);

            // MSRC001_029B 
            // total_energy [31:0] 
            var sampleTime = DateTime.Now;
            Ring0.Rdmsr(MsrPkgEnergyStat, out eax, out _);
            var totalEnergy = eax;

            // THM_TCON_CUR_TMP 
            // CUR_TEMP [31:21] 
            Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                F17HM01HThmTconCurTmp);
            Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4, out var temperature);

            // SVI0_TFN_PLANE0 [0] 
            // SVI0_TFN_PLANE1 [1] 
            Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                F17HM01HSvi + 0x8);
            Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4, out var smusvi0Tfn);

            // SVI0_PLANE0_VDDCOR [24:16] 
            // SVI0_PLANE0_IDDCOR [7:0] 
            Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                F17HM01HSvi + 0xc);
            Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4,
                out var smusvi0TelPlane0);

            // SVI0_PLANE1_VDDCOR [24:16] 
            // SVI0_PLANE1_IDDCOR [7:0] 
            Ring0.WritePciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister,
                F17HM01HSvi + 0x10);
            Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), Family17HPciControlRegister + 4,
                out var smusvi0TelPlane1);

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
                mainCpu.ActivateSensor(_socVoltage);
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
                node = new NumaNode(mainCpu, numaId);
                Nodes.Add(node);
            }

            if (thread != null)
                node.AppendThread(thread, coreId);
        }
    }
}