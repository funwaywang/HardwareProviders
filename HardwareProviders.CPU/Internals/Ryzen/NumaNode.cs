// This Source Code Form is subject to the terms of the Mozilla Public 
// License, v. 2.0. If a copy of the MPL was not distributed with this 
// file, You can obtain one at http://mozilla.org/MPL/2.0/. 
// Copyright (C) 2016-2017 Sebastian Grams <https://github.com/sebastian-dev> 
// Copyright (C) 2016-2017 Aqua Computer <https://github.com/aquacomputer, info@aqua-computer.de> 

using System.Collections.Generic;

namespace HardwareProviders.CPU.Internals.Ryzen
{
    internal class NumaNode
    {
        private readonly AmdCpu17 _hw;

        public NumaNode(AmdCpu17 hw, int id)
        {
            Cores = new List<RyzenCore>();
            NodeId = id;
            _hw = hw;
        }

        public int NodeId { get; }
        public List<RyzenCore> Cores { get; }

        public void AppendThread(Cpuid thread, int coreId)
        {
            RyzenCore core = null;
            foreach (var c in Cores)
                if (c.CoreId == coreId)
                    core = c;
            if (core == null)
            {
                core = new RyzenCore(_hw, coreId);
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
}