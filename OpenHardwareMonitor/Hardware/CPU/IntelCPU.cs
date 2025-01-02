/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;
using System.Text;
using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.CPU;

internal sealed class IntelCpu : GenericCpu
{
    private enum Microarchitecture
    {
        Unknown,
        NetBurst,
        Core,
        Atom,
        Nehalem,
        SandyBridge,
        IvyBridge,
        Haswell,
        Broadwell,
        Silvermont,
        Skylake,
        Airmont,
        KabyLake,
        Goldmont,
        GoldmontPlus,
        CannonLake,
        IceLake,
        CometLake,
        Tremont,
        TigerLake,
        RocketLake,
        AlderLake,
        RaptorLake,
        MeteorLake
    }

    private readonly Sensor[] _coreTemperatures;
    private readonly Sensor _packageTemperature;
    private readonly Sensor[] _coreClocks;
    private readonly Sensor _busClock;
    private readonly Sensor[] _powerSensors;

    private readonly Microarchitecture _microarchitecture;
    private readonly double _timeStampCounterMultiplier;

    private const uint Ia32ThermStatusMsr = 0x019C;
    private const uint Ia32TemperatureTarget = 0x01A2;
    private const uint Ia32PerfStatus = 0x0198;
    private const uint MsrPlatformInfo = 0xCE;
    private const uint Ia32PackageThermStatus = 0x1B1;
    private const uint MsrRaplPowerUnit = 0x606;
    private const uint MsrPkgEneryStatus = 0x611;
    private const uint MsrDramEnergyStatus = 0x619;
    private const uint MsrPp0EneryStatus = 0x639;
    private const uint MsrPp1EneryStatus = 0x641;

    private readonly uint[] _energyStatusMsRs =
    {
        MsrPkgEneryStatus,
        MsrPp0EneryStatus, MsrPp1EneryStatus, MsrDramEnergyStatus
    };

    private readonly string[] _powerSensorLabels =
        { "CPU Package", "CPU Cores", "CPU Graphics", "CPU DRAM" };

    private float _energyUnitMultiplier = 0;
    private DateTime[] _lastEnergyTime;
    private uint[] _lastEnergyConsumed;


    private float[] Floats(float f)
    {
        var result = new float[CoreCount];
        for (var i = 0; i < CoreCount; i++)
            result[i] = f;
        return result;
    }

    private float[] GetTjMaxFromMsr()
    {
        uint eax, edx;
        var result = new float[CoreCount];
        for (var i = 0; i < CoreCount; i++)
            if (Ring0.RdmsrTx(Ia32TemperatureTarget, out eax,
                    out edx, Cpuid[i][0].Affinity))
                result[i] = (eax >> 16) & 0xFF;
            else
                result[i] = 100;
        return result;
    }

    public IntelCpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
        : base(processorIndex, cpuid, settings)
    {
        // set tjMax
        float[] tjMax;
        switch (Family)
        {
            case 0x06:
            {
                switch (Model)
                {
                    case 0x0F: // Intel Core 2 (65nm)
                        _microarchitecture = Microarchitecture.Core;
                        switch (Stepping)
                        {
                            case 0x06: // B2
                                switch (CoreCount)
                                {
                                    case 2:
                                        tjMax = Floats(80 + 10);
                                        break;
                                    case 4:
                                        tjMax = Floats(90 + 10);
                                        break;
                                    default:
                                        tjMax = Floats(85 + 10);
                                        break;
                                }

                                tjMax = Floats(80 + 10);
                                break;
                            case 0x0B: // G0
                                tjMax = Floats(90 + 10);
                                break;
                            case 0x0D: // M0
                                tjMax = Floats(85 + 10);
                                break;
                            default:
                                tjMax = Floats(85 + 10);
                                break;
                        }

                        break;
                    case 0x17: // Intel Core 2 (45nm)
                        _microarchitecture = Microarchitecture.Core;
                        tjMax = Floats(100);
                        break;
                    case 0x1C: // Intel Atom (45nm)
                        _microarchitecture = Microarchitecture.Atom;
                        switch (Stepping)
                        {
                            case 0x02: // C0
                                tjMax = Floats(90);
                                break;
                            case 0x0A: // A0, B0
                                tjMax = Floats(100);
                                break;
                            default:
                                tjMax = Floats(90);
                                break;
                        }

                        break;
                    case 0x1A: // Intel Core i7 LGA1366 (45nm)
                    case 0x1E: // Intel Core i5, i7 LGA1156 (45nm)
                    case 0x1F: // Intel Core i5, i7
                    case 0x25: // Intel Core i3, i5, i7 LGA1156 (32nm)
                    case 0x2C: // Intel Core i7 LGA1366 (32nm) 6 Core
                    case 0x2E: // Intel Xeon Processor 7500 series (45nm)
                    case 0x2F: // Intel Xeon Processor (32nm)
                        _microarchitecture = Microarchitecture.Nehalem;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x2A: // Intel Core i5, i7 2xxx LGA1155 (32nm)
                    case 0x2D: // Next Generation Intel Xeon, i7 3xxx LGA2011 (32nm)
                        _microarchitecture = Microarchitecture.SandyBridge;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x3A: // Intel Core i5, i7 3xxx LGA1155 (22nm)
                    case 0x3E: // Intel Core i7 4xxx LGA2011 (22nm)
                        _microarchitecture = Microarchitecture.IvyBridge;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x3C: // Intel Core i5, i7 4xxx LGA1150 (22nm)
                    case 0x3F: // Intel Xeon E5-2600/1600 v3, Core i7-59xx
                    // LGA2011-v3, Haswell-E (22nm)
                    case 0x45: // Intel Core i5, i7 4xxxU (22nm)
                    case 0x46:
                        _microarchitecture = Microarchitecture.Haswell;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x3D: // Intel Core M-5xxx (14nm)
                    case 0x47: // Intel i5, i7 5xxx, Xeon E3-1200 v4 (14nm)
                    case 0x4F: // Intel Xeon E5-26xx v4
                    case 0x56: // Intel Xeon D-15xx
                        _microarchitecture = Microarchitecture.Broadwell;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x36: // Intel Atom S1xxx, D2xxx, N2xxx (32nm)
                        _microarchitecture = Microarchitecture.Atom;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x37: // Intel Atom E3xxx, Z3xxx (22nm)
                    case 0x4A:
                    case 0x4D: // Intel Atom C2xxx (22nm)
                    case 0x5A:
                    case 0x5D:
                        _microarchitecture = Microarchitecture.Silvermont;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x4E:
                    case 0x5E: // Intel Core i5, i7 6xxxx LGA1151 (14nm)
                    case 0x55: // Intel Core i7, i9 7xxxx LGA2066 (14nm)
                        _microarchitecture = Microarchitecture.Skylake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x4C:
                        _microarchitecture = Microarchitecture.Airmont;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x8E:
                    case 0x9E: // Intel Core i5, i7 7xxxx (14nm)
                        _microarchitecture = Microarchitecture.KabyLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x5C: // Intel Atom processors (Apollo Lake) (14nm)
                    case 0x5F: // Intel Atom processors (Denverton) (14nm)
                        _microarchitecture = Microarchitecture.Goldmont;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x7A: // Intel Atom processors (14nm)
                        _microarchitecture = Microarchitecture.GoldmontPlus;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x66: // Intel Core i3 8121U (10nm)
                        _microarchitecture = Microarchitecture.CannonLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x7D: // Intel Core i3, i5, i7 10xxGx (10nm)
                    case 0x7E:
                    case 0x6A: // Intel Xeon (10nm)
                    case 0x6C:
                        _microarchitecture = Microarchitecture.IceLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0xA5:
                    case 0xA6: // Intel Core i3, i5, i7 10xxxU (14nm)
                        _microarchitecture = Microarchitecture.CometLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x86: // Intel Atom processors
                        _microarchitecture = Microarchitecture.Tremont;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x8C: // Intel processors (10nm++)
                    case 0x8D:
                        _microarchitecture = Microarchitecture.TigerLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0x97: //Intel Core 12th
                    case 0x9A: //Intel Core 12th (Mobile)
                    case 0xBE: // Some other Alder lake CPU
                        _microarchitecture = Microarchitecture.AlderLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0xA7:
                        _microarchitecture = Microarchitecture.RocketLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0xB7: // Raptor lake family
                    case 0xBA:
                        _microarchitecture = Microarchitecture.RaptorLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    case 0xAA: // Meteor lake family
                        _microarchitecture = Microarchitecture.MeteorLake;
                        tjMax = GetTjMaxFromMsr();
                        break;
                    default:
                        _microarchitecture = Microarchitecture.Unknown;
                        tjMax = Floats(100);
                        break;
                }
            }
                break;
            case 0x0F:
            {
                switch (Model)
                {
                    case 0x00: // Pentium 4 (180nm)
                    case 0x01: // Pentium 4 (130nm)
                    case 0x02: // Pentium 4 (130nm)
                    case 0x03: // Pentium 4, Celeron D (90nm)
                    case 0x04: // Pentium 4, Pentium D, Celeron D (90nm)
                    case 0x06: // Pentium 4, Pentium D, Celeron D (65nm)
                        _microarchitecture = Microarchitecture.NetBurst;
                        tjMax = Floats(100);
                        break;
                    default:
                        _microarchitecture = Microarchitecture.Unknown;
                        tjMax = Floats(100);
                        break;
                }
            }
                break;
            default:
                _microarchitecture = Microarchitecture.Unknown;
                tjMax = Floats(100);
                break;
        }

        // set timeStampCounterMultiplier
        switch (_microarchitecture)
        {
            case Microarchitecture.NetBurst:
            case Microarchitecture.Atom:
            case Microarchitecture.Core:
            {
                uint eax, edx;
                if (Ring0.Rdmsr(Ia32PerfStatus, out eax, out edx))
                    _timeStampCounterMultiplier =
                        ((edx >> 8) & 0x1f) + 0.5 * ((edx >> 14) & 1);
            }
                break;
            case Microarchitecture.Nehalem:
            case Microarchitecture.SandyBridge:
            case Microarchitecture.IvyBridge:
            case Microarchitecture.Haswell:
            case Microarchitecture.Broadwell:
            case Microarchitecture.Silvermont:
            case Microarchitecture.Skylake:
            case Microarchitecture.Airmont:
            case Microarchitecture.KabyLake:
            case Microarchitecture.Goldmont:
            case Microarchitecture.GoldmontPlus:
            case Microarchitecture.CannonLake:
            case Microarchitecture.IceLake:
            case Microarchitecture.CometLake:
            case Microarchitecture.Tremont:
            case Microarchitecture.TigerLake:
            case Microarchitecture.AlderLake:
            case Microarchitecture.RaptorLake:
            case Microarchitecture.RocketLake:
            {
                uint eax, edx;
                if (Ring0.Rdmsr(MsrPlatformInfo, out eax, out edx)) _timeStampCounterMultiplier = (eax >> 8) & 0xff;
            }
                break;
            default:
                _timeStampCounterMultiplier = 0;
                break;
        }

        // check if processor supports a digital thermal sensor at core level
        if (cpuid[0][0].Data.GetLength(0) > 6 &&
            (cpuid[0][0].Data[6, 0] & 1) != 0 &&
            _microarchitecture != Microarchitecture.Unknown)
        {
            _coreTemperatures = new Sensor[CoreCount];
            for (var i = 0; i < _coreTemperatures.Length; i++)
            {
                _coreTemperatures[i] = new Sensor(CoreString(i), i,
                    SensorType.Temperature, this, new[]
                    {
                        new ParameterDescription(
                            "TjMax [°C]", "TjMax temperature of the core sensor.\n" +
                                          "Temperature = TjMax - TSlope * Value.", tjMax[i]),
                        new ParameterDescription("TSlope [°C]",
                            "Temperature slope of the digital thermal sensor.\n" +
                            "Temperature = TjMax - TSlope * Value.", 1)
                    }, settings);
                ActivateSensor(_coreTemperatures[i]);
            }
        }
        else
        {
            _coreTemperatures = new Sensor[0];
        }

        // check if processor supports a digital thermal sensor at package level
        if (cpuid[0][0].Data.GetLength(0) > 6 &&
            (cpuid[0][0].Data[6, 0] & 0x40) != 0 &&
            _microarchitecture != Microarchitecture.Unknown)
        {
            _packageTemperature = new Sensor("CPU Package",
                _coreTemperatures.Length, SensorType.Temperature, this, new[]
                {
                    new ParameterDescription(
                        "TjMax [°C]", "TjMax temperature of the package sensor.\n" +
                                      "Temperature = TjMax - TSlope * Value.", tjMax[0]),
                    new ParameterDescription("TSlope [°C]",
                        "Temperature slope of the digital thermal sensor.\n" +
                        "Temperature = TjMax - TSlope * Value.", 1)
                }, settings);
            ActivateSensor(_packageTemperature);
        }

        _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
        _coreClocks = new Sensor[CoreCount];
        for (var i = 0; i < _coreClocks.Length; i++)
        {
            _coreClocks[i] =
                new Sensor(CoreString(i), i + 1, SensorType.Clock, this, settings);
            if (HasTimeStampCounter && _microarchitecture != Microarchitecture.Unknown)
                ActivateSensor(_coreClocks[i]);
        }

        if (_microarchitecture == Microarchitecture.SandyBridge ||
            _microarchitecture == Microarchitecture.IvyBridge ||
            _microarchitecture == Microarchitecture.Haswell ||
            _microarchitecture == Microarchitecture.Broadwell ||
            _microarchitecture == Microarchitecture.Skylake ||
            _microarchitecture == Microarchitecture.Silvermont ||
            _microarchitecture == Microarchitecture.Airmont ||
            _microarchitecture == Microarchitecture.KabyLake ||
            _microarchitecture == Microarchitecture.Goldmont ||
            _microarchitecture == Microarchitecture.GoldmontPlus ||
            _microarchitecture == Microarchitecture.CannonLake ||
            _microarchitecture == Microarchitecture.IceLake ||
            _microarchitecture == Microarchitecture.CometLake ||
            _microarchitecture == Microarchitecture.Tremont ||
            _microarchitecture == Microarchitecture.TigerLake ||
            _microarchitecture == Microarchitecture.AlderLake ||
            _microarchitecture == Microarchitecture.RaptorLake ||
            _microarchitecture == Microarchitecture.RocketLake)
        {
            _powerSensors = new Sensor[_energyStatusMsRs.Length];
            _lastEnergyTime = new DateTime[_energyStatusMsRs.Length];
            _lastEnergyConsumed = new uint[_energyStatusMsRs.Length];

            uint eax, edx;
            if (Ring0.Rdmsr(MsrRaplPowerUnit, out eax, out edx))
                switch (_microarchitecture)
                {
                    case Microarchitecture.Silvermont:
                    case Microarchitecture.Airmont:
                        _energyUnitMultiplier = 1.0e-6f * (1 << (int)((eax >> 8) & 0x1F));
                        break;
                    default:
                        _energyUnitMultiplier = 1.0f / (1 << (int)((eax >> 8) & 0x1F));
                        break;
                }

            if (_energyUnitMultiplier != 0)
                for (var i = 0; i < _energyStatusMsRs.Length; i++)
                {
                    if (!Ring0.Rdmsr(_energyStatusMsRs[i], out eax, out edx))
                        continue;

                    _lastEnergyTime[i] = DateTime.UtcNow;
                    _lastEnergyConsumed[i] = eax;
                    _powerSensors[i] = new Sensor(_powerSensorLabels[i], i,
                        SensorType.Power, this, settings);
                    ActivateSensor(_powerSensors[i]);
                }
        }

        Update();
    }

    protected override uint[] GetMsRs()
    {
        return new[]
        {
            MsrPlatformInfo,
            Ia32PerfStatus,
            Ia32ThermStatusMsr,
            Ia32TemperatureTarget,
            Ia32PackageThermStatus,
            MsrRaplPowerUnit,
            MsrPkgEneryStatus,
            MsrDramEnergyStatus,
            MsrPp0EneryStatus,
            MsrPp1EneryStatus
        };
    }

    public override string GetReport()
    {
        var r = new StringBuilder();
        r.Append(base.GetReport());

        r.Append("Microarchitecture: ");
        r.AppendLine(_microarchitecture.ToString());
        r.Append("Time Stamp Counter Multiplier: ");
        r.AppendLine(_timeStampCounterMultiplier.ToString(
            CultureInfo.InvariantCulture));
        r.AppendLine();

        return r.ToString();
    }

    public override void Update()
    {
        base.Update();

        for (var i = 0; i < _coreTemperatures.Length; i++)
        {
            uint eax, edx;
            // if reading is valid
            if (Ring0.RdmsrTx(Ia32ThermStatusMsr, out eax, out edx,
                    Cpuid[i][0].Affinity) && (eax & 0x80000000) != 0)
            {
                // get the dist from tjMax from bits 22:16
                float deltaT = (eax & 0x007F0000) >> 16;
                var tjMax = _coreTemperatures[i].Parameters[0].Value;
                var tSlope = _coreTemperatures[i].Parameters[1].Value;
                _coreTemperatures[i].Value = tjMax - tSlope * deltaT;
            }
            else
            {
                _coreTemperatures[i].Value = null;
            }
        }

        if (_packageTemperature != null)
        {
            uint eax, edx;
            // if reading is valid
            if (Ring0.RdmsrTx(Ia32PackageThermStatus, out eax, out edx,
                    Cpuid[0][0].Affinity) && (eax & 0x80000000) != 0)
            {
                // get the dist from tjMax from bits 22:16
                float deltaT = (eax & 0x007F0000) >> 16;
                var tjMax = _packageTemperature.Parameters[0].Value;
                var tSlope = _packageTemperature.Parameters[1].Value;
                _packageTemperature.Value = tjMax - tSlope * deltaT;
            }
            else
            {
                _packageTemperature.Value = null;
            }
        }

        if (HasTimeStampCounter && _timeStampCounterMultiplier > 0)
        {
            double newBusClock = 0;
            uint eax, edx;
            for (var i = 0; i < _coreClocks.Length; i++)
            {
                System.Threading.Thread.Sleep(1);
                if (Ring0.RdmsrTx(Ia32PerfStatus, out eax, out edx,
                        Cpuid[i][0].Affinity))
                {
                    newBusClock =
                        TimeStampCounterFrequency / _timeStampCounterMultiplier;
                    switch (_microarchitecture)
                    {
                        case Microarchitecture.Nehalem:
                        {
                            var multiplier = eax & 0xff;
                            _coreClocks[i].Value = multiplier * newBusClock;
                        }
                            break;
                        case Microarchitecture.SandyBridge:
                        case Microarchitecture.IvyBridge:
                        case Microarchitecture.Haswell:
                        case Microarchitecture.Broadwell:
                        case Microarchitecture.Silvermont:
                        case Microarchitecture.Skylake:
                        case Microarchitecture.KabyLake:
                        case Microarchitecture.Goldmont:
                        case Microarchitecture.GoldmontPlus:
                        case Microarchitecture.CannonLake:
                        case Microarchitecture.IceLake:
                        case Microarchitecture.CometLake:
                        case Microarchitecture.Tremont:
                        case Microarchitecture.TigerLake:
                        case Microarchitecture.AlderLake:
                        case Microarchitecture.RaptorLake:
                        case Microarchitecture.RocketLake:
                        {
                            var multiplier = (eax >> 8) & 0xff;
                            _coreClocks[i].Value = multiplier * newBusClock;
                        }
                            break;
                        default:
                        {
                            var multiplier =
                                ((eax >> 8) & 0x1f) + 0.5 * ((eax >> 14) & 1);
                            _coreClocks[i].Value = multiplier * newBusClock;
                        }
                            break;
                    }
                }
                else
                {
                    // if IA32_PERF_STATUS is not available, assume TSC frequency
                    _coreClocks[i].Value = TimeStampCounterFrequency;
                }
            }

            if (newBusClock > 0)
            {
                _busClock.Value = newBusClock;
                ActivateSensor(_busClock);
            }
        }

        if (_powerSensors != null)
            foreach (var sensor in _powerSensors)
            {
                if (sensor == null)
                    continue;

                uint eax, edx;
                if (!Ring0.Rdmsr(_energyStatusMsRs[sensor.Index], out eax, out edx))
                    continue;

                var time = DateTime.UtcNow;
                var energyConsumed = eax;
                var deltaTime =
                    (time - _lastEnergyTime[sensor.Index]).TotalSeconds;
                if (deltaTime < 0.01)
                    continue;

                sensor.Value = _energyUnitMultiplier * unchecked(
                    energyConsumed - _lastEnergyConsumed[sensor.Index]) / deltaTime;
                _lastEnergyTime[sensor.Index] = time;
                _lastEnergyConsumed[sensor.Index] = energyConsumed;
            }
    }
}
