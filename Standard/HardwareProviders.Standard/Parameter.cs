/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;

namespace HardwareProviders
{
    public class Parameter : IParameter
    {
        public Parameter(string name, string description, float defaultValue)
        {
            Name = name;
            Description = description;
            Value = defaultValue;
        }

        public Parameter(Parameter description)
        {
            Name = description.Name;
            Description = description.Description;
            Value = description.Value;
        }

        public string Name { get; }

        public string Description { get; }

        public float Value { get; set; }

        public void Accept(IVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException(nameof(visitor));
            visitor.VisitParameter(this);
        }

        public void Traverse(IVisitor visitor)
        {
        }
    }
}