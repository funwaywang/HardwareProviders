/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System.Collections.Generic;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware
{
    public interface ISensor : IElement
    {
        SensorType SensorType { get; }
        Identifier Identifier { get; }

        string Name { get; set; }
        int Index { get; }

        bool IsDefaultHidden { get; }

        IReadOnlyArray<IParameter> Parameters { get; }

        float? Value { get; }
        float? Min { get; }
        float? Max { get; }

        IControl Control { get; }

        void ResetMin();
        void ResetMax();
    }
}