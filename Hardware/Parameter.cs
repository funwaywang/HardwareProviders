/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;

namespace OpenHardwareMonitor.Hardware
{
    public struct ParameterDescription
    {
        public ParameterDescription(string name, string description,
            float defaultValue)
        {
            Name = name;
            Description = description;
            DefaultValue = defaultValue;
        }

        public string Name { get; }

        public string Description { get; }

        public float DefaultValue { get; }
    }

    internal class Parameter : IParameter
    {
        private readonly ParameterDescription description;

        public Parameter(ParameterDescription description)
        {
            this.description = description;
            Value = description.DefaultValue;

            Identifier = new Identifier("parameter", Name.Replace(" ", "").ToLowerInvariant());
        }

        public Identifier Identifier { get; }

        public string Name => description.Name;

        public string Description => description.Description;

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