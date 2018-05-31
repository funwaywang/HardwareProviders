/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

namespace HardwareProviders
{
    public delegate void ControlEventHandler(Control control);

    public class Control
    {
        public Control(float minSoftwareValue, float maxSoftwareValue)
        {
            MinSoftwareValue = minSoftwareValue;
            MaxSoftwareValue = maxSoftwareValue;

            SoftwareValue = 0;
            ControlMode = ControlMode.Undefined;
        }

        public ControlMode ControlMode { get; private set; }

        public float SoftwareValue { get; private set; }

        public float MinSoftwareValue { get; }

        public float MaxSoftwareValue { get; }

        public void SetSoftware(float value)
        {
            ControlMode = ControlMode.Software;
            SoftwareValue = value;
        }
    }
}