/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.CPU;

internal sealed class Amd10Cpu : Amdcpu
{
    private readonly Sensor _coreTemperature;
    private readonly Sensor[] _coreClocks;
    private readonly Sensor _busClock;

    private const uint PerfCtl0 = 0xC0010000;
    private const uint PerfCtr0 = 0xC0010004;
    private const uint Hwcr = 0xC0010015;
    private const uint PState0 = 0xC0010064;
    private const uint CofvidStatus = 0xC0010071;

    private const byte MiscellaneousControlFunction = 3;
    private const ushort Family10HMiscellaneousControlDeviceId = 0x1203;
    private const ushort Family11HMiscellaneousControlDeviceId = 0x1303;
    private const ushort Family12HMiscellaneousControlDeviceId = 0x1703;
    private const ushort Family14HMiscellaneousControlDeviceId = 0x1703;
    private const ushort Family15HModel00MiscControlDeviceId = 0x1603;
    private const ushort Family15HModel10MiscControlDeviceId = 0x1403;
    private const ushort Family15HModel30MiscControlDeviceId = 0x141D;
    private const ushort Family15HModel60MiscControlDeviceId = 0x1573;
    private const ushort Family15HModel70MiscControlDeviceId = 0x15B3;
    private const ushort Family16HModel00MiscControlDeviceId = 0x1533;
    private const ushort Family16HModel30MiscControlDeviceId = 0x1583;

    private const uint ReportedTemperatureControlRegister = 0xA4;
    private const uint ClockPowerTimingControl0Register = 0xD4;
    private const uint SmuReportedTempControlRegister = 0xD8200CA4;

    private readonly bool _hasSmuTemperatureRegister = false;

    private readonly uint _miscellaneousControlAddress;
    private readonly ushort _miscellaneousControlDeviceId;

    private readonly FileStream _temperatureStream;

    private readonly double _timeStampCounterMultiplier;
    private readonly bool _corePerformanceBoostSupport;

    public Amd10Cpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
        : base(processorIndex, cpuid, settings)
    {
        // AMD family 1Xh processors support only one temperature sensor
        _coreTemperature = new Sensor(
            "Core" + (CoreCount > 1 ? " #1 - #" + CoreCount : ""), 0,
            SensorType.Temperature, this, new[]
            {
                new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
            }, settings);

        switch (Family)
        {
            case 0x10:
                _miscellaneousControlDeviceId =
                    Family10HMiscellaneousControlDeviceId;
                break;
            case 0x11:
                _miscellaneousControlDeviceId =
                    Family11HMiscellaneousControlDeviceId;
                break;
            case 0x12:
                _miscellaneousControlDeviceId =
                    Family12HMiscellaneousControlDeviceId;
                break;
            case 0x14:
                _miscellaneousControlDeviceId =
                    Family14HMiscellaneousControlDeviceId;
                break;
            case 0x15:
                switch (Model & 0xF0)
                {
                    case 0x00:
                        _miscellaneousControlDeviceId =
                            Family15HModel00MiscControlDeviceId;
                        break;
                    case 0x10:
                        _miscellaneousControlDeviceId =
                            Family15HModel10MiscControlDeviceId;
                        break;
                    case 0x30:
                        _miscellaneousControlDeviceId =
                            Family15HModel30MiscControlDeviceId;
                        break;
                    case 0x60:
                        _miscellaneousControlDeviceId =
                            Family15HModel60MiscControlDeviceId;
                        _hasSmuTemperatureRegister = true;
                        break;
                    case 0x70:
                        _miscellaneousControlDeviceId =
                            Family15HModel70MiscControlDeviceId;
                        _hasSmuTemperatureRegister = true;
                        break;
                    default:
                        _miscellaneousControlDeviceId = 0;
                        break;
                }

                break;
            case 0x16:
                switch (Model & 0xF0)
                {
                    case 0x00:
                        _miscellaneousControlDeviceId =
                            Family16HModel00MiscControlDeviceId;
                        break;
                    case 0x30:
                        _miscellaneousControlDeviceId =
                            Family16HModel30MiscControlDeviceId;
                        break;
                    default:
                        _miscellaneousControlDeviceId = 0;
                        break;
                }

                break;
            default:
                _miscellaneousControlDeviceId = 0;
                break;
        }

        // get the pci address for the Miscellaneous Control registers 
        _miscellaneousControlAddress = GetPciAddress(
            MiscellaneousControlFunction, _miscellaneousControlDeviceId);

        _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
        _coreClocks = new Sensor[CoreCount];
        for (var i = 0; i < _coreClocks.Length; i++)
        {
            _coreClocks[i] = new Sensor(CoreString(i), i + 1, SensorType.Clock,
                this, settings);
            if (HasTimeStampCounter)
                ActivateSensor(_coreClocks[i]);
        }

        _corePerformanceBoostSupport = (cpuid[0][0].ExtData[7, 3] & (1 << 9)) > 0;

        // set affinity to the first thread for all frequency estimations     
        var previousAffinity = ThreadAffinity.Set(cpuid[0][0].Affinity);

        // disable core performance boost  
        uint hwcrEax, hwcrEdx;
        Ring0.Rdmsr(Hwcr, out hwcrEax, out hwcrEdx);
        if (_corePerformanceBoostSupport)
            Ring0.Wrmsr(Hwcr, hwcrEax | (1 << 25), hwcrEdx);

        uint ctlEax, ctlEdx;
        Ring0.Rdmsr(PerfCtl0, out ctlEax, out ctlEdx);
        uint ctrEax, ctrEdx;
        Ring0.Rdmsr(PerfCtr0, out ctrEax, out ctrEdx);

        _timeStampCounterMultiplier = EstimateTimeStampCounterMultiplier();

        // restore the performance counter registers
        Ring0.Wrmsr(PerfCtl0, ctlEax, ctlEdx);
        Ring0.Wrmsr(PerfCtr0, ctrEax, ctrEdx);

        // restore core performance boost
        if (_corePerformanceBoostSupport)
            Ring0.Wrmsr(Hwcr, hwcrEax, hwcrEdx);

        // restore the thread affinity.
        ThreadAffinity.Set(previousAffinity);

        // the file reader for lm-sensors support on Linux
        _temperatureStream = null;
        if (OperatingSystem.IsUnix)
        {
            var devicePaths = Directory.GetDirectories("/sys/class/hwmon/");
            foreach (var path in devicePaths)
            {
                string name = null;
                try
                {
                    using (var reader = new StreamReader(path + "/device/name"))
                    {
                        name = reader.ReadLine();
                    }
                }
                catch (IOException)
                {
                }

                switch (name)
                {
                    case "k10temp":
                        _temperatureStream = new FileStream(path + "/device/temp1_input",
                            FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        break;
                }
            }
        }

        Update();
    }

    private double EstimateTimeStampCounterMultiplier()
    {
        // preload the function
        EstimateTimeStampCounterMultiplier(0);
        EstimateTimeStampCounterMultiplier(0);

        // estimate the multiplier
        var estimate = new List<double>(3);
        for (var i = 0; i < 3; i++)
            estimate.Add(EstimateTimeStampCounterMultiplier(0.025));
        estimate.Sort();
        return estimate[1];
    }

    private double EstimateTimeStampCounterMultiplier(double timeWindow)
    {
        uint eax, edx;

        // select event "076h CPU Clocks not Halted" and enable the counter
        Ring0.Wrmsr(PerfCtl0,
            (1 << 22) | // enable performance counter
            (1 << 17) | // count events in user mode
            (1 << 16) | // count events in operating-system mode
            0x76, 0x00000000);

        // set the counter to 0
        Ring0.Wrmsr(PerfCtr0, 0, 0);

        var ticks = (long)(timeWindow * Stopwatch.Frequency);
        uint lsbBegin, msbBegin, lsbEnd, msbEnd;

        var timeBegin = Stopwatch.GetTimestamp() +
                        (long)Math.Ceiling(0.001 * ticks);
        var timeEnd = timeBegin + ticks;
        while (Stopwatch.GetTimestamp() < timeBegin)
        {
        }

        Ring0.Rdmsr(PerfCtr0, out lsbBegin, out msbBegin);

        while (Stopwatch.GetTimestamp() < timeEnd)
        {
        }

        Ring0.Rdmsr(PerfCtr0, out lsbEnd, out msbEnd);
        Ring0.Rdmsr(CofvidStatus, out eax, out edx);
        var coreMultiplier = GetCoreMultiplier(eax);

        var countBegin = ((ulong)msbBegin << 32) | lsbBegin;
        var countEnd = ((ulong)msbEnd << 32) | lsbEnd;

        var coreFrequency = 1e-6 *
                            ((double)(countEnd - countBegin) * Stopwatch.Frequency) /
                            (timeEnd - timeBegin);

        var busFrequency = coreFrequency / coreMultiplier;

        return 0.25 * Math.Round(4 * TimeStampCounterFrequency / busFrequency);
    }

    protected override uint[] GetMsRs()
    {
        return new uint[]
        {
            PerfCtl0, PerfCtr0, Hwcr, PState0,
            CofvidStatus
        };
    }

    public override string GetReport()
    {
        var r = new StringBuilder();
        r.Append(base.GetReport());

        r.Append("Miscellaneous Control Address: 0x");
        r.AppendLine(_miscellaneousControlAddress.ToString("X",
            CultureInfo.InvariantCulture));
        r.Append("Time Stamp Counter Multiplier: ");
        r.AppendLine(_timeStampCounterMultiplier.ToString(
            CultureInfo.InvariantCulture));
        if (Family == 0x14)
        {
            uint value = 0;
            Ring0.ReadPciConfig(_miscellaneousControlAddress,
                ClockPowerTimingControl0Register, out value);
            r.Append("PCI Register D18F3xD4: ");
            r.AppendLine(value.ToString("X8", CultureInfo.InvariantCulture));
        }

        r.AppendLine();

        return r.ToString();
    }

    private double GetCoreMultiplier(uint cofvidEax)
    {
        switch (Family)
        {
            case 0x10:
            case 0x11:
            case 0x15:
            case 0x16:
            {
                // 8:6 CpuDid: current core divisor ID
                // 5:0 CpuFid: current core frequency ID
                var cpuDid = (cofvidEax >> 6) & 7;
                var cpuFid = cofvidEax & 0x1F;
                return 0.5 * (cpuFid + 0x10) / (1 << (int)cpuDid);
            }
            case 0x12:
            {
                // 8:4 CpuFid: current CPU core frequency ID
                // 3:0 CpuDid: current CPU core divisor ID
                var cpuFid = (cofvidEax >> 4) & 0x1F;
                var cpuDid = cofvidEax & 0xF;
                double divisor;
                switch (cpuDid)
                {
                    case 0:
                        divisor = 1;
                        break;
                    case 1:
                        divisor = 1.5;
                        break;
                    case 2:
                        divisor = 2;
                        break;
                    case 3:
                        divisor = 3;
                        break;
                    case 4:
                        divisor = 4;
                        break;
                    case 5:
                        divisor = 6;
                        break;
                    case 6:
                        divisor = 8;
                        break;
                    case 7:
                        divisor = 12;
                        break;
                    case 8:
                        divisor = 16;
                        break;
                    default:
                        divisor = 1;
                        break;
                }

                return (cpuFid + 0x10) / divisor;
            }
            case 0x14:
            {
                // 8:4: current CPU core divisor ID most significant digit
                // 3:0: current CPU core divisor ID least significant digit
                var divisorIdMsd = (cofvidEax >> 4) & 0x1F;
                var divisorIdLsd = cofvidEax & 0xF;
                uint value = 0;
                Ring0.ReadPciConfig(_miscellaneousControlAddress,
                    ClockPowerTimingControl0Register, out value);
                var frequencyId = value & 0x1F;
                return (frequencyId + 0x10) /
                       (divisorIdMsd + divisorIdLsd * 0.25 + 1);
            }
            default:
                return 1;
        }
    }

    private string ReadFirstLine(Stream stream)
    {
        var sb = new StringBuilder();
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            var b = stream.ReadByte();
            while (b != -1 && b != 10)
            {
                sb.Append((char)b);
                b = stream.ReadByte();
            }
        }
        catch
        {
        }

        return sb.ToString();
    }

    private bool ReadSmuRegister(uint address, out uint value)
    {
        if (Ring0.WaitPciBusMutex(10))
        {
            if (!Ring0.WritePciConfig(0, 0xB8, address))
            {
                value = 0;

                Ring0.ReleasePciBusMutex();
                return false;
            }

            var result = Ring0.ReadPciConfig(0, 0xBC, out value);

            Ring0.ReleasePciBusMutex();
            return result;
        }
        else
        {
            value = 0;
            return false;
        }
    }

    public override void Update()
    {
        base.Update();

        if (_temperatureStream == null)
        {
            if (_miscellaneousControlAddress != Ring0.InvalidPciAddress)
            {
                uint value;
                bool valueValid;
                if (_hasSmuTemperatureRegister)
                    valueValid =
                        ReadSmuRegister(SmuReportedTempControlRegister, out value);
                else
                    valueValid = Ring0.ReadPciConfig(_miscellaneousControlAddress,
                        ReportedTemperatureControlRegister, out value);
                if (valueValid)
                {
                    if ((Family == 0x15 || Family == 0x16) && (value & 0x30000) == 0x30000)
                        _coreTemperature.Value = ((value >> 21) & 0x7FF) * 0.125f +
                            _coreTemperature.Parameters[0].Value - 49;
                    else
                        _coreTemperature.Value = ((value >> 21) & 0x7FF) * 0.125f +
                                                _coreTemperature.Parameters[0].Value;
                    ActivateSensor(_coreTemperature);
                }
                else
                {
                    DeactivateSensor(_coreTemperature);
                }
            }
        }
        else
        {
            var s = ReadFirstLine(_temperatureStream);
            try
            {
                _coreTemperature.Value = 0.001f *
                                        long.Parse(s, CultureInfo.InvariantCulture);
                ActivateSensor(_coreTemperature);
            }
            catch
            {
                DeactivateSensor(_coreTemperature);
            }
        }

        if (HasTimeStampCounter)
        {
            double newBusClock = 0;

            for (var i = 0; i < _coreClocks.Length; i++)
            {
                Thread.Sleep(1);

                uint curEax, curEdx;
                if (Ring0.RdmsrTx(CofvidStatus, out curEax, out curEdx,
                        Cpuid[i][0].Affinity))
                {
                    double multiplier;
                    multiplier = GetCoreMultiplier(curEax);

                    _coreClocks[i].Value =
                        multiplier * TimeStampCounterFrequency /
                        _timeStampCounterMultiplier;
                    newBusClock =
                        TimeStampCounterFrequency / _timeStampCounterMultiplier;
                }
                else
                {
                    _coreClocks[i].Value = TimeStampCounterFrequency;
                }
            }

            if (newBusClock > 0)
            {
                _busClock.Value = newBusClock;
                ActivateSensor(_busClock);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_temperatureStream != null) _temperatureStream.Close();
        base.Dispose(disposing);
    }
}
