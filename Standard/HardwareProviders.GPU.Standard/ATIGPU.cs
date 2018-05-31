/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Collections.Generic;
using HardwareProviders.GPU.ATI;

namespace HardwareProviders.GPU
{
    public sealed class AtiGpu : Gpu
    {
        private readonly int _adapterIndex;

        private AtiGpu(string name, int adapterIndex, int busNumber, int deviceNumber) : base(name)
        {
            _adapterIndex = adapterIndex;
            BusNumber = busNumber;
            DeviceNumber = deviceNumber;

            Temperature = new Sensor("GPU Core", 0);
            Fan = new Sensor("GPU Fan", 0);
            CoreClock = new Sensor("GPU Core", 0);
            MemoryClock = new Sensor("GPU Memory", (SensorType) 1);
            CoreVoltage = new Sensor("GPU Core", 0);
            CoreLoad = new Sensor("GPU Core", 0);
            ControlSensor = new Sensor("GPU Fan", 0);

            var afsi = new ADLFanSpeedInfo();
            if (ADL.ADL_Overdrive5_FanSpeedInfo_Get(adapterIndex, 0, ref afsi)
                != ADL.ADL_OK)
            {
                afsi.MaxPercent = 100;
                afsi.MinPercent = 0;
            }

            ControlModeChanged(FanControl);
            ControlSensor.Control = FanControl;
            Update();
        }

        public Sensor ControlSensor { get; }
        public Sensor CoreClock { get; }
        public Sensor CoreLoad { get; }
        public Sensor CoreVoltage { get; }
        public Sensor Fan { get; }
        public Control FanControl { get; }
        public Sensor MemoryClock { get; }
        public Sensor Temperature { get; }

        public int BusNumber { get; }

        public int DeviceNumber { get; }

        public new static IEnumerable<AtiGpu> Discover()
        {
            var status = ADL.ADL_Main_Control_Create(1);

            if (status != ADL.ADL_OK) yield break;

            var numberOfAdapters = 0;
            ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

            if (numberOfAdapters <= 0) yield break;

            var adapterInfo = new ADLAdapterInfo[numberOfAdapters];
            if (ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) != ADL.ADL_OK) yield break;

            for (var i = 0; i < numberOfAdapters; i++)
            {
                ADL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex, out _);
                ADL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex, out _);

                if (!string.IsNullOrEmpty(adapterInfo[i].UDID) && adapterInfo[i].VendorID == ADL.ATI_VENDOR_ID)
                {
                    yield return (new AtiGpu(
                        adapterInfo[i].AdapterName.Trim(),
                        adapterInfo[i].AdapterIndex,
                        adapterInfo[i].BusNumber,
                        adapterInfo[i].DeviceNumber));
                }
            }
        }


        private void SoftwareControlValueChanged(Control control)
        {
            if (control.ControlMode != ControlMode.Software) return;

            var adlf = new ADLFanSpeedValue
            {
                SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT,
                Flags = ADL.ADL_DL_FANCTRL_FLAG_USER_DEFINED_SPEED,
                FanSpeed = (int) control.SoftwareValue
            };
            ADL.ADL_Overdrive5_FanSpeed_Set(_adapterIndex, 0, ref adlf);
        }

        private void ControlModeChanged(Control control)
        {
            switch (control.ControlMode)
            {
                case ControlMode.Undefined:
                    return;
                case ControlMode.Default:
                    SetDefaultFanSpeed();
                    break;
                case ControlMode.Software:
                    SoftwareControlValueChanged(control);
                    break;
                default:
                    return;
            }
        }

        private void SetDefaultFanSpeed()
        {
            ADL.ADL_Overdrive5_FanSpeedToDefault_Set(_adapterIndex, 0);
        }

        public override void Update()
        {
            var adlt = new ADLTemperature();
            Temperature.Value = ADL.ADL_Overdrive5_Temperature_Get(_adapterIndex, 0, ref adlt) == ADL.ADL_OK ? (float?) (0.001f * adlt.Temperature) : null;

            var adlf = new ADLFanSpeedValue {SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM};
            Fan.Value = ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf) == ADL.ADL_OK ? (float?) adlf.FanSpeed : null;

            adlf = new ADLFanSpeedValue {SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT};
            ControlSensor.Value = ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf) == ADL.ADL_OK ? (float?) adlf.FanSpeed : null;

            var adlp = new ADLPMActivity();
            if (ADL.ADL_Overdrive5_CurrentActivity_Get(_adapterIndex, ref adlp) == ADL.ADL_OK)
            {
                CoreClock.Value = adlp.EngineClock > 0 ? (float?) (0.01f * adlp.EngineClock) : null;
                MemoryClock.Value = adlp.MemoryClock > 0 ? (float?) (0.01f * adlp.MemoryClock) : null;
                CoreVoltage.Value = adlp.Vddc > 0 ? (float?) (0.001f * adlp.Vddc) : null;
                CoreLoad.Value = Math.Min(adlp.ActivityPercent, 100);
            }
            else
            {
                CoreClock.Value = null;
                MemoryClock.Value = null;
                CoreVoltage.Value = null;
                CoreLoad.Value = null;
            }
        }
    }
}