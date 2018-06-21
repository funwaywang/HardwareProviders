/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2010-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Diagnostics;
using HardwareProviders.CPU.Internals;

namespace HardwareProviders.CPU
{
    public class Cpu : Hardware
    {
        internal readonly Cpuid[][] Cpuid;

        private readonly CpuLoad _cpuLoad;
        private readonly double _estimatedTimeStampCounterFrequency;
        private readonly double _estimatedTimeStampCounterFrequencyError;
        protected readonly uint Family;

        private readonly bool _isInvariantTimeStampCounter;
        protected readonly uint Model;

        protected readonly int ProcessorIndex;
        protected readonly uint Stepping;

        private long _lastTime;
        private ulong _lastTimeStampCount;

        internal Cpu(int processorIndex, Cpuid[][] cpuid)
            : base(cpuid[0][0].Name)
        {
            Cpuid = cpuid;

            Vendor = cpuid[0][0].Vendor;

            Family = cpuid[0][0].Family;
            Model = cpuid[0][0].Model;
            Stepping = cpuid[0][0].Stepping;

            ProcessorIndex = processorIndex;
            CoreCount = cpuid.Length;

            // check if processor has MSRs
            HasModelSpecificRegisters = cpuid[0][0].Data.GetLength(0) > 1
                                        && (cpuid[0][0].Data[1, 3] & 0x20) != 0;

            // check if processor has a TSC
            HasTimeStampCounter = cpuid[0][0].Data.GetLength(0) > 1
                                  && (cpuid[0][0].Data[1, 3] & 0x10) != 0;

            // check if processor supports an invariant TSC 
            _isInvariantTimeStampCounter = cpuid[0][0].ExtData.GetLength(0) > 7
                                           && (cpuid[0][0].ExtData[7, 3] & 0x100) != 0;

            TotalLoad = CoreCount > 1 ? new Sensor("CPU Total", SensorType.Load) : null;

            CoreLoads = new Sensor[CoreCount];
            for (var i = 0; i < CoreLoads.Length; i++)
                CoreLoads[i] = new Sensor(CoreString(i), SensorType.Load);

            _cpuLoad = new CpuLoad(cpuid);

            if (HasTimeStampCounter)
            {
                var mask = ThreadAffinity.Set(1UL << cpuid[0][0].Thread);

                EstimateTimeStampCounterFrequency(
                    out _estimatedTimeStampCounterFrequency,
                    out _estimatedTimeStampCounterFrequencyError);

                ThreadAffinity.Set(mask);
            }
            else
            {
                _estimatedTimeStampCounterFrequency = 0;
            }

            TimeStampCounterFrequency = _estimatedTimeStampCounterFrequency;
        }

        public int CoreCount { get; protected set; }

        public Sensor[] CoreLoads { get; }
        public Sensor TotalLoad { get; }
        public Sensor PackageTemperature { get; protected set; }
        public Sensor BusClock { get; protected set; }
        public Sensor[] CoreClocks { get; protected set; }
        public Sensor[] CoreTemperatures { get; protected set; }
        public Sensor[] CorePowers { get; protected set; }

        public Vendor Vendor { get; }

        public bool HasModelSpecificRegisters { get; }

        public bool HasTimeStampCounter { get; }

        public double TimeStampCounterFrequency { get; private set; }

        protected string CoreString(int i) => CoreCount == 1 ? "CPU Core" : "CPU Core #" + (i + 1);

        private static void EstimateTimeStampCounterFrequency(out double frequency, out double error)
        {
            // preload the function
            EstimateTimeStampCounterFrequency(0, out var f, out var e);
            EstimateTimeStampCounterFrequency(0, out f, out e);

            // estimate the frequency
            error = double.MaxValue;
            frequency = 0;
            for (var i = 0; i < 5; i++)
            {
                EstimateTimeStampCounterFrequency(0.025, out f, out e);
                if (e < error)
                {
                    error = e;
                    frequency = f;
                }

                if (error < 1e-4)
                    break;
            }
        }

        private static void EstimateTimeStampCounterFrequency(double timeWindow, out double frequency, out double error)
        {
            var ticks = (long) (timeWindow * Stopwatch.Frequency);

            var timeBegin = Stopwatch.GetTimestamp() +
                            (long) Math.Ceiling(0.001 * ticks);
            var timeEnd = timeBegin + ticks;

            while (Stopwatch.GetTimestamp() < timeBegin)
            {
            }

            var countBegin = Opcode.Rdtsc();
            var afterBegin = Stopwatch.GetTimestamp();

            while (Stopwatch.GetTimestamp() < timeEnd)
            {
            }

            var countEnd = Opcode.Rdtsc();
            var afterEnd = Stopwatch.GetTimestamp();

            double delta = timeEnd - timeBegin;
            frequency = 1e-6 *
                        ((double) (countEnd - countBegin) * Stopwatch.Frequency) / delta;

            var beginError = (afterBegin - timeBegin) / delta;
            var endError = (afterEnd - timeEnd) / delta;
            error = beginError + endError;
        }

        protected virtual uint[] GetMsRs() => null;

        public override void Update()
        {
            if (HasTimeStampCounter && _isInvariantTimeStampCounter)
            {
                // make sure always the same thread is used
                var mask = ThreadAffinity.Set(1UL << Cpuid[0][0].Thread);

                // read time before and after getting the TSC to estimate the error
                var firstTime = Stopwatch.GetTimestamp();
                var timeStampCount = Opcode.Rdtsc();
                var time = Stopwatch.GetTimestamp();

                // restore the thread affinity mask
                ThreadAffinity.Set(mask);

                var delta = (double) (time - _lastTime) / Stopwatch.Frequency;
                var error = (double) (time - firstTime) / Stopwatch.Frequency;

                // only use data if they are measured accuarte enough (max 0.1ms delay)
                if (error < 0.0001)
                {
                    // ignore the first reading because there are no initial values 
                    // ignore readings with too large or too small time window
                    if (_lastTime != 0 && delta > 0.5 && delta < 2)
                        TimeStampCounterFrequency =
                            (timeStampCount - _lastTimeStampCount) / (1e6 * delta);

                    _lastTimeStampCount = timeStampCount;
                    _lastTime = time;
                }
            }

            if (!_cpuLoad.IsAvailable) return;

            _cpuLoad.Update();

            for (var i = 0; i < CoreLoads.Length; i++)
                CoreLoads[i].Value = _cpuLoad.GetCoreLoad(i);

            if (TotalLoad != null)
                TotalLoad.Value = _cpuLoad.GetTotalLoad();
        }
    }
}