/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

namespace HardwareProviders
{
    public interface IControl
    {
        ControlMode ControlMode { get; }

        float SoftwareValue { get; }

        float MinSoftwareValue { get; }
        float MaxSoftwareValue { get; }

        void SetDefault();

        void SetSoftware(float value);
    }
}