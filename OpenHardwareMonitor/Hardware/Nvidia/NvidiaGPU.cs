/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2011 Christian Vallières

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Nvidia;

internal class NvidiaGpu : Hardware
{
    private readonly int _adapterIndex;
    private readonly NvPhysicalGpuHandle _handle;
    private readonly NvDisplayHandle? _displayHandle;
    private readonly Nvml.NvmlDevice? _device;

    private readonly Sensor[] _temperatures;
    private readonly Sensor _fan;
    private readonly Sensor[] _clocks;
    private readonly Sensor[] _loads;
    private readonly Sensor _control;
    private readonly Sensor _memoryLoad;
    private readonly Sensor _memoryUsed;
    private readonly Sensor _memoryFree;
    private readonly Sensor _memoryAvail;
    private readonly Sensor _power;
    private readonly Sensor _pcieThroughputRx;
    private readonly Sensor _pcieThroughputTx;
    private readonly Control _fanControl;

    public NvidiaGpu(int adapterIndex, NvPhysicalGpuHandle handle,
        NvDisplayHandle? displayHandle, ISettings settings)
        : base(GetName(handle), new Identifier("nvidiagpu",
            adapterIndex.ToString(CultureInfo.InvariantCulture)), settings)
    {
        this._adapterIndex = adapterIndex;
        this._handle = handle;
        this._displayHandle = displayHandle;

        var thermalSettings = GetThermalSettings();
        _temperatures = new Sensor[thermalSettings.Count];
        for (var i = 0; i < _temperatures.Length; i++)
        {
            var sensor = thermalSettings.Sensor[i];
            string name;
            switch (sensor.Target)
            {
                case NvThermalTarget.BOARD:
                    name = "GPU Board";
                    break;
                case NvThermalTarget.GPU:
                    name = "GPU Core";
                    break;
                case NvThermalTarget.MEMORY:
                    name = "GPU Memory";
                    break;
                case NvThermalTarget.POWER_SUPPLY:
                    name = "GPU Power Supply";
                    break;
                case NvThermalTarget.UNKNOWN:
                    name = "GPU Unknown";
                    break;
                default:
                    name = "GPU";
                    break;
            }

            _temperatures[i] = new Sensor(name, i, SensorType.Temperature, this,
                new ParameterDescription[0], settings);
            ActivateSensor(_temperatures[i]);
        }

        _fan = new Sensor("GPU", 0, SensorType.Fan, this, settings);

        _clocks = new Sensor[3];
        _clocks[0] = new Sensor("GPU Core", 0, SensorType.Clock, this, settings);
        _clocks[1] = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings);
        _clocks[2] = new Sensor("GPU Shader", 2, SensorType.Clock, this, settings);
        for (var i = 0; i < _clocks.Length; i++)
            ActivateSensor(_clocks[i]);

        _loads = new Sensor[4];
        _loads[0] = new Sensor("GPU Core", 0, SensorType.Load, this, settings);
        _loads[1] = new Sensor("GPU Frame Buffer", 1, SensorType.Load, this, settings);
        _loads[2] = new Sensor("GPU Video Engine", 2, SensorType.Load, this, settings);
        _loads[3] = new Sensor("GPU Bus Interface", 3, SensorType.Load, this, settings);
        _memoryLoad = new Sensor("GPU Memory", 4, SensorType.Load, this, settings);
        _memoryFree = new Sensor("GPU Memory Free", 1, SensorType.SmallData, this, settings);
        _memoryUsed = new Sensor("GPU Memory Used", 2, SensorType.SmallData, this, settings);
        _memoryAvail = new Sensor("GPU Memory Total", 3, SensorType.SmallData, this, settings);
        _control = new Sensor("GPU Fan", 0, SensorType.Control, this, settings);

        var coolerSettings = GetCoolerSettings();
        if (coolerSettings.Count > 0)
        {
            _fanControl = new Control(_control, settings,
                coolerSettings.Cooler[0].DefaultMin,
                coolerSettings.Cooler[0].DefaultMax);
            _fanControl.ControlModeChanged += ControlModeChanged;
            _fanControl.SoftwareControlValueChanged += SoftwareControlValueChanged;
            ControlModeChanged(_fanControl);
            _control.Control = _fanControl;
        }

        if (Nvml.IsInitialized)
            if (Nvapi.NvApiGpuGetBusId != null &&
                Nvapi.NvApiGpuGetBusId(handle, out var busId) == NvStatus.OK)
                if (Nvml.NvmlDeviceGetHandleByPciBusId != null &&
                    Nvml.NvmlDeviceGetHandleByPciBusId(
                        "0000:" + busId.ToString("X2") + ":00.0", out var result)
                    == Nvml.NvmlReturn.Success)
                {
                    _device = result;
                    _power = new Sensor("GPU Power", 0, SensorType.Power, this, settings);
                    _pcieThroughputRx = new Sensor("GPU PCIE Rx", 0,
                        SensorType.Throughput, this, settings);
                    _pcieThroughputTx = new Sensor("GPU PCIE Tx", 1,
                        SensorType.Throughput, this, settings);
                }

        Update();
    }

    private static string GetName(NvPhysicalGpuHandle handle)
    {
        string gpuName;
        if (Nvapi.NvAPI_GPU_GetFullName(handle, out gpuName) == NvStatus.OK)
            return "NVIDIA " + gpuName.Trim();
        else
            return "NVIDIA";
    }

    public override HardwareType HardwareType => HardwareType.GpuNvidia;

    private NvGpuThermalSettings GetThermalSettings()
    {
        var settings = new NvGpuThermalSettings();
        settings.Version = Nvapi.GpuThermalSettingsVer;
        settings.Count = Nvapi.MaxThermalSensorsPerGpu;
        settings.Sensor = new NvSensor[Nvapi.MaxThermalSensorsPerGpu];
        if (!(Nvapi.NvApiGpuGetThermalSettings != null &&
              Nvapi.NvApiGpuGetThermalSettings(_handle, (int)NvThermalTarget.ALL,
                  ref settings) == NvStatus.OK))
            settings.Count = 0;
        return settings;
    }

    private NvGpuCoolerSettings GetCoolerSettings()
    {
        var settings = new NvGpuCoolerSettings();
        settings.Version = Nvapi.GpuCoolerSettingsVer;
        settings.Cooler = new NvCooler[Nvapi.MaxCoolerPerGpu];
        if (!(Nvapi.NvApiGpuGetCoolerSettings != null &&
              Nvapi.NvApiGpuGetCoolerSettings(_handle, 0,
                  ref settings) == NvStatus.OK))
            settings.Count = 0;
        return settings;
    }

    private NvFanCoolersStatus GetFanCoolersStatus()
    {
        var coolers = new NvFanCoolersStatus();
        coolers.Version = Nvapi.GpuFanCoolersStatusVer;
        coolers.Items =
            new NvFanCoolersStatusItem[Nvapi.MaxFanCoolersStatusItems];

        if (!(Nvapi.NvApiGpuClientFanCoolersGetStatus != null &&
              Nvapi.NvApiGpuClientFanCoolersGetStatus(_handle, ref coolers)
              == NvStatus.OK))
            coolers.Count = 0;
        return coolers;
    }

    private uint[] GetClocks()
    {
        var allClocks = new NvClocks();
        allClocks.Version = Nvapi.GpuClocksVer;
        allClocks.Clock = new uint[Nvapi.MaxClocksPerGpu];
        if (Nvapi.NvApiGpuGetAllClocks != null &&
            Nvapi.NvApiGpuGetAllClocks(_handle, ref allClocks) == NvStatus.OK)
            return allClocks.Clock;
        return null;
    }

    public override void Update()
    {
        var settings = GetThermalSettings();
        foreach (var sensor in _temperatures)
            sensor.Value = settings.Sensor[sensor.Index].CurrentTemp;

        var tachReadingOk = false;
        if (Nvapi.NvApiGpuGetTachReading != null &&
            Nvapi.NvApiGpuGetTachReading(_handle, out var fanValue) == NvStatus.OK)
        {
            _fan.Value = fanValue;
            ActivateSensor(_fan);
            tachReadingOk = true;
        }

        var values = GetClocks();
        if (values != null)
        {
            _clocks[1].Value = 0.001f * values[8];
            if (values[30] != 0)
            {
                _clocks[0].Value = 0.0005f * values[30];
                _clocks[2].Value = 0.001f * values[30];
            }
            else
            {
                _clocks[0].Value = 0.001f * values[0];
                _clocks[2].Value = 0.001f * values[14];
            }
        }

        var infoEx = new NvDynamicPstatesInfoEx();
        infoEx.Version = Nvapi.GpuDynamicPstatesInfoExVer;
        infoEx.UtilizationDomains =
            new NvUtilizationDomainEx[Nvapi.NvapiMaxGpuUtilizations];
        if (Nvapi.NvApiGpuGetDynamicPstatesInfoEx != null &&
            Nvapi.NvApiGpuGetDynamicPstatesInfoEx(_handle, ref infoEx) == NvStatus.OK)
        {
            for (var i = 0; i < _loads.Length; i++)
                if (infoEx.UtilizationDomains[i].Present)
                {
                    _loads[i].Value = infoEx.UtilizationDomains[i].Percentage;
                    ActivateSensor(_loads[i]);
                }
        }
        else
        {
            var info = new NvDynamicPstatesInfo();
            info.Version = Nvapi.GpuDynamicPstatesInfoVer;
            info.UtilizationDomains =
                new NvUtilizationDomain[Nvapi.NvapiMaxGpuUtilizations];
            if (Nvapi.NvApiGpuGetDynamicPstatesInfo != null &&
                Nvapi.NvApiGpuGetDynamicPstatesInfo(_handle, ref info) == NvStatus.OK)
                for (var i = 0; i < _loads.Length; i++)
                    if (info.UtilizationDomains[i].Present)
                    {
                        _loads[i].Value = info.UtilizationDomains[i].Percentage;
                        ActivateSensor(_loads[i]);
                    }
        }

        var coolerSettings = GetCoolerSettings();
        var coolerSettingsOk = false;
        if (coolerSettings.Count > 0)
        {
            _control.Value = coolerSettings.Cooler[0].CurrentLevel;
            ActivateSensor(_control);
            coolerSettingsOk = true;
        }

        if (!tachReadingOk || !coolerSettingsOk)
        {
            var coolersStatus = GetFanCoolersStatus();
            if (coolersStatus.Count > 0)
            {
                if (!coolerSettingsOk)
                {
                    _control.Value = coolersStatus.Items[0].CurrentLevel;
                    ActivateSensor(_control);
                    coolerSettingsOk = true;
                }

                if (!tachReadingOk)
                {
                    _fan.Value = coolersStatus.Items[0].CurrentRpm;
                    ActivateSensor(_fan);
                    tachReadingOk = true;
                }
            }
        }

        var memoryInfo = new NvGpuMemoryInfo();
        memoryInfo.Version = Nvapi.GpuMemoryInfoVer;
        memoryInfo.Values = new uint[Nvapi.MaxMemoryValuesPerGpu];
        if (Nvapi.NvApiGpuGetMemoryInfo != null &&
            Nvapi.NvApiGpuGetMemoryInfo(_handle, ref memoryInfo) == NvStatus.OK)
        {
            var totalMemory = memoryInfo.Values[0];
            var freeMemory = memoryInfo.Values[4];
            float usedMemory = Math.Max(totalMemory - freeMemory, 0);
            _memoryFree.Value = freeMemory / 1024;
            _memoryAvail.Value = totalMemory / 1024;
            _memoryUsed.Value = usedMemory / 1024;
            _memoryLoad.Value = 100f * usedMemory / totalMemory;
            ActivateSensor(_memoryAvail);
            ActivateSensor(_memoryUsed);
            ActivateSensor(_memoryFree);
            ActivateSensor(_memoryLoad);
        }

        if (_power != null)
            if (Nvml.NvmlDeviceGetPowerUsage(_device.Value, out var powerValue)
                == Nvml.NvmlReturn.Success)
            {
                _power.Value = powerValue * 0.001f;
                ActivateSensor(_power);
            }

        if (_pcieThroughputRx != null)
            if (Nvml.NvmlDeviceGetPcieThroughput(_device.Value,
                    Nvml.NvmlPcieUtilCounter.RxBytes, out var value)
                == Nvml.NvmlReturn.Success)
            {
                _pcieThroughputRx.Value = value * (1.0f / 0x400);
                ActivateSensor(_pcieThroughputRx);
            }

        if (_pcieThroughputTx != null)
            if (Nvml.NvmlDeviceGetPcieThroughput(_device.Value,
                    Nvml.NvmlPcieUtilCounter.TxBytes, out var value)
                == Nvml.NvmlReturn.Success)
            {
                _pcieThroughputTx.Value = value * (1.0f / 0x400);
                ActivateSensor(_pcieThroughputTx);
            }
    }

    public override string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("Nvidia GPU");
        r.AppendLine();

        r.AppendFormat("Name: {0}{1}", Name, Environment.NewLine);
        r.AppendFormat("Index: {0}{1}", _adapterIndex, Environment.NewLine);

        if (_displayHandle.HasValue && Nvapi.NvApiGetDisplayDriverVersion != null)
        {
            var driverVersion = new NvDisplayDriverVersion();
            driverVersion.Version = Nvapi.DisplayDriverVersionVer;
            if (Nvapi.NvApiGetDisplayDriverVersion(_displayHandle.Value,
                    ref driverVersion) == NvStatus.OK)
            {
                r.Append("Driver Version: ");
                r.Append(driverVersion.DriverVersion / 100);
                r.Append(".");
                r.Append((driverVersion.DriverVersion % 100).ToString("00",
                    CultureInfo.InvariantCulture));
                r.AppendLine();
                r.Append("Driver Branch: ");
                r.AppendLine(driverVersion.BuildBranch);
            }
        }

        r.AppendLine();

        if (Nvapi.NvApiGpuGetPciIdentifiers != null)
        {
            uint deviceId, subSystemId, revisionId, extDeviceId;

            var status = Nvapi.NvApiGpuGetPciIdentifiers(_handle,
                out deviceId, out subSystemId, out revisionId, out extDeviceId);

            if (status == NvStatus.OK)
            {
                r.Append("DeviceID: 0x");
                r.AppendLine(deviceId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("SubSystemID: 0x");
                r.AppendLine(subSystemId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("RevisionID: 0x");
                r.AppendLine(revisionId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("ExtDeviceID: 0x");
                r.AppendLine(extDeviceId.ToString("X", CultureInfo.InvariantCulture));
                r.AppendLine();
            }
        }

        if (Nvapi.NvApiGpuGetThermalSettings != null)
        {
            var settings = new NvGpuThermalSettings();
            settings.Version = Nvapi.GpuThermalSettingsVer;
            settings.Count = Nvapi.MaxThermalSensorsPerGpu;
            settings.Sensor = new NvSensor[Nvapi.MaxThermalSensorsPerGpu];

            var status = Nvapi.NvApiGpuGetThermalSettings(_handle,
                (int)NvThermalTarget.ALL, ref settings);

            r.AppendLine("Thermal Settings");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < settings.Count; i++)
                {
                    r.AppendFormat(" Sensor[{0}].Controller: {1}{2}", i,
                        settings.Sensor[i].Controller, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].DefaultMinTemp: {1}{2}", i,
                        settings.Sensor[i].DefaultMinTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].DefaultMaxTemp: {1}{2}", i,
                        settings.Sensor[i].DefaultMaxTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].CurrentTemp: {1}{2}", i,
                        settings.Sensor[i].CurrentTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].Target: {1}{2}", i,
                        settings.Sensor[i].Target, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuGetAllClocks != null)
        {
            var allClocks = new NvClocks();
            allClocks.Version = Nvapi.GpuClocksVer;
            allClocks.Clock = new uint[Nvapi.MaxClocksPerGpu];
            var status = Nvapi.NvApiGpuGetAllClocks(_handle, ref allClocks);

            r.AppendLine("Clocks");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < allClocks.Clock.Length; i++)
                    if (allClocks.Clock[i] > 0)
                        r.AppendFormat(" Clock[{0}]: {1}{2}", i, allClocks.Clock[i],
                            Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuGetTachReading != null)
        {
            int tachValue;
            var status = Nvapi.NvApiGpuGetTachReading(_handle, out tachValue);

            r.AppendLine("Tachometer");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                r.AppendFormat(" Value: {0}{1}", tachValue, Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuGetDynamicPstatesInfoEx != null)
        {
            var info = new NvDynamicPstatesInfoEx();
            info.Version = Nvapi.GpuDynamicPstatesInfoExVer;
            info.UtilizationDomains =
                new NvUtilizationDomainEx[Nvapi.NvapiMaxGpuUtilizations];
            var status = Nvapi.NvApiGpuGetDynamicPstatesInfoEx(_handle, ref info);

            r.AppendLine("Utilization Domains Ex");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < info.UtilizationDomains.Length; i++)
                    if (info.UtilizationDomains[i].Present)
                        r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
                            info.UtilizationDomains[i].Percentage, Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuGetDynamicPstatesInfo != null)
        {
            var info = new NvDynamicPstatesInfo();
            info.Version = Nvapi.GpuDynamicPstatesInfoVer;
            info.UtilizationDomains =
                new NvUtilizationDomain[Nvapi.NvapiMaxGpuUtilizations];
            var status = Nvapi.NvApiGpuGetDynamicPstatesInfo(_handle, ref info);

            r.AppendLine("Utilization Domains");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < info.UtilizationDomains.Length; i++)
                    if (info.UtilizationDomains[i].Present)
                        r.AppendFormat(" Percentage[{0}]: {1}{2}", i,
                            info.UtilizationDomains[i].Percentage, Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuGetCoolerSettings != null)
        {
            var settings = new NvGpuCoolerSettings();
            settings.Version = Nvapi.GpuCoolerSettingsVer;
            settings.Cooler = new NvCooler[Nvapi.MaxCoolerPerGpu];
            var status =
                Nvapi.NvApiGpuGetCoolerSettings(_handle, 0, ref settings);

            r.AppendLine("Cooler Settings");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < settings.Count; i++)
                {
                    r.AppendFormat(" Cooler[{0}].Type: {1}{2}", i,
                        settings.Cooler[i].Type, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Controller: {1}{2}", i,
                        settings.Cooler[i].Controller, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultMin: {1}{2}", i,
                        settings.Cooler[i].DefaultMin, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultMax: {1}{2}", i,
                        settings.Cooler[i].DefaultMax, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentMin: {1}{2}", i,
                        settings.Cooler[i].CurrentMin, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentMax: {1}{2}", i,
                        settings.Cooler[i].CurrentMax, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentLevel: {1}{2}", i,
                        settings.Cooler[i].CurrentLevel, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultPolicy: {1}{2}", i,
                        settings.Cooler[i].DefaultPolicy, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentPolicy: {1}{2}", i,
                        settings.Cooler[i].CurrentPolicy, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Target: {1}{2}", i,
                        settings.Cooler[i].Target, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].ControlType: {1}{2}", i,
                        settings.Cooler[i].ControlType, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Active: {1}{2}", i,
                        settings.Cooler[i].Active, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGetDisplayDriverMemoryInfo != null)
        {
            var memoryInfo = new NvGpuMemoryInfo();
            memoryInfo.Version = Nvapi.GpuMemoryInfoVer;
            memoryInfo.Values = new uint[Nvapi.MaxMemoryValuesPerGpu];
            var status = Nvapi.NvApiGpuGetMemoryInfo(_handle, ref memoryInfo);

            r.AppendLine("Memory Info");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < memoryInfo.Values.Length; i++)
                    r.AppendFormat(" Value[{0}]: {1}{2}", i,
                        memoryInfo.Values[i], Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (Nvapi.NvApiGpuClientFanCoolersGetStatus != null)
        {
            var coolers = new NvFanCoolersStatus();
            coolers.Version = Nvapi.GpuFanCoolersStatusVer;
            coolers.Items =
                new NvFanCoolersStatusItem[Nvapi.MaxFanCoolersStatusItems];

            var status = Nvapi.NvApiGpuClientFanCoolersGetStatus(_handle, ref coolers);

            r.AppendLine("Fan Coolers Status");
            r.AppendLine();
            if (status == NvStatus.OK)
            {
                for (var i = 0; i < coolers.Count; i++)
                {
                    r.AppendFormat(" Items[{0}].Type: {1}{2}", i,
                        coolers.Items[i].Type, Environment.NewLine);
                    r.AppendFormat(" Items[{0}].CurrentRpm: {1}{2}", i,
                        coolers.Items[i].CurrentRpm, Environment.NewLine);
                    r.AppendFormat(" Items[{0}].CurrentMinLevel: {1}{2}", i,
                        coolers.Items[i].CurrentMinLevel, Environment.NewLine);
                    r.AppendFormat(" Items[{0}].CurrentMaxLevel: {1}{2}", i,
                        coolers.Items[i].CurrentMaxLevel, Environment.NewLine);
                    r.AppendFormat(" Items[{0}].CurrentLevel: {1}{2}", i,
                        coolers.Items[i].CurrentLevel, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        return r.ToString();
    }

    private void SoftwareControlValueChanged(IControl control)
    {
        var coolerLevels = new NvGpuCoolerLevels();
        coolerLevels.Version = Nvapi.GpuCoolerLevelsVer;
        coolerLevels.Levels = new NvLevel[Nvapi.MaxCoolerPerGpu];
        coolerLevels.Levels[0].Level = (int)control.SoftwareValue;
        coolerLevels.Levels[0].Policy = 1;
        Nvapi.NvApiGpuSetCoolerLevels(_handle, 0, ref coolerLevels);
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
        var coolerLevels = new NvGpuCoolerLevels();
        coolerLevels.Version = Nvapi.GpuCoolerLevelsVer;
        coolerLevels.Levels = new NvLevel[Nvapi.MaxCoolerPerGpu];
        coolerLevels.Levels[0].Policy = 0x20;
        Nvapi.NvApiGpuSetCoolerLevels(_handle, 0, ref coolerLevels);
    }

    protected override void Dispose(bool disposing)
    {
        if (_fanControl != null)
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
