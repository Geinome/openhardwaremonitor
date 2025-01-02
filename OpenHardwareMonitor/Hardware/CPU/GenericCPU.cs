/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.CPU;

internal class GenericCpu : Hardware
{
    protected readonly Cpuid[][] Cpuid;

    protected readonly uint Family;
    protected readonly uint Model;
    protected readonly uint Stepping;

    protected readonly int ProcessorIndex;
    protected readonly int CoreCount;

    private readonly bool _hasModelSpecificRegisters;

    private readonly bool _hasTimeStampCounter;
    private readonly bool _isInvariantTimeStampCounter;
    private readonly double _estimatedTimeStampCounterFrequency;
    private readonly double _estimatedTimeStampCounterFrequencyError;

    private ulong _lastTimeStampCount;
    private long _lastTime;
    private double _timeStampCounterFrequency;


    private readonly Vendor _vendor;

    private readonly CpuLoad _cpuLoad;
    private readonly Sensor _totalLoad;
    private readonly Sensor[] _coreLoads;

    protected string CoreString(int i)
    {
        if (CoreCount == 1)
            return "CPU Core";
        else
            return "CPU Core #" + (i + 1);
    }

    public GenericCpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
        : base(cpuid[0][0].Name, CreateIdentifier(cpuid[0][0].Vendor,
            processorIndex), settings)
    {
        this.Cpuid = cpuid;

        _vendor = cpuid[0][0].Vendor;

        Family = cpuid[0][0].Family;
        Model = cpuid[0][0].Model;
        Stepping = cpuid[0][0].Stepping;

        this.ProcessorIndex = processorIndex;
        CoreCount = cpuid.Length;

        // check if processor has MSRs
        if (cpuid[0][0].Data.GetLength(0) > 1
            && (cpuid[0][0].Data[1, 3] & 0x20) != 0)
            _hasModelSpecificRegisters = true;
        else
            _hasModelSpecificRegisters = false;

        // check if processor has a TSC
        if (cpuid[0][0].Data.GetLength(0) > 1
            && (cpuid[0][0].Data[1, 3] & 0x10) != 0)
            _hasTimeStampCounter = true;
        else
            _hasTimeStampCounter = false;

        // check if processor supports an invariant TSC
        if (cpuid[0][0].ExtData.GetLength(0) > 7
            && (cpuid[0][0].ExtData[7, 3] & 0x100) != 0)
            _isInvariantTimeStampCounter = true;
        else
            _isInvariantTimeStampCounter = false;

        if (CoreCount > 1)
            _totalLoad = new Sensor("CPU Total", 0, SensorType.Load, this, settings);
        else
            _totalLoad = null;
        _coreLoads = new Sensor[CoreCount];
        for (var i = 0; i < _coreLoads.Length; i++)
            _coreLoads[i] = new Sensor(CoreString(i), i + 1,
                SensorType.Load, this, settings);
        _cpuLoad = new CpuLoad(cpuid);
        if (_cpuLoad.IsAvailable)
        {
            foreach (var sensor in _coreLoads)
                ActivateSensor(sensor);
            if (_totalLoad != null)
                ActivateSensor(_totalLoad);
        }

        if (_hasTimeStampCounter)
        {
            var previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

            EstimateTimeStampCounterFrequency(
                out _estimatedTimeStampCounterFrequency,
                out _estimatedTimeStampCounterFrequencyError);

            ThreadAffinity.Set(previousAffinity);
        }
        else
        {
            _estimatedTimeStampCounterFrequency = 0;
        }

        _timeStampCounterFrequency = _estimatedTimeStampCounterFrequency;
    }

    private static Identifier CreateIdentifier(Vendor vendor,
        int processorIndex)
    {
        string s;
        switch (vendor)
        {
            case Vendor.AMD:
                s = "amdcpu";
                break;
            case Vendor.Intel:
                s = "intelcpu";
                break;
            default:
                s = "genericcpu";
                break;
        }

        return new Identifier(s,
            processorIndex.ToString(CultureInfo.InvariantCulture));
    }

    private void EstimateTimeStampCounterFrequency(out double frequency,
        out double error)
    {
        double f, e;

        // preload the function
        EstimateTimeStampCounterFrequency(0, out f, out e);
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

    private void EstimateTimeStampCounterFrequency(double timeWindow,
        out double frequency, out double error)
    {
        var ticks = (long)(timeWindow * Stopwatch.Frequency);
        ulong countBegin, countEnd;

        var timeBegin = Stopwatch.GetTimestamp() +
                        (long)Math.Ceiling(0.001 * ticks);
        var timeEnd = timeBegin + ticks;

        while (Stopwatch.GetTimestamp() < timeBegin)
        {
        }

        countBegin = Opcode.Rdtsc();
        var afterBegin = Stopwatch.GetTimestamp();

        while (Stopwatch.GetTimestamp() < timeEnd)
        {
        }

        countEnd = Opcode.Rdtsc();
        var afterEnd = Stopwatch.GetTimestamp();

        double delta = timeEnd - timeBegin;
        frequency = 1e-6 *
            ((double)(countEnd - countBegin) * Stopwatch.Frequency) / delta;

        var beginError = (afterBegin - timeBegin) / delta;
        var endError = (afterEnd - timeEnd) / delta;
        error = beginError + endError;
    }


    private static void AppendMsrData(StringBuilder r, uint msr,
        GroupAffinity affinity)
    {
        uint eax, edx;
        if (Ring0.RdmsrTx(msr, out eax, out edx, affinity))
        {
            r.Append(" ");
            r.Append(msr.ToString("X8", CultureInfo.InvariantCulture));
            r.Append("  ");
            r.Append(edx.ToString("X8", CultureInfo.InvariantCulture));
            r.Append("  ");
            r.Append(eax.ToString("X8", CultureInfo.InvariantCulture));
            r.AppendLine();
        }
    }

    protected virtual uint[] GetMsRs()
    {
        return null;
    }

    public override string GetReport()
    {
        var r = new StringBuilder();

        switch (_vendor)
        {
            case Vendor.AMD:
                r.AppendLine("AMD CPU");
                break;
            case Vendor.Intel:
                r.AppendLine("Intel CPU");
                break;
            default:
                r.AppendLine("Generic CPU");
                break;
        }

        r.AppendLine();
        r.AppendFormat("Name: {0}{1}", Name, Environment.NewLine);
        r.AppendFormat("Number of Cores: {0}{1}", CoreCount,
            Environment.NewLine);
        r.AppendFormat("Threads per Core: {0}{1}", Cpuid[0].Length,
            Environment.NewLine);
        r.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Timer Frequency: {0} MHz", Stopwatch.Frequency * 1e-6));
        r.AppendLine("Time Stamp Counter: " +
                     (_hasTimeStampCounter ? _isInvariantTimeStampCounter ? "Invariant" : "Not Invariant" : "None"));
        r.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Estimated Time Stamp Counter Frequency: {0} MHz",
            Math.Round(_estimatedTimeStampCounterFrequency * 100) * 0.01));
        r.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Estimated Time Stamp Counter Frequency Error: {0} Mhz",
            Math.Round(_estimatedTimeStampCounterFrequency *
                       _estimatedTimeStampCounterFrequencyError * 1e5) * 1e-5));
        r.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Time Stamp Counter Frequency: {0} MHz",
            Math.Round(_timeStampCounterFrequency * 100) * 0.01));
        r.AppendLine();

        var msrArray = GetMsRs();
        if (msrArray != null && msrArray.Length > 0)
            for (var i = 0; i < Cpuid.Length; i++)
            {
                r.AppendLine("MSR Core #" + (i + 1));
                r.AppendLine();
                r.AppendLine(" MSR       EDX       EAX");
                foreach (var msr in msrArray)
                    AppendMsrData(r, msr, Cpuid[i][0].Affinity);
                r.AppendLine();
            }

        return r.ToString();
    }

    public override HardwareType HardwareType => HardwareType.CPU;

    public bool HasModelSpecificRegisters => _hasModelSpecificRegisters;

    public bool HasTimeStampCounter => _hasTimeStampCounter;

    public double TimeStampCounterFrequency => _timeStampCounterFrequency;

    public override void Update()
    {
        if (_hasTimeStampCounter && _isInvariantTimeStampCounter)
        {
            // make sure always the same thread is used
            var previousAffinity = ThreadAffinity.Set(Cpuid[0][0].Affinity);

            // read time before and after getting the TSC to estimate the error
            var firstTime = Stopwatch.GetTimestamp();
            var timeStampCount = Opcode.Rdtsc();
            var time = Stopwatch.GetTimestamp();

            // restore the previous thread affinity mask
            ThreadAffinity.Set(previousAffinity);

            var delta = (double)(time - _lastTime) / Stopwatch.Frequency;
            var error = (double)(time - firstTime) / Stopwatch.Frequency;

            // only use data if they are measured accuarte enough (max 0.1ms delay)
            if (error < 0.0001)
            {
                // ignore the first reading because there are no initial values
                // ignore readings with too large or too small time window
                if (_lastTime != 0 && delta > 0.5 && delta < 2)
                    // update the TSC frequency with the new value
                    _timeStampCounterFrequency =
                        (timeStampCount - _lastTimeStampCount) / (1e6 * delta);

                _lastTimeStampCount = timeStampCount;
                _lastTime = time;
            }
        }

        if (_cpuLoad.IsAvailable)
        {
            _cpuLoad.Update();
            for (var i = 0; i < _coreLoads.Length; i++)
                _coreLoads[i].Value = _cpuLoad.GetCoreLoad(i);
            if (_totalLoad != null)
                _totalLoad.Value = _cpuLoad.GetTotalLoad();
        }
    }
}
