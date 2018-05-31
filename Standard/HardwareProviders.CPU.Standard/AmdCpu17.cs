// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/. 
// Copyright (C) 2016-2017 Sebastian Grams <https://github.com/sebastian-dev> 
// Copyright (C) 2016-2017 Aqua Computer <https://github.com/aquacomputer, info@aqua-computer.de> 

using HardwareProviders.CPU.Internals;
using HardwareProviders.CPU.Internals.Ryzen;

namespace HardwareProviders.CPU
{
    public class AmdCpu17 : AmdCpu
    {
        // register index names for CPUID[] 
        private const int Eax = 0;
        private const int Ebx = 1;
        private const int Ecx = 2;
        private const int Edx = 3;

        private readonly RyzenProcessor _ryzen;
        internal int _sensorClock;
        internal int _sensorMulti;

        internal int _sensorPower;

        // counter, to create sensor index values 
        internal int _sensorTemperatures;
        internal int _sensorVoltage;


        private const uint PerfCtl0 = 0xC0010000;
        private const uint PerfCtr0 = 0xC0010004;
        private const uint Hwcr = 0xC0010015;
        private const uint MsrPstate0 = 0xC0010064;


        private const uint CofvidStatus = 0xC0010071;

        internal AmdCpu17(int processorIndex, Cpuid[][] cpuid)
            : base(processorIndex, cpuid)
        {
            // add all numa nodes 
            // Register ..1E_ECX, [10:8] + 1 
            _ryzen = new RyzenProcessor(this);
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

        protected override uint[] GetMsRs() => new[] {PerfCtl0, PerfCtr0, Hwcr, MsrPstate0, CofvidStatus};

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
    }
}