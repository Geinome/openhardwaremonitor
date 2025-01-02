/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.CPU;

internal sealed class Amd17Cpu : Amdcpu
{
    private readonly Core[] _cores;

    private readonly Sensor _coreTemperature;
    private readonly Sensor _tctlTemperature;
    private readonly Sensor _ccdMaxTemperature;
    private readonly Sensor _ccdAvgTemperature;
    private readonly Sensor[] _ccdTemperatures;
    private readonly Sensor _packagePowerSensor;
    private readonly Sensor _coresPowerSensor;
    private readonly Sensor _busClock;

    private const uint Family17HM01HThmTconTemp = 0x00059800;
    private const uint Family17HM01HThmTconTempRangeSel = 0x80000;

    private uint FAMILY_17H_M70H_CCD_TEMP(uint i)
    {
        return 0x00059954 + i * 4;
    }

    private const uint Family17HM70HCcdTempValid = 0x800;
    private uint _maxCcdCount;

    private const uint MsrRaplPwrUnit = 0xC0010299;
    private const uint MsrCoreEnergyStat = 0xC001029A;
    private const uint MsrPkgEnergyStat = 0xC001029B;
    private const uint MsrPState0 = 0xC0010064;
    private const uint MsrFamily17HPState = 0xc0010293;

    private float _energyUnitMultiplier = 0;
    private uint _lastEnergyConsumed;
    private DateTime _lastEnergyTime;

    private readonly double _timeStampCounterMultiplier;

    private struct TctlOffsetItem
    {
        public string Name { get; set; }
        public float Offset { get; set; }
    }

    private IEnumerable<TctlOffsetItem> _tctlOffsetItems = new[]
    {
        new TctlOffsetItem { Name = "AMD Ryzen 5 1600X", Offset = 20.0f },
        new TctlOffsetItem { Name = "AMD Ryzen 7 1700X", Offset = 20.0f },
        new TctlOffsetItem { Name = "AMD Ryzen 7 1800X", Offset = 20.0f },
        new TctlOffsetItem { Name = "AMD Ryzen 7 2700X", Offset = 10.0f },
        new TctlOffsetItem { Name = "AMD Ryzen Threadripper 19", Offset = 27.0f },
        new TctlOffsetItem { Name = "AMD Ryzen Threadripper 29", Offset = 27.0f }
    };

    private readonly float _tctlOffset = 0.0f;

    public Amd17Cpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
        : base(processorIndex, cpuid, settings)
    {
        var cpuName = cpuid[0][0].BrandString;
        if (!string.IsNullOrEmpty(cpuName))
            foreach (var item in _tctlOffsetItems)
                if (cpuName.StartsWith(item.Name))
                {
                    _tctlOffset = item.Offset;
                    break;
                }

        _coreTemperature = new Sensor(
            "CPU Package", 0, SensorType.Temperature, this, new[]
            {
                new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
            }, this.Settings);

        if (_tctlOffset != 0.0f)
            _tctlTemperature = new Sensor(
                "CPU Tctl", 1, true, SensorType.Temperature, this, new[]
                {
                    new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                }, this.Settings);

        _ccdMaxTemperature = new Sensor(
            "CPU CCD Max", 2, SensorType.Temperature, this, this.Settings);

        _ccdAvgTemperature = new Sensor(
            "CPU CCD Average", 3, SensorType.Temperature, this, this.Settings);

        switch (Model & 0xf0)
        {
            case 0x30:
            case 0x70:
                _maxCcdCount = 8;
                break;
            default:
                _maxCcdCount = 4;
                break;
        }

        _ccdTemperatures = new Sensor[_maxCcdCount];
        for (var i = 0; i < _ccdTemperatures.Length; i++)
            _ccdTemperatures[i] = new Sensor(
                "CPU CCD #" + (i + 1), i + 4, SensorType.Temperature, this,
                new[]
                {
                    new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                }, this.Settings);

        if (Ring0.Rdmsr(MsrRaplPwrUnit, out var eax, out _))
            _energyUnitMultiplier = 1.0f / (1 << (int)((eax >> 8) & 0x1F));

        if (_energyUnitMultiplier != 0)
            if (Ring0.Rdmsr(MsrPkgEnergyStat, out var energyConsumed, out _))
            {
                _lastEnergyTime = DateTime.UtcNow;
                _lastEnergyConsumed = energyConsumed;
                _packagePowerSensor = new Sensor(
                    "CPU Package", 0, SensorType.Power, this, settings);
                ActivateSensor(_packagePowerSensor);
            }

        _coresPowerSensor = new Sensor("CPU Cores", 1, SensorType.Power, this,
            settings);

        _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
        _timeStampCounterMultiplier = GetTimeStampCounterMultiplier();
        if (_timeStampCounterMultiplier > 0)
        {
            _busClock.Value = TimeStampCounterFrequency /
                             _timeStampCounterMultiplier;
            ActivateSensor(_busClock);
        }

        _cores = new Core[CoreCount];
        for (var i = 0; i < _cores.Length; i++) _cores[i] = new Core(i, cpuid[i], this, settings);
    }

    protected override uint[] GetMsRs()
    {
        return new uint[]
        {
            MsrPState0, MsrFamily17HPState,
            MsrRaplPwrUnit, MsrCoreEnergyStat, MsrPkgEnergyStat
        };
    }

    private IList<uint> GetSmnRegisters()
    {
        var registers = new List<uint>();
        registers.Add(Family17HM01HThmTconTemp);
        for (uint i = 0; i < _maxCcdCount; i++) registers.Add(FAMILY_17H_M70H_CCD_TEMP(i));
        return registers;
    }

    public override string GetReport()
    {
        var r = new StringBuilder();
        r.Append(base.GetReport());

        r.Append("Time Stamp Counter Multiplier: ");
        r.AppendLine(_timeStampCounterMultiplier.ToString(
            CultureInfo.InvariantCulture));
        r.AppendLine();

        if (Ring0.WaitPciBusMutex(100))
        {
            r.AppendLine("SMN Registers");
            r.AppendLine();
            r.AppendLine(" Register  Value");
            var registers = GetSmnRegisters();

            for (var i = 0; i < registers.Count; i++)
                if (ReadSmnRegister(registers[i], out var value))
                {
                    r.Append(" ");
                    r.Append(registers[i].ToString("X8", CultureInfo.InvariantCulture));
                    r.Append("  ");
                    r.Append(value.ToString("X8", CultureInfo.InvariantCulture));
                    r.AppendLine();
                }

            r.AppendLine();

            Ring0.ReleasePciBusMutex();
        }

        return r.ToString();
    }

    private double GetTimeStampCounterMultiplier()
    {
        Ring0.Rdmsr(MsrPState0, out var eax, out _);
        var cpuDfsId = (eax >> 8) & 0x3f;
        var cpuFid = eax & 0xff;
        return 2.0 * cpuFid / cpuDfsId;
    }

    private bool ReadSmnRegister(uint address, out uint value)
    {
        if (!Ring0.WritePciConfig(0, 0x60, address))
        {
            value = 0;
            return false;
        }

        return Ring0.ReadPciConfig(0, 0x64, out value);
    }

    public override void Update()
    {
        base.Update();

        if (Ring0.WaitPciBusMutex(10))
        {
            uint value;
            if (ReadSmnRegister(Family17HM01HThmTconTemp, out value))
            {
                var temperature = ((value >> 21) & 0x7FF) / 8.0f;
                if ((value & Family17HM01HThmTconTempRangeSel) != 0)
                    temperature -= 49;

                if (_tctlTemperature != null)
                {
                    _tctlTemperature.Value = temperature +
                                            _tctlTemperature.Parameters[0].Value;
                    ActivateSensor(_tctlTemperature);
                }

                temperature -= _tctlOffset;

                _coreTemperature.Value = temperature +
                                        _coreTemperature.Parameters[0].Value;
                ActivateSensor(_coreTemperature);
            }

            var maxTemperature = float.MinValue;
            var ccdCount = 0;
            float ccdTemperatureSum = 0;
            for (uint i = 0; i < _ccdTemperatures.Length; i++)
                if (ReadSmnRegister(FAMILY_17H_M70H_CCD_TEMP(i), out value))
                {
                    if ((value & Family17HM70HCcdTempValid) == 0)
                        continue;

                    var temperature = (value & 0x7FF) / 8.0f - 49;
                    temperature += _ccdTemperatures[i].Parameters[0].Value;

                    if (temperature > maxTemperature)
                        maxTemperature = temperature;
                    ccdCount++;
                    ccdTemperatureSum += temperature;

                    _ccdTemperatures[i].Value = temperature;
                    ActivateSensor(_ccdTemperatures[i]);
                }

            if (ccdCount > 1)
            {
                _ccdMaxTemperature.Value = maxTemperature;
                ActivateSensor(_ccdMaxTemperature);

                _ccdAvgTemperature.Value = ccdTemperatureSum / ccdCount;
                ActivateSensor(_ccdAvgTemperature);
            }

            Ring0.ReleasePciBusMutex();
        }

        if (_energyUnitMultiplier != 0 &&
            Ring0.Rdmsr(MsrPkgEnergyStat, out var energyConsumed, out _))
        {
            var time = DateTime.UtcNow;
            var deltaTime = (time - _lastEnergyTime).TotalSeconds;
            if (deltaTime > 0.01)
            {
                _packagePowerSensor.Value = _energyUnitMultiplier * unchecked(
                    energyConsumed - _lastEnergyConsumed) / deltaTime;
                _lastEnergyTime = time;
                _lastEnergyConsumed = energyConsumed;
            }
        }

        double? coresPower = 0f;
        for (var i = 0; i < _cores.Length; i++)
        {
            _cores[i].Update();
            coresPower += _cores[i].Power;
        }

        _coresPowerSensor.Value = coresPower;

        if (coresPower.HasValue) ActivateSensor(_coresPowerSensor);
    }

    private class Core
    {
        private readonly Amd17Cpu _cpu;
        private readonly GroupAffinity _affinity;

        private readonly Sensor _powerSensor;
        private readonly Sensor _clockSensor;

        private DateTime _lastEnergyTime;
        private uint _lastEnergyConsumed;
        private double? _power = null;

        public Core(int index, Cpuid[] threads, Amd17Cpu cpu, ISettings settings)
        {
            this._cpu = cpu;
            _affinity = threads[0].Affinity;

            var coreString = cpu.CoreString(index);
            _powerSensor =
                new Sensor(coreString, index + 2, SensorType.Power, cpu, settings);
            _clockSensor =
                new Sensor(coreString, index + 1, SensorType.Clock, cpu, settings);

            if (cpu._energyUnitMultiplier != 0)
                if (Ring0.RdmsrTx(MsrCoreEnergyStat, out var energyConsumed,
                        out _, _affinity))
                {
                    _lastEnergyTime = DateTime.UtcNow;
                    _lastEnergyConsumed = energyConsumed;
                    cpu.ActivateSensor(_powerSensor);
                }
        }

        private double? GetMultiplier()
        {
            if (Ring0.Rdmsr(MsrFamily17HPState, out var eax, out _))
            {
                var cpuDfsId = (eax >> 8) & 0x3f;
                var cpuFid = eax & 0xff;
                return 2.0 * cpuFid / cpuDfsId;
            }
            else
            {
                return null;
            }
        }

        public double? Power => _power;

        public void Update()
        {
            var energyTime = DateTime.MinValue;
            double? multiplier = null;

            var previousAffinity = ThreadAffinity.Set(_affinity);
            if (Ring0.Rdmsr(MsrCoreEnergyStat, out var energyConsumed, out _)) energyTime = DateTime.UtcNow;

            multiplier = GetMultiplier();
            ThreadAffinity.Set(previousAffinity);

            if (_cpu._energyUnitMultiplier != 0)
            {
                var deltaTime = (energyTime - _lastEnergyTime).TotalSeconds;
                if (deltaTime > 0.01)
                {
                    _power = _cpu._energyUnitMultiplier *
                        unchecked(energyConsumed - _lastEnergyConsumed) / deltaTime;
                    _powerSensor.Value = _power;
                    _lastEnergyTime = energyTime;
                    _lastEnergyConsumed = energyConsumed;
                }
            }

            if (multiplier.HasValue)
            {
                var clock = (float?)(multiplier * _cpu._busClock.Value);
                _clockSensor.Value = clock;
                if (clock.HasValue)
                    _cpu.ActivateSensor(_clockSensor);
            }
        }
    }
}
