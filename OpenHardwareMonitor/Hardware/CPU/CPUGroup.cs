/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU;

internal class CpuGroup : IGroup
{
    private readonly List<GenericCpu> _hardware = new();

    /// <summary>
    /// CPU information per thread.
    /// First index: Physical CPU number
    /// Second index: CPU core number
    /// Third index: Thread number (for hyperthreading-capable CPUs)
    /// </summary>
    private readonly Cpuid[][][] _threads;

    private static Cpuid[][] GetProcessorThreads()
    {
        var threads = new List<Cpuid>();
        for (var i = 0; i < ThreadAffinity.ProcessorGroupCount; i++)
        for (var j = 0; j < 64; j++)
            try
            {
                if (!ThreadAffinity.IsValid(GroupAffinity.Single((ushort)i, j)))
                    continue;
                var cpuid = Cpuid.Get(i, j);
                if (cpuid != null)
                    threads.Add(cpuid);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

        var processors =
            new SortedDictionary<uint, List<Cpuid>>();
        foreach (var thread in threads)
        {
            List<Cpuid> list;
            processors.TryGetValue(thread.ProcessorId, out list);
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
            List<Cpuid> coreList;
            cores.TryGetValue(thread.CoreId, out coreList);
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

    public CpuGroup(ISettings settings)
    {
        var processorThreads = GetProcessorThreads();
        this._threads = new Cpuid[processorThreads.Length][][];

        var index = 0;
        foreach (var threads in processorThreads)
        {
            if (threads.Length == 0)
                continue;

            var coreThreads = GroupThreadsByCore(threads);

            this._threads[index] = coreThreads;

            switch (threads[0].Vendor)
            {
                case Vendor.Intel:
                    _hardware.Add(new IntelCpu(index, coreThreads, settings));
                    break;
                case Vendor.AMD:
                    switch (threads[0].Family)
                    {
                        case 0x0F:
                            _hardware.Add(new Amd0Fcpu(index, coreThreads, settings));
                            break;
                        case 0x10:
                        case 0x11:
                        case 0x12:
                        case 0x14:
                        case 0x15:
                        case 0x16:
                            _hardware.Add(new Amd10Cpu(index, coreThreads, settings));
                            break;
                        case 0x17:
                        case 0x19:
                            _hardware.Add(new Amd17Cpu(index, coreThreads, settings));
                            break;
                        default:
                            _hardware.Add(new GenericCpu(index, coreThreads, settings));
                            break;
                    }

                    break;
                default:
                    _hardware.Add(new GenericCpu(index, coreThreads, settings));
                    break;
            }

            index++;
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware.ToArray();

    private static void AppendCpuidData(StringBuilder r, uint[,] data,
        uint offset)
    {
        for (var i = 0; i < data.GetLength(0); i++)
        {
            r.Append(" ");
            r.Append((i + offset).ToString("X8", CultureInfo.InvariantCulture));
            for (var j = 0; j < 4; j++)
            {
                r.Append("  ");
                r.Append(data[i, j].ToString("X8", CultureInfo.InvariantCulture));
            }

            r.Append("    ");
            for (var j = 0; j < 4; j++)
            {
                var b1 = (byte)data[i, j];
                var b2 = (byte)(data[i, j] >> 8);
                var b3 = (byte)(data[i, j] >> 16);
                var b4 = (byte)(data[i, j] >> 24);
                var bytes = new byte[] { b1, b2, b3, b4 };
                r.Append(ConvertBytesToAscii(bytes));
            }

            r.AppendLine();
        }
    }

    public static string ConvertBytesToAscii(byte[] bytes)
    {
        var ret = new StringBuilder();
        foreach (var b in bytes)
        {
            var c = (char)b;
            if (c < '!') c = '.';

            if (c >= 0x80) c = '.';

            ret.Append(c);
        }

        return ret.ToString();
    }

    public string GetReport()
    {
        if (_threads == null)
            return null;

        var r = new StringBuilder();

        r.AppendLine("CPUID");
        r.AppendLine();

        for (var i = 0; i < _threads.Length; i++)
        {
            r.AppendLine("Processor " + i);
            r.AppendLine();
            r.AppendFormat("Processor Vendor: {0}{1}", _threads[i][0][0].Vendor,
                Environment.NewLine);
            r.AppendFormat("Processor Brand: {0}{1}", _threads[i][0][0].BrandString,
                Environment.NewLine);
            r.AppendFormat("Family: 0x{0}{1}",
                _threads[i][0][0].Family.ToString("X", CultureInfo.InvariantCulture),
                Environment.NewLine);
            r.AppendFormat("Model: 0x{0}{1}",
                _threads[i][0][0].Model.ToString("X", CultureInfo.InvariantCulture),
                Environment.NewLine);
            r.AppendFormat("Stepping: 0x{0}{1}",
                _threads[i][0][0].Stepping.ToString("X", CultureInfo.InvariantCulture),
                Environment.NewLine);
            r.AppendLine();

            r.AppendLine("CPUID Return Values");
            r.AppendLine();
            for (var j = 0; j < _threads[i].Length; j++)
            for (var k = 0; k < _threads[i][j].Length; k++)
            {
                r.AppendLine(" CPU Group: " + _threads[i][j][k].Group);
                r.AppendLine(" CPU Thread: " + _threads[i][j][k].Thread);
                r.AppendLine(" APIC ID: " + _threads[i][j][k].ApicId);
                r.AppendLine(" Processor ID: " + _threads[i][j][k].ProcessorId);
                r.AppendLine(" Core ID: " + _threads[i][j][k].CoreId);
                r.AppendLine(" Thread ID: " + _threads[i][j][k].ThreadId);
                r.AppendLine();
                r.AppendLine(" Function  EAX       EBX       ECX       EDX");
                AppendCpuidData(r, _threads[i][j][k].Data, Cpuid.Cpuid0);
                AppendCpuidData(r, _threads[i][j][k].ExtData, Cpuid.CpuidExt);
                r.AppendLine();
            }
        }

        return r.ToString();
    }

    public void Close()
    {
        foreach (var cpu in _hardware) cpu.Dispose();
    }
}
