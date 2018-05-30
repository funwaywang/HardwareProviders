/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System.Collections.Generic;
using System.Linq;
using HardwareProviders.CPU;
using OpenHardwareMonitor.Hardware;

namespace OpenHardwareMonitor
{
    public class CpuGroup : IGroup
    {
        private readonly IEnumerable<Cpu> _hardware;

        public CpuGroup(IEnumerable<Cpu> cpus)
        {
            _hardware = cpus;
        }

        public IHardware[] Hardware => _hardware.ToArray();

        public string GetReport()
        {
            return "";
        }

        public void Close()
        {
            foreach (var cpu in _hardware) cpu.Close();
        }
    }
}