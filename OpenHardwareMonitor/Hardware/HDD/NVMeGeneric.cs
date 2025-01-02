/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2017 Alexander Thulcke <alexth4ef9@gmail.com>

*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware.HDD;

internal sealed class NvMeGeneric : AbstractStorage
{
    private delegate float GetSensorValue(NvMeHealthInfo health);

    private class NvMeSensor : Sensor
    {
        private readonly GetSensorValue _getValue;

        public NvMeSensor(string name, int index, bool defaultHidden,
            SensorType sensorType, Hardware hardware, ISettings settings,
            GetSensorValue getValue)
            : base(name, index, defaultHidden, sensorType, hardware, null, settings)
        {
            this._getValue = getValue;
        }

        public void Update(NvMeHealthInfo health)
        {
            Value = _getValue(health);
        }
    }

    private const int MaxDrives = 32;

    private readonly WindowsNvMeSmart _smart;
    private readonly NvMeInfo _info;
    private List<NvMeSensor> _sensors = new();

    private NvMeGeneric(string name, NvMeInfo info, int index, ISettings settings)
        : base(name, info.Revision, "nvme", index, settings)
    {
        _smart = new WindowsNvMeSmart(info.Index);
        this._info = info;
        CreateSensors();
    }

    // \\.\ScsiY: is different from \\.\PhysicalDriveX
    // We need to find the NvmeInfo that matches the drive we search.
    private static NvMeInfo GetDeviceInfo(StorageInfo infoToMatch, int previousDrive)
    {
        for (var nextDrive = previousDrive + 1; nextDrive <= MaxDrives; nextDrive++)
            using (var smart = new WindowsNvMeSmart(nextDrive))
            {
                if (!smart.IsValid)
                    continue;
                var info = smart.GetInfo(infoToMatch, nextDrive, false);
                if (info != null)
                    return info;
            }

        // This is a bit tricky. If this fails for one drive, it will fail for all, because we start with 0 each time.
        // if we failed to obtain any useful information for any drive letter, we force creation - this will later try to use the alternate approach
        // when reading NVMe data. As we know it's an NVME drive
        for (var nextDrive = previousDrive + 1; nextDrive <= MaxDrives; nextDrive++)
            using (var smart = new WindowsNvMeSmart(nextDrive))
            {
                if (!smart.IsValid)
                    continue;
                // this one is completely unusable. The device seems to require yet another api.
                if (smart.GetHealthInfo() == null) continue;
                var info = smart.GetInfo(infoToMatch, nextDrive, true);
                if (info != null)
                    return info;
            }

        return null;
    }

    public static AbstractStorage CreateInstance(StorageInfo storageInfo, NvMeGeneric previousNvme, ISettings settings)
    {
        var nvmeInfo = GetDeviceInfo(storageInfo, previousNvme != null ? previousNvme._info.LogicalDeviceNumber : -1);
        if (nvmeInfo == null)
        {
            Logging.LogInfo(
                $"Device {storageInfo.Index} ({storageInfo.Name}) identifies as NVMe device, but does not support all requires features.");
            return null;
        }

        IEnumerable<string> logicalDrives = WindowsStorage.GetLogicalDrives(storageInfo.Index);
        var name = nvmeInfo.Model;

        if (logicalDrives.Any())
        {
            logicalDrives = logicalDrives.Select(x => $"{x}:");
            name += " (" + string.Join(", ", logicalDrives) + ")";
        }

        return new NvMeGeneric(name, nvmeInfo, storageInfo.Index, settings);
    }

    protected override void CreateSensors()
    {
        AddSensor("Temperature", 0, false, SensorType.Temperature, (health) => health.Temperature);
        AddSensor("Available Spare", 0, false, SensorType.Level, (health) => health.AvailableSpare);
        AddSensor("Available Spare Threshold", 1, false, SensorType.Level, (health) => health.AvailableSpareThreshold);
        AddSensor("Percentage Used", 2, false, SensorType.Level, (health) => health.PercentageUsed);
        AddSensor("Data Read", 1, false, SensorType.Data, (health) => UnitsToData(health.DataUnitRead));
        AddSensor("Data Written", 2, false, SensorType.Data, (health) => UnitsToData(health.DataUnitWritten));
        var log = _smart.GetHealthInfo();
        for (var i = 0; i < log.TemperatureSensors.Length; i++)
            if (log.TemperatureSensors[i] > short.MinValue)
            {
                var idx = 0;
                AddSensor("Temperature", i + 1, true, SensorType.Temperature,
                    (health) => health.TemperatureSensors[idx]);
            }

        var idx1 = 0;
        AddSensor("Power-On Hours (POH)", idx1++, false, SensorType.RawValue, (health) => health.PowerOnHours);
        AddSensor("Media Errors", idx1++, true, SensorType.RawValue, (health) => health.MediaErrors);
        // What is this?
        // AddSensor("Controller busy time", 0, true, SensorType.TimeSpan, (health) => health.ControllerBusyTime);

        base.CreateSensors();
    }

    private void AddSensor(string name, int index, bool defaultHidden,
        SensorType sensorType, GetSensorValue getValue)
    {
        var sensor = new NvMeSensor(name, index, defaultHidden, sensorType, this, Settings, getValue);
        ActivateSensor(sensor);
        _sensors.Add(sensor);
    }

    private static readonly ulong Units = 512;
    private static readonly ulong Scale = 1000000;

    private static float UnitsToData(ulong u)
    {
        // one unit is 512 * 1000 bytes, return in GB (not GiB)
        return Units * u / Scale;
    }

    protected override void UpdateSensors()
    {
        var health = _smart.GetHealthInfo();
        // This may sometimes be null after recovering from sleep/hybernate
        if (health != null)
            foreach (var sensor in _sensors)
                sensor.Update(health);

        base.UpdateSensors();
    }

    protected override void GetReport(StringBuilder r)
    {
        r.AppendLine("PCI Vendor ID: 0x" + _info.Vid.ToString("x04"));
        if (_info.Vid != _info.Ssvid)
            r.AppendLine("PCI Subsystem Vendor ID: 0x" + _info.Vid.ToString("x04"));
        if (_info.Ieee != null)
            r.AppendLine("IEEE OUI Identifier: 0x" + _info.Ieee[2].ToString("x02") + _info.Ieee[1].ToString("x02") +
                         _info.Ieee[0].ToString("x02"));
        r.AppendLine("Total NVM Capacity: " + _info.TotalCapacity);
        r.AppendLine("Unallocated NVM Capacity: " + _info.UnallocatedCapacity);
        r.AppendLine("Controller ID: " + _info.ControllerId);
        r.AppendLine("Number of Namespaces: " + _info.NumberNamespaces);
        if (_info.Namespace1 != null)
        {
            r.AppendLine("Namespace 1 Size: " + _info.Namespace1.Size);
            r.AppendLine("Namespace 1 Capacity: " + _info.Namespace1.Capacity);
            r.AppendLine("Namespace 1 Utilization: " + _info.Namespace1.Utilization);
            r.AppendLine("Namespace 1 LBA Data Size: " + _info.Namespace1.LbaDataSize);
        }

        var health = _smart.GetHealthInfo();
        if (health.CriticalWarning == NvMeCriticalWarning.None)
        {
            r.AppendLine("Critical Warning: -");
        }
        else
        {
            if ((health.CriticalWarning & NvMeCriticalWarning.AvailableSpaceLow) != 0)
                r.AppendLine("Critical Warning: the available spare space has fallen below the threshold.");
            if ((health.CriticalWarning & NvMeCriticalWarning.TemperatureThreshold) != 0)
                r.AppendLine(
                    "Critical Warning: a temperature is above an over temperature threshold or below an under temperature threshold.");
            if ((health.CriticalWarning & NvMeCriticalWarning.ReliabilityDegraded) != 0)
                r.AppendLine(
                    "Critical Warning: the device reliability has been degraded due to significant media related errors or any internal error that degrades device reliability.");
            if ((health.CriticalWarning & NvMeCriticalWarning.ReadOnly) != 0)
                r.AppendLine("Critical Warning: the media has been placed in read only mode.");
            if ((health.CriticalWarning & NvMeCriticalWarning.VolatileMemoryBackupDeviceFailed) != 0)
                r.AppendLine("Critical Warning: the volatile memory backup device has failed.");
        }

        r.AppendLine("Temperature: " + health.Temperature + " Celsius");
        r.AppendLine("Available Spare: " + health.AvailableSpare + "%");
        r.AppendLine("Available Spare Threshold: " + health.AvailableSpareThreshold + "%");
        r.AppendLine("Data Units Read: " + health.DataUnitRead);
        r.AppendLine("Data Units Written: " + health.DataUnitWritten);
        r.AppendLine("Host Read Commands: " + health.HostReadCommands);
        r.AppendLine("Data Write Commands: " + health.HostWriteCommands);
        r.AppendLine("Controller Busy Time: " + health.ControllerBusyTime);
        r.AppendLine("Power Cycles: " + health.PowerCycle);
        r.AppendLine("Power On Hours: " + health.PowerOnHours);
        r.AppendLine("Unsafe Shutdowns: " + health.UnsafeShutdowns);
        r.AppendLine("Media Errors: " + health.MediaErrors);
        r.AppendLine("Number of Error Information Log Entries: " + health.ErrorInfoLogEntryCount);
        r.AppendLine("Warning Composite Temperature Time: " + health.WarningCompositeTemperatureTime);
        r.AppendLine("Critical Composite Temperature Time: " + health.CriticalCompositeTemperatureTime);
        for (var i = 0; i < health.TemperatureSensors.Length; i++)
            if (health.TemperatureSensors[i] > short.MinValue)
                r.AppendLine("Temperature Sensor " + (i + 1) + ": " + health.TemperatureSensors[i] + " Celsius");
    }

    protected override void Dispose(bool disposing)
    {
        _smart.Dispose();

        base.Dispose(disposing);
    }
}
