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
    enum RegisterFeature : byte
    {
        /// <summary>
        ///     Read SMART data.
        /// </summary>
        SmartReadData = 0xD0,

        /// <summary>
        ///     Read SMART thresholds.
        /// </summary>
        SmartReadThresholds = 0xD1, /* obsolete */

        /// <summary>
        ///     Autosave SMART data.
        /// </summary>
        SmartAutosave = 0xD2,

        /// <summary>
        ///     Save SMART attributes.
        /// </summary>
        SmartSaveAttr = 0xD3,

        /// <summary>
        ///     Set SMART to offline immediately.
        /// </summary>
        SmartImmediateOffline = 0xD4,

        /// <summary>
        ///     Read SMART log.
        /// </summary>
        SmartReadLog = 0xD5,

        /// <summary>
        ///     Write SMART log.
        /// </summary>
        SmartWriteLog = 0xD6,

        /// <summary>
        ///     Write SMART thresholds.
        /// </summary>
        SmartWriteThresholds = 0xD7, /* obsolete */

        /// <summary>
        ///     Enable SMART.
        /// </summary>
        SmartEnableOperations = 0xD8,

        /// <summary>
        ///     Disable SMART.
        /// </summary>
        SmartDisableOperations = 0xD9,

        /// <summary>
        ///     Get SMART status.
        /// </summary>
        SmartStatus = 0xDA,

        /// <summary>
        ///     Set SMART to offline automatically.
        /// </summary>
        SmartAutoOffline = 0xDB /* obsolete */
    }
}