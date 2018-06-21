using System;
using System.Collections;
using System.Collections.Generic;
using System.Timers;
using HardwareProviders.CPU.Internals;

namespace HardwareProviders.CPU
{
    public class CpuCollection : IDisposable, IEnumerable<Cpu>
    {
        private readonly Timer _timer;
        private readonly List<Cpu> _list;

        public CpuCollection(int updateInterval)
        {
            _timer = new Timer(updateInterval) {AutoReset = true};
            _timer.Elapsed += (sender, args) => Update();
            _timer.Enabled = true;
        }

        public CpuCollection()
        {
            _list = new List<Cpu>();

            Ring0.Open();
            Opcode.Open();

            var processorThreads = GetProcessorThreads();
            var _threads = new Cpuid[processorThreads.Length][][];

            var index = 0;
            foreach (var threads in processorThreads)
            {
                if (threads.Length == 0)
                    continue;

                var coreThreads = GroupThreadsByCore(threads);

                _threads[index] = coreThreads;

                switch (threads[0].Vendor)
                {
                    case Vendor.Intel:
                        _list.Add(new IntelCpu(index, coreThreads));
                        break;
                    case Vendor.Amd:
                        switch (threads[0].Family)
                        {
                            case 0x0F:
                                _list.Add(new AmdCpu0(index, coreThreads));
                                break;
                            case 0x10:
                            case 0x11:
                            case 0x12:
                            case 0x14:
                            case 0x15:
                            case 0x16:
                                _list.Add(new AmdCpu10(index, coreThreads));
                                break;
                            case 0x17:
                                _list.Add(new AmdCpu17(index, coreThreads));
                                break;
                            default:
                                _list.Add(new Cpu(index, coreThreads));
                                break;
                        }

                        break;
                    default:
                        _list.Add(new Cpu(index, coreThreads));
                        break;
                }

                index++;
            }
        }

        private static Cpuid[][] GetProcessorThreads()
        {
            var threads = new List<Cpuid>();
            for (var i = 0; i < 64; i++)
                try
                {
                    threads.Add(new Cpuid(i));
                }
                catch (ArgumentOutOfRangeException)
                {
                }

            var processors =
                new SortedDictionary<uint, List<Cpuid>>();
            foreach (var thread in threads)
            {
                processors.TryGetValue(thread.ProcessorId, out var list);
                if (list == null)
                {
                    list = new List<Cpuid>();
                    processors.Add(thread.ProcessorId, list);
                }

                list.Add(thread);
            }

            var processorThreads = new Cpuid[processors.Count][];
            var index = 0;
            foreach (var list in processors.Values)
            {
                processorThreads[index] = list.ToArray();
                index++;
            }

            return processorThreads;
        }

        private static Cpuid[][] GroupThreadsByCore(IEnumerable<Cpuid> threads)
        {
            var cores =
                new SortedDictionary<uint, List<Cpuid>>();
            foreach (var thread in threads)
            {
                cores.TryGetValue(thread.CoreId, out var coreList);
                if (coreList == null)
                {
                    coreList = new List<Cpuid>();
                    cores.Add(thread.CoreId, coreList);
                }

                coreList.Add(thread);
            }

            var coreThreads = new Cpuid[cores.Count][];
            var index = 0;
            foreach (var list in cores.Values)
            {
                coreThreads[index] = list.ToArray();
                index++;
            }

            return coreThreads;
        }

        public void Dispose()
        {
            Opcode.Close();
            Ring0.Close();
        }

        public IEnumerator<Cpu> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        public void Update()
        {
            foreach (var cpu in _list)
            {
                cpu.Update();
            }
        }
    }
}