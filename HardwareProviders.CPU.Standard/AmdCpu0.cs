/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2010 Paul Werelds <paul@werelds.net>

*/

using System.Threading;
using HardwareProviders.CPU.Internals;

namespace HardwareProviders.CPU
{
    public class AmdCpu0 : AmdCpu
    {
        private const uint FidvidStatus = 0xC0010042;

        private const byte MiscellaneousControlFunction = 3;
        private const ushort MiscellaneousControlDeviceId = 0x1103;
        private const uint ThermtripStatusRegister = 0xE4;

        private readonly uint _miscellaneousControlAddress;

        private readonly byte _thermSenseCoreSelCpu0;
        private readonly byte _thermSenseCoreSelCpu1;

        internal AmdCpu0(int processorIndex, Cpuid[][] cpuid)
            : base(processorIndex, cpuid)
        {
            var offset = -49.0f;

            // AM2+ 65nm +21 offset
            var model = cpuid[0][0].Model;
            if (model >= 0x69 && model != 0xc1 && model != 0x6c && model != 0x7c)
                offset += 21;

            if (model < 40)
            {
                // AMD Athlon 64 Processors
                _thermSenseCoreSelCpu0 = 0x0;
                _thermSenseCoreSelCpu1 = 0x4;
            }
            else
            {
                // AMD NPT Family 0Fh Revision F, G have the core selection swapped
                _thermSenseCoreSelCpu0 = 0x4;
                _thermSenseCoreSelCpu1 = 0x0;
            }

            // check if processor supports a digital thermal sensor 
            if (cpuid[0][0].ExtData.GetLength(0) > 7 &&
                (cpuid[0][0].ExtData[7, 3] & 1) != 0)
            {
                CoreTemperatures = new Sensor[CoreCount];
                for (var i = 0; i < CoreCount; i++)
                    CoreTemperatures[i] =
                        new Sensor("Core #" + (i + 1), (SensorType) SensorType.Temperature, new[]
                            {
                                new Parameter("Offset [°C]",
                                    "Temperature offset of the thermal sensor.\n" +
                                    "Temperature = Value + Offset.", offset)
                            });
            }
            else
            {
                CoreTemperatures = new Sensor[0];
            }

            _miscellaneousControlAddress = GetPciAddress(
                MiscellaneousControlFunction, MiscellaneousControlDeviceId);

            BusClock = new Sensor("Bus Speed", SensorType.Clock);
            CoreClocks = new Sensor[CoreCount];
            for (var i = 0; i < CoreClocks.Length; i++)
            {
                CoreClocks[i] = new Sensor(CoreString(i), SensorType.Clock);

            }

            Update();
        }

        protected override uint[] GetMsRs()
        {
            return new[] {FidvidStatus};
        }

        public override void Update()
        {
            base.Update();

            if (_miscellaneousControlAddress != Ring0.InvalidPciAddress)
                for (uint i = 0; i < CoreTemperatures.Length; i++)
                    if (Ring0.WritePciConfig(
                        _miscellaneousControlAddress, ThermtripStatusRegister,
                        i > 0 ? _thermSenseCoreSelCpu1 : _thermSenseCoreSelCpu0))
                    {
                        if (Ring0.ReadPciConfig(
                            _miscellaneousControlAddress, ThermtripStatusRegister,
                            out var value))
                        {
                            CoreTemperatures[i].Value = ((value >> 16) & 0xFF) +
                                                         CoreTemperatures[i].Parameters[0].Value;
                        }
                    }

            if (!HasTimeStampCounter) return;

            double newBusClock = 0;

            for (var i = 0; i < CoreClocks.Length; i++)
            {
                Thread.Sleep(1);

                if (Ring0.RdmsrTx(FidvidStatus, out var eax, out _,
                    1UL << Cpuid[i][0].Thread))
                {
                    // CurrFID can be found in eax bits 0-5, MaxFID in 16-21
                    // 8-13 hold StartFID, we don't use that here.
                    var curMp = 0.5 * ((eax & 0x3F) + 8);
                    var maxMp = 0.5 * (((eax >> 16) & 0x3F) + 8);
                    CoreClocks[i].Value =
                        (float) (curMp * TimeStampCounterFrequency / maxMp);
                    newBusClock = (float) (TimeStampCounterFrequency / maxMp);
                }
                else
                {
                    // Fail-safe value - if the code above fails, we'll use this instead
                    CoreClocks[i].Value = (float) TimeStampCounterFrequency;
                }
            }

            if (!(newBusClock > 0)) return;
            BusClock.Value = (float) newBusClock;
        }
    }
}