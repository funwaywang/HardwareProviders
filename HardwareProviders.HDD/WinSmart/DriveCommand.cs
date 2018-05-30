/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>
	
*/


namespace OpenHardwareMonitor.Hardware.HDD
{
    enum DriveCommand : uint
    {
        GetVersion = 0x00074080,
        SendDriveCommand = 0x0007c084,
        ReceiveDriveData = 0x0007c088
    }
}