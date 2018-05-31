/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2014 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Globalization;
using HardwareProviders.GPU.ATI;
using OpenHardwareMonitor.Hardware;

namespace HardwareProviders.GPU
{
    public sealed class Atigpu : Hardware
    {
        private readonly int _adapterIndex;

        public Sensor ControlSensor { get; }
        public Sensor CoreClock { get; }
        public Sensor CoreLoad { get; }
        public Sensor CoreVoltage { get; }
        public Sensor Fan { get; }
        public Control FanControl { get; }
        public Sensor MemoryClock { get; }
        public Sensor Temperature { get; }

        public Atigpu(string name, int adapterIndex, int busNumber,
            int deviceNumber)
            : base(name, new Identifier("atigpu",
                adapterIndex.ToString(CultureInfo.InvariantCulture)))
        {
            this._adapterIndex = adapterIndex;
            BusNumber = busNumber;
            DeviceNumber = deviceNumber;

            Temperature = new Sensor("GPU Core", 0, SensorType.Temperature, this);
            Fan = new Sensor("GPU Fan", 0, SensorType.Fan, this);
            CoreClock = new Sensor("GPU Core", 0, SensorType.Clock, this);
            MemoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this);
            CoreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this);
            CoreLoad = new Sensor("GPU Core", 0, SensorType.Load, this);
            ControlSensor = new Sensor("GPU Fan", 0, SensorType.Control, this);

            var afsi = new ADLFanSpeedInfo();
            if (ADL.ADL_Overdrive5_FanSpeedInfo_Get(adapterIndex, 0, ref afsi)
                != ADL.ADL_OK)
            {
                afsi.MaxPercent = 100;
                afsi.MinPercent = 0;
            }

            FanControl = new Control(ControlSensor, afsi.MinPercent,
                afsi.MaxPercent);
            FanControl.ControlModeChanged += ControlModeChanged;
            FanControl.SoftwareControlValueChanged +=
                SoftwareControlValueChanged;
            ControlModeChanged(FanControl);
            ControlSensor.Control = FanControl;
            Update();
        }

        public int BusNumber { get; }

        public int DeviceNumber { get; }


        public override HardwareType HardwareType => HardwareType.GpuAti;

        private void SoftwareControlValueChanged(IControl control)
        {
            if (control.ControlMode == ControlMode.Software)
            {
                var adlf = new ADLFanSpeedValue();
                adlf.SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT;
                adlf.Flags = ADL.ADL_DL_FANCTRL_FLAG_USER_DEFINED_SPEED;
                adlf.FanSpeed = (int) control.SoftwareValue;
                ADL.ADL_Overdrive5_FanSpeed_Set(_adapterIndex, 0, ref adlf);
            }
        }

        private void ControlModeChanged(IControl control)
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
            if (ADL.ADL_Overdrive5_Temperature_Get(_adapterIndex, 0, ref adlt)
                == ADL.ADL_OK)
            {
                Temperature.Value = 0.001f * adlt.Temperature;
                ActivateSensor(Temperature);
            }
            else
            {
                Temperature.Value = null;
            }

            var adlf = new ADLFanSpeedValue();
            adlf.SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_RPM;
            if (ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf)
                == ADL.ADL_OK)
            {
                Fan.Value = adlf.FanSpeed;
                ActivateSensor(Fan);
            }
            else
            {
                Fan.Value = null;
            }

            adlf = new ADLFanSpeedValue();
            adlf.SpeedType = ADL.ADL_DL_FANCTRL_SPEED_TYPE_PERCENT;
            if (ADL.ADL_Overdrive5_FanSpeed_Get(_adapterIndex, 0, ref adlf)
                == ADL.ADL_OK)
            {
                ControlSensor.Value = adlf.FanSpeed;
                ActivateSensor(ControlSensor);
            }
            else
            {
                ControlSensor.Value = null;
            }

            var adlp = new ADLPMActivity();
            if (ADL.ADL_Overdrive5_CurrentActivity_Get(_adapterIndex, ref adlp)
                == ADL.ADL_OK)
            {
                if (adlp.EngineClock > 0)
                {
                    CoreClock.Value = 0.01f * adlp.EngineClock;
                    ActivateSensor(CoreClock);
                }
                else
                {
                    CoreClock.Value = null;
                }

                if (adlp.MemoryClock > 0)
                {
                    MemoryClock.Value = 0.01f * adlp.MemoryClock;
                    ActivateSensor(MemoryClock);
                }
                else
                {
                    MemoryClock.Value = null;
                }

                if (adlp.Vddc > 0)
                {
                    CoreVoltage.Value = 0.001f * adlp.Vddc;
                    ActivateSensor(CoreVoltage);
                }
                else
                {
                    CoreVoltage.Value = null;
                }

                CoreLoad.Value = Math.Min(adlp.ActivityPercent, 100);
                ActivateSensor(CoreLoad);
            }
            else
            {
                CoreClock.Value = null;
                MemoryClock.Value = null;
                CoreVoltage.Value = null;
                CoreLoad.Value = null;
            }
        }

        public override void Close()
        {
            FanControl.ControlModeChanged -= ControlModeChanged;
            FanControl.SoftwareControlValueChanged -=
                SoftwareControlValueChanged;

            if (FanControl.ControlMode != ControlMode.Undefined)
                SetDefaultFanSpeed();
            base.Close();
        }
    }
}