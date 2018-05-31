/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System.Collections.Generic;

namespace HardwareProviders
{
    public class Sensor
    {
        public Sensor(string name, SensorType sensorType) : this(name, sensorType, null)
        {
        }

        public Sensor(string name,SensorType sensorType, IReadOnlyList<Parameter> parameterDescriptions)
        {
            SensorType = sensorType;
            var parameters = new Parameter[parameterDescriptions?.Count ?? 0];
            for (var i = 0; i < parameters.Length; i++)
                parameters[i] = new Parameter(parameterDescriptions[i]);
            Parameters = parameters;

            Name =  name;
        }

        public SensorType SensorType { get; }

        public string Name { get; set; }

        public Parameter[] Parameters { get; }

        public float? Value { get; set; }

        public IControl Control { get; set; }
    }
}