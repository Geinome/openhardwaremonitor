/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware.ATI;

internal sealed class Atigpu : Hardware
{
    private readonly int _adapterIndex;
    private readonly int _busNumber;
    private readonly int _deviceNumber;
    private readonly Sensor _temperatureCore;
    private readonly Sensor _temperatureMemory;
    private readonly Sensor _temperatureVrmCore;
    private readonly Sensor _temperatureVrmMemory;
    private readonly Sensor _temperatureVrmMemory0;
    private readonly Sensor _temperatureVrmMemory1;
    private readonly Sensor _temperatureLiquid;
    private readonly Sensor _temperaturePlx;
    private readonly Sensor _temperatureHotSpot;
    private readonly Sensor _temperatureVrmSoc;
    private readonly Sensor _powerCore;
    private readonly Sensor _powerPpt;
    private readonly Sensor _powerSocket;
    private readonly Sensor _powerTotal;
    private readonly Sensor _powerSoc;
    private readonly Sensor _fan;
    private readonly Sensor _coreClock;
    private readonly Sensor _memoryClock;
    private readonly Sensor _socClock;
    private readonly Sensor _coreVoltage;
    private readonly Sensor _memoryVoltage;
    private readonly Sensor _socVoltage;
    private readonly Sensor _coreLoad;
    private readonly Sensor _memoryLoad;
    private readonly Sensor _controlSensor;
    private readonly Control _fanControl;

    private IntPtr _context;
    private readonly int _overdriveVersion;
    private readonly ILogger _logger;

    public Atigpu(string name, int adapterIndex, int busNumber,
        int deviceNumber, IntPtr context, ISettings settings)
        : base(name, new Identifier("atigpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings)
    {
        _logger = this.GetCurrentClassLogger();
        this._adapterIndex = adapterIndex;
        this._busNumber = busNumber;
        this._deviceNumber = deviceNumber;

        this._context = context;

        var status = Adl.AdlOverdriveCaps(adapterIndex, out var supported, out var enabled,
            out _overdriveVersion);
        if (status != AdlStatus.OK)
        {
            _logger.LogWarning(
                $"Overdrive version not available for adapter {adapterIndex}. Status {status}, Supported {supported}, enabled {enabled}");
            _overdriveVersion = -1;
        }

        _logger.LogInformation(
            $"Overdrive version {_overdriveVersion} on adapter {adapterIndex}. Status {status}, Supported {supported}, enabled {enabled}");
        _temperatureCore = new Sensor("GPU Core", 0, SensorType.Temperature, this, settings);
        _temperatureMemory =
            new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings);
        _temperatureVrmCore =
            new Sensor("GPU VRM Core", 2, SensorType.Temperature, this, settings);
        _temperatureVrmMemory =
            new Sensor("GPU VRM Memory", 3, SensorType.Temperature, this, settings);
        _temperatureVrmMemory0 =
            new Sensor("GPU VRM Memory #1", 4, SensorType.Temperature, this, settings);
        _temperatureVrmMemory1 =
            new Sensor("GPU VRM Memory #2", 5, SensorType.Temperature, this, settings);
        _temperatureVrmSoc =
            new Sensor("GPU VRM SOC", 6, SensorType.Temperature, this, settings);
        _temperatureLiquid =
            new Sensor("GPU Liquid", 7, SensorType.Temperature, this, settings);
        _temperaturePlx =
            new Sensor("GPU PLX", 8, SensorType.Temperature, this, settings);
        _temperatureHotSpot =
            new Sensor("GPU Hot Spot", 9, SensorType.Temperature, this, settings);

        _powerTotal = new Sensor("GPU Total", 0, SensorType.Power, this, settings);
        _powerCore = new Sensor("GPU Core", 1, SensorType.Power, this, settings);
        _powerPpt = new Sensor("GPU PPT", 2, SensorType.Power, this, settings);
        _powerSocket = new Sensor("GPU Socket", 3, SensorType.Power, this, settings);
        _powerSoc = new Sensor("GPU SOC", 4, SensorType.Power, this, settings);

        _fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings);

        _coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
        _memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
        _socClock = new Sensor("GPU SOC", 2, SensorType.Clock, this, settings);

        _coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings);
        _memoryVoltage = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings);
        _socVoltage = new Sensor("GPU SOC", 2, SensorType.Voltage, this, settings);

        _coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
        _memoryLoad = new Sensor("GPU Memory", 1, SensorType.Load, this, settings);

        _controlSensor = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

        var afsi = new AdlFanSpeedInfo();
        if (Adl.AdlOverdrive5FanSpeedInfoGet(adapterIndex, 0, ref afsi)
            != AdlStatus.OK)
        {
            afsi.MaxPercent = 100;
            afsi.MinPercent = 0;
        }

        _fanControl = new Control(_controlSensor, settings, afsi.MinPercent,
            afsi.MaxPercent);
        _fanControl.ControlModeChanged += ControlModeChanged;
        _fanControl.SoftwareControlValueChanged +=
            SoftwareControlValueChanged;
        ControlModeChanged(_fanControl);
        _controlSensor.Control = _fanControl;
        Update();
    }

    private void SoftwareControlValueChanged(IControl control)
    {
        if (control.ControlMode == ControlMode.Software)
        {
            var adlf = new AdlFanSpeedValue();
            adlf.SpeedType = Adl.AdlDlFanctrlSpeedTypePercent;
            adlf.Flags = Adl.AdlDlFanctrlFlagUserDefinedSpeed;
            adlf.FanSpeed = (int)control.SoftwareValue;
            Adl.AdlOverdrive5FanSpeedSet(_adapterIndex, 0, ref adlf);
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
        Adl.AdlOverdrive5FanSpeedToDefaultSet(_adapterIndex, 0);
    }

    public int BusNumber => _busNumber;

    public int DeviceNumber => _deviceNumber;


    public override HardwareType HardwareType => HardwareType.GpuAti;

    private void GetOdnTemperature(AdlodnTemperatureType type,
        Sensor sensor)
    {
        if (Adl.Adl2OverdriveNTemperatureGet(_context, _adapterIndex,
                type, out var temperature) == AdlStatus.OK)
        {
            sensor.Value = 0.001f * temperature;
            ActivateSensor(sensor);
        }
        else
        {
            sensor.Value = null;
        }
    }

    private void GetOd6Power(AdlodnCurrentPowerType type, Sensor sensor)
    {
        if (Adl.Adl2Overdrive6CurrentPowerGet(_context, _adapterIndex, type,
                out var power) == AdlStatus.OK)
        {
            sensor.Value = power * (1.0f / 0xFF);
            ActivateSensor(sensor);
        }
        else
        {
            sensor.Value = null;
        }
    }

    public override string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("AMD GPU");
        r.AppendLine();

        r.Append("AdapterIndex: ");
        r.AppendLine(_adapterIndex.ToString(CultureInfo.InvariantCulture));
        r.AppendLine();

        r.AppendLine("Overdrive Caps");
        r.AppendLine();
        try
        {
            var status = Adl.AdlOverdriveCaps(_adapterIndex,
                out var supported, out var enabled, out var version);
            r.Append(" Status: ");
            r.AppendLine(status.ToString());
            r.Append(" Supported: ");
            r.AppendLine(supported.ToString(CultureInfo.InvariantCulture));
            r.Append(" Enabled: ");
            r.AppendLine(enabled.ToString(CultureInfo.InvariantCulture));
            r.Append(" Version: ");
            r.AppendLine(version.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception e)
        {
            r.AppendLine(" Status: " + e.Message);
        }

        r.AppendLine();

        r.AppendLine("Overdrive5 Parameters");
        r.AppendLine();
        try
        {
            var status = Adl.AdlOverdrive5OdParametersGet(
                _adapterIndex, out var p);
            r.Append(" Status: ");
            r.AppendLine(status.ToString());
            r.AppendFormat(" NumberOfPerformanceLevels: {0}{1}",
                p.NumberOfPerformanceLevels, Environment.NewLine);
            r.AppendFormat(" ActivityReportingSupported: {0}{1}",
                p.ActivityReportingSupported, Environment.NewLine);
            r.AppendFormat(" DiscretePerformanceLevels: {0}{1}",
                p.DiscretePerformanceLevels, Environment.NewLine);
            r.AppendFormat(" EngineClock.Min: {0}{1}",
                p.EngineClock.Min, Environment.NewLine);
            r.AppendFormat(" EngineClock.Max: {0}{1}",
                p.EngineClock.Max, Environment.NewLine);
            r.AppendFormat(" EngineClock.Step: {0}{1}",
                p.EngineClock.Step, Environment.NewLine);
            r.AppendFormat(" MemoryClock.Min: {0}{1}",
                p.MemoryClock.Min, Environment.NewLine);
            r.AppendFormat(" MemoryClock.Max: {0}{1}",
                p.MemoryClock.Max, Environment.NewLine);
            r.AppendFormat(" MemoryClock.Step: {0}{1}",
                p.MemoryClock.Step, Environment.NewLine);
            r.AppendFormat(" Vddc.Min: {0}{1}",
                p.Vddc.Min, Environment.NewLine);
            r.AppendFormat(" Vddc.Max: {0}{1}",
                p.Vddc.Max, Environment.NewLine);
            r.AppendFormat(" Vddc.Step: {0}{1}",
                p.Vddc.Step, Environment.NewLine);
        }
        catch (Exception e)
        {
            r.AppendLine(" Status: " + e.Message);
        }

        r.AppendLine();

        r.AppendLine("Overdrive5 Temperature");
        r.AppendLine();
        try
        {
            var adlt = new AdlTemperature();
            var status = Adl.AdlOverdrive5TemperatureGet(_adapterIndex, 0,
                ref adlt);
            r.Append(" Status: ");
            r.AppendLine(status.ToString());
            r.AppendFormat(" Value: {0}{1}",
                0.001f * adlt.Temperature, Environment.NewLine);
        }
        catch (Exception e)
        {
            r.AppendLine(" Status: " + e.Message);
        }

        r.AppendLine();

        r.AppendLine("Overdrive5 FanSpeed");
        r.AppendLine();
        try
        {
            var adlf = new AdlFanSpeedValue();
            adlf.SpeedType = Adl.AdlDlFanctrlSpeedTypeRpm;
            var status = Adl.AdlOverdrive5FanSpeedGet(_adapterIndex, 0, ref adlf);
            r.Append(" Status RPM: ");
            r.AppendLine(status.ToString());
            r.AppendFormat(" Value RPM: {0}{1}",
                adlf.FanSpeed, Environment.NewLine);
            adlf.SpeedType = Adl.AdlDlFanctrlSpeedTypePercent;
            status = Adl.AdlOverdrive5FanSpeedGet(_adapterIndex, 0, ref adlf);
            r.Append(" Status Percent: ");
            r.AppendLine(status.ToString());
            r.AppendFormat(" Value Percent: {0}{1}",
                adlf.FanSpeed, Environment.NewLine);
        }
        catch (Exception e)
        {
            r.AppendLine(" Status: " + e.Message);
        }

        r.AppendLine();

        r.AppendLine("Overdrive5 CurrentActivity");
        r.AppendLine();
        try
        {
            var adlp = new AdlpmActivity();
            var status = Adl.AdlOverdrive5CurrentActivityGet(_adapterIndex,
                ref adlp);
            r.Append(" Status: ");
            r.AppendLine(status.ToString());
            r.AppendFormat(" EngineClock: {0}{1}",
                0.01f * adlp.EngineClock, Environment.NewLine);
            r.AppendFormat(" MemoryClock: {0}{1}",
                0.01f * adlp.MemoryClock, Environment.NewLine);
            r.AppendFormat(" Vddc: {0}{1}",
                0.001f * adlp.Vddc, Environment.NewLine);
            r.AppendFormat(" ActivityPercent: {0}{1}",
                adlp.ActivityPercent, Environment.NewLine);
            r.AppendFormat(" CurrentPerformanceLevel: {0}{1}",
                adlp.CurrentPerformanceLevel, Environment.NewLine);
            r.AppendFormat(" CurrentBusSpeed: {0}{1}",
                adlp.CurrentBusSpeed, Environment.NewLine);
            r.AppendFormat(" CurrentBusLanes: {0}{1}",
                adlp.CurrentBusLanes, Environment.NewLine);
            r.AppendFormat(" MaximumBusLanes: {0}{1}",
                adlp.MaximumBusLanes, Environment.NewLine);
        }
        catch (Exception e)
        {
            r.AppendLine(" Status: " + e.Message);
        }

        r.AppendLine();

        if (_context != IntPtr.Zero)
        {
            r.AppendLine("Overdrive6 CurrentPower");
            r.AppendLine();
            try
            {
                for (var i = 0; i < 4; i++)
                {
                    var pt = ((AdlodnCurrentPowerType)i).ToString();
                    var status = Adl.Adl2Overdrive6CurrentPowerGet(
                        _context, _adapterIndex, (AdlodnCurrentPowerType)i,
                        out var power);
                    if (status == AdlStatus.OK)
                        r.AppendFormat(" Power[{0}].Value: {1}{2}", pt,
                            power * (1.0f / 0xFF), Environment.NewLine);
                    else
                        r.AppendFormat(" Power[{0}].Status: {1}{2}", pt,
                            status.ToString(), Environment.NewLine);
                }
            }
            catch (EntryPointNotFoundException)
            {
                r.AppendLine(" Status: Entry point not found");
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }

            r.AppendLine();
        }

        if (_context != IntPtr.Zero)
        {
            r.AppendLine("OverdriveN Temperature");
            r.AppendLine();
            try
            {
                for (var i = 1; i < 8; i++)
                {
                    var tt = ((AdlodnTemperatureType)i).ToString();
                    var status = Adl.Adl2OverdriveNTemperatureGet(
                        _context, _adapterIndex, (AdlodnTemperatureType)i,
                        out var temperature);
                    if (status == AdlStatus.OK)
                        r.AppendFormat(" Temperature[{0}].Value: {1}{2}", tt,
                            0.001f * temperature, Environment.NewLine);
                    else
                        r.AppendFormat(" Temperature[{0}].Status: {1}{2}", tt,
                            status.ToString(), Environment.NewLine);
                }
            }
            catch (EntryPointNotFoundException)
            {
                r.AppendLine(" Status: Entry point not found");
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }

            r.AppendLine();
        }

        if (_context != IntPtr.Zero)
        {
            r.AppendLine("OverdriveN Performance Status");
            r.AppendLine();
            try
            {
                var status = Adl.Adl2OverdriveNPerformanceStatusGet(_context,
                    _adapterIndex, out var ps);
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
                r.AppendFormat(" CoreClock: {0}{1}",
                    ps.CoreClock, Environment.NewLine);
                r.AppendFormat(" MemoryClock: {0}{1}",
                    ps.MemoryClock, Environment.NewLine);
                r.AppendFormat(" DCEFClock: {0}{1}",
                    ps.DCEFClock, Environment.NewLine);
                r.AppendFormat(" GFXClock: {0}{1}",
                    ps.GFXClock, Environment.NewLine);
                r.AppendFormat(" UVDClock: {0}{1}",
                    ps.UVDClock, Environment.NewLine);
                r.AppendFormat(" VCEClock: {0}{1}",
                    ps.VCEClock, Environment.NewLine);
                r.AppendFormat(" GPUActivityPercent: {0}{1}",
                    ps.GPUActivityPercent, Environment.NewLine);
                r.AppendFormat(" CurrentCorePerformanceLevel: {0}{1}",
                    ps.CurrentCorePerformanceLevel, Environment.NewLine);
                r.AppendFormat(" CurrentMemoryPerformanceLevel: {0}{1}",
                    ps.CurrentMemoryPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" CurrentDCEFPerformanceLevel: {0}{1}",
                    ps.CurrentDCEFPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" CurrentGFXPerformanceLevel: {0}{1}",
                    ps.CurrentGFXPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" UVDPerformanceLevel: {0}{1}",
                    ps.UVDPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" VCEPerformanceLevel: {0}{1}",
                    ps.VCEPerformanceLevel, Environment.NewLine);
                r.AppendFormat(" CurrentBusSpeed: {0}{1}",
                    ps.CurrentBusSpeed, Environment.NewLine);
                r.AppendFormat(" CurrentBusLanes: {0}{1}",
                    ps.CurrentBusLanes, Environment.NewLine);
                r.AppendFormat(" MaximumBusLanes: {0}{1}",
                    ps.MaximumBusLanes, Environment.NewLine);
                r.AppendFormat(" VDDC: {0}{1}",
                    ps.VDDC, Environment.NewLine);
                r.AppendFormat(" VDDCI: {0}{1}",
                    ps.VDDCI, Environment.NewLine);
            }
            catch (EntryPointNotFoundException)
            {
                r.AppendLine(" Status: Entry point not found");
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }

            r.AppendLine();
        }

        if (_context != IntPtr.Zero)
        {
            r.AppendLine("Performance Metrics");
            r.AppendLine();
            try
            {
                var status = Adl.Adl2NewQueryPmLogDataGet(_context, _adapterIndex,
                    out var data);
                if (status == AdlStatus.OK)
                {
                    for (var i = 0; i < data.Sensors.Length; i++)
                        if (data.Sensors[i].Supported)
                        {
                            var st = ((AdlSensorType)i).ToString();
                            r.AppendFormat(" Sensor[{0}].Value: {1}{2}", st,
                                data.Sensors[i].Value, Environment.NewLine);
                        }
                }
                else
                {
                    r.Append(" Status: ");
                    r.AppendLine(status.ToString());
                }
            }
            catch (EntryPointNotFoundException)
            {
                r.AppendLine(" Status: Entry point not found");
            }
            catch (Exception e)
            {
                r.AppendLine(" Status: " + e.Message);
            }

            r.AppendLine();
        }

        return r.ToString();
    }

    private void GetPmLog(AdlpmLogDataOutput data,
        AdlSensorType sensorType, Sensor sensor, float factor = 1.0f)
    {
        var i = (int)sensorType;
        if (i < data.Sensors.Length && data.Sensors[i].Supported)
        {
            sensor.Value = data.Sensors[i].Value * factor;
            ActivateSensor(sensor);
        }
    }

    public override void Update()
    {
        if (_context != IntPtr.Zero && _overdriveVersion >= 8 &&
            Adl.Adl2NewQueryPmLogDataGet(_context, _adapterIndex,
                out var data) == AdlStatus.OK)
        {
            GetPmLog(data, AdlSensorType.TEMPERATURE_EDGE, _temperatureCore);
            GetPmLog(data, AdlSensorType.TEMPERATURE_MEM, _temperatureMemory);
            GetPmLog(data, AdlSensorType.TEMPERATURE_VRVDDC, _temperatureVrmCore);
            GetPmLog(data, AdlSensorType.TEMPERATURE_VRMVDD, _temperatureVrmMemory);
            GetPmLog(data, AdlSensorType.TEMPERATURE_VRMVDD0, _temperatureVrmMemory0);
            GetPmLog(data, AdlSensorType.TEMPERATURE_VRMVDD1, _temperatureVrmMemory1);
            GetPmLog(data, AdlSensorType.TEMPERATURE_VRSOC, _temperatureVrmSoc);
            GetPmLog(data, AdlSensorType.TEMPERATURE_LIQUID, _temperatureLiquid);
            GetPmLog(data, AdlSensorType.TEMPERATURE_PLX, _temperaturePlx);
            GetPmLog(data, AdlSensorType.TEMPERATURE_HOTSPOT, _temperatureHotSpot);
            GetPmLog(data, AdlSensorType.GFX_POWER, _powerCore);
            GetPmLog(data, AdlSensorType.ASIC_POWER, _powerTotal);
            GetPmLog(data, AdlSensorType.SOC_POWER, _powerSoc);
            GetPmLog(data, AdlSensorType.FAN_RPM, _fan);
            GetPmLog(data, AdlSensorType.CLK_GFXCLK, _coreClock);
            GetPmLog(data, AdlSensorType.CLK_MEMCLK, _memoryClock);
            GetPmLog(data, AdlSensorType.CLK_SOCCLK, _socClock);
            GetPmLog(data, AdlSensorType.GFX_VOLTAGE, _coreVoltage, 0.001f);
            GetPmLog(data, AdlSensorType.MEM_VOLTAGE, _memoryVoltage, 0.001f);
            GetPmLog(data, AdlSensorType.SOC_VOLTAGE, _socVoltage, 0.001f);
            GetPmLog(data, AdlSensorType.INFO_ACTIVITY_GFX, _coreLoad);
            GetPmLog(data, AdlSensorType.INFO_ACTIVITY_MEM, _memoryLoad);
            GetPmLog(data, AdlSensorType.FAN_PERCENTAGE, _controlSensor);
        }
        else
        {
            if (_context != IntPtr.Zero && _overdriveVersion >= 7)
            {
                GetOdnTemperature(AdlodnTemperatureType.CORE, _temperatureCore);
                GetOdnTemperature(AdlodnTemperatureType.MEMORY, _temperatureMemory);
                GetOdnTemperature(AdlodnTemperatureType.VRM_CORE, _temperatureVrmCore);
                GetOdnTemperature(AdlodnTemperatureType.VRM_MEMORY, _temperatureVrmMemory);
                GetOdnTemperature(AdlodnTemperatureType.LIQUID, _temperatureLiquid);
                GetOdnTemperature(AdlodnTemperatureType.PLX, _temperaturePlx);
                GetOdnTemperature(AdlodnTemperatureType.HOTSPOT, _temperatureHotSpot);
            }
            else
            {
                var adlt = new AdlTemperature();
                if (Adl.AdlOverdrive5TemperatureGet(_adapterIndex, 0, ref adlt)
                    == AdlStatus.OK)
                {
                    _temperatureCore.Value = 0.001f * adlt.Temperature;
                    ActivateSensor(_temperatureCore);
                }
                else
                {
                    _temperatureCore.Value = null;
                }
            }

            if (_context != IntPtr.Zero && _overdriveVersion >= 6)
            {
                GetOd6Power(AdlodnCurrentPowerType.TOTAL_POWER, _powerTotal);
                GetOd6Power(AdlodnCurrentPowerType.CHIP_POWER, _powerCore);
                GetOd6Power(AdlodnCurrentPowerType.PPT_POWER, _powerPpt);
                GetOd6Power(AdlodnCurrentPowerType.SOCKET_POWER, _powerSocket);
            }

            var adlf = new AdlFanSpeedValue();
            adlf.SpeedType = Adl.AdlDlFanctrlSpeedTypeRpm;
            if (Adl.AdlOverdrive5FanSpeedGet(_adapterIndex, 0, ref adlf)
                == AdlStatus.OK)
            {
                _fan.Value = adlf.FanSpeed;
                ActivateSensor(_fan);
            }
            else
            {
                _fan.Value = null;
            }

            adlf = new AdlFanSpeedValue();
            adlf.SpeedType = Adl.AdlDlFanctrlSpeedTypePercent;
            if (Adl.AdlOverdrive5FanSpeedGet(_adapterIndex, 0, ref adlf)
                == AdlStatus.OK)
            {
                _controlSensor.Value = adlf.FanSpeed;
                ActivateSensor(_controlSensor);
            }
            else
            {
                _controlSensor.Value = null;
            }

            var adlp = new AdlpmActivity();
            if (Adl.AdlOverdrive5CurrentActivityGet(_adapterIndex, ref adlp)
                == AdlStatus.OK)
            {
                if (adlp.EngineClock > 0)
                {
                    _coreClock.Value = 0.01f * adlp.EngineClock;
                    ActivateSensor(_coreClock);
                }
                else
                {
                    _coreClock.Value = null;
                }

                if (adlp.MemoryClock > 0)
                {
                    _memoryClock.Value = 0.01f * adlp.MemoryClock;
                    ActivateSensor(_memoryClock);
                }
                else
                {
                    _memoryClock.Value = null;
                }

                if (adlp.Vddc > 0)
                {
                    _coreVoltage.Value = 0.001f * adlp.Vddc;
                    ActivateSensor(_coreVoltage);
                }
                else
                {
                    _coreVoltage.Value = null;
                }

                if (adlp.ActivityPercent >= 0 && adlp.ActivityPercent <= 100)
                {
                    _coreLoad.Value = adlp.ActivityPercent;
                    ActivateSensor(_coreLoad);
                }
                else
                {
                    _coreLoad.Value = null;
                }
            }
            else
            {
                _coreClock.Value = null;
                _memoryClock.Value = null;
                _coreVoltage.Value = null;
                _coreLoad.Value = null;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fanControl.ControlModeChanged -= ControlModeChanged;
            _fanControl.SoftwareControlValueChanged -=
                SoftwareControlValueChanged;

            if (_fanControl.ControlMode != ControlMode.Undefined)
                SetDefaultFanSpeed();
        }

        base.Dispose(disposing);
    }
}
