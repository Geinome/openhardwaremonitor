/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware.HDD;

internal abstract class AbstractStorage : Hardware
{
    private const int UpdateDivider = 5; // update only every 30s
    private const double BytesToGigabytes = 1.0 / (1024 * 1024 * 1024);
    private const double BytesToMegabytes = 1.0 / (1024 * 1024);

    protected string FirmwareRevision;

    protected readonly int Index;
    private int _count;

    private DriveInfo[] _driveInfos;
    private Sensor _usageSensor;
    private List<(Sensor Sensor, double? Value)> _performanceSensors;
    private DrivePerformanceValues _lastPerformanceValues;
    private ISmart _smart;

    protected AbstractStorage(string name, string firmwareRevision,
        string id, int index, ISettings settings)
        : base(name, new Identifier(id,
            index.ToString(CultureInfo.InvariantCulture)), settings)
    {
        this.FirmwareRevision = firmwareRevision;

        this.Index = index;
        _count = 0;

        _performanceSensors = new List<(Sensor, double? value)>();
        _lastPerformanceValues = null;

        var logicalDrives = WindowsStorage.GetLogicalDrives(index);
        var driveInfoList = new List<DriveInfo>(logicalDrives.Length);
        foreach (var logicalDrive in logicalDrives)
            try
            {
                var di = new DriveInfo(logicalDrive);
                if (di.TotalSize > 0)
                    driveInfoList.Add(new DriveInfo(logicalDrive));
            }
            catch (Exception x) when (x is ArgumentException || x is IOException || x is UnauthorizedAccessException)
            {
                Logger.LogError(x, $"Unable to obtain drive info for {logicalDrive}");
            }

        _driveInfos = driveInfoList.ToArray();

        _smart = new WindowsSmart(index);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            if (_smart != null)
            {
                _smart.Dispose();
                _smart = null;
            }

        base.Dispose(disposing);
    }

    public static AbstractStorage CreateInstance(int driveNumber, NvMeGeneric previousNvMe, ISettings settings)
    {
        var info = WindowsStorage.GetStorageInfo(driveNumber);
        if (info == null)
        {
            Logging.LogInfo($"Could not retrieve storage information for drive number {driveNumber}");
            return null;
        }

        bool alsoShowRemovables;
        if (!bool.TryParse(settings.GetValue("hddMenuItemRemovable", "true"), out alsoShowRemovables))
            alsoShowRemovables = true;

        if (info.Removable && alsoShowRemovables) return null;
        AbstractStorage ret = null;
        if (info.BusType == StorageBusType.BusTypeNvme) ret = NvMeGeneric.CreateInstance(info, previousNvMe, settings);

        // If the disk uses Nvme, but does not support the required interfaces, we try Sata instead.
        if (ret == null && (info.BusType == StorageBusType.BusTypeAta || info.BusType == StorageBusType.BusTypeSata ||
                            info.BusType == StorageBusType.BusTypeNvme))
            ret = AtaStorage.CreateInstance(info, settings);

        if (ret == null) ret = StorageGeneric.CreateInstance(info, settings);

        return ret;
    }

    protected virtual void CreateSensors()
    {
        if (_driveInfos.Length > 0)
        {
            _usageSensor =
                new Sensor("Used Space", 0, SensorType.Load, this, Settings);
            ActivateSensor(_usageSensor);
        }

        var performanceValues = _smart.ReadThroughputValues();

        // Our sensor indices just need to be different from any existing sensors
        if (performanceValues != null)
        {
            var sensors = CreatePerformanceSensors(performanceValues);
            foreach (var elem in sensors) ActivateSensor(elem.Item1);

            _performanceSensors = sensors;
        }
    }

    /// <summary>
    /// This method is used both for construction as well as updating the sensors. This avoids big if's on names
    /// </summary>
    private List<(Sensor, double? value)> CreatePerformanceSensors(DrivePerformanceValues throughputValues)
    {
        var idx = Sensors.Length + 1;
        var newPerformanceSensors = new List<(Sensor, double?)>();

        TimeSpan deltaTime = default;
        if (_lastPerformanceValues != null) deltaTime = throughputValues.QueryTime - _lastPerformanceValues.QueryTime;

        var s = new Sensor("Bytes read total", idx++, SensorType.Data, this, Settings);
        double? v = throughputValues.BytesRead * BytesToGigabytes;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Bytes written total", idx++, SensorType.Data, this, Settings);
        v = throughputValues.BytesWritten * BytesToGigabytes;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Read time total", idx++, SensorType.TimeSpan, this, Settings);
        v = throughputValues.ReadTime.TotalSeconds;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Write time total", idx++, SensorType.TimeSpan, this, Settings);
        v = throughputValues.WriteTime.TotalSeconds;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Idle time total", idx++, SensorType.TimeSpan, this, Settings);
        v = throughputValues.IdleTime.TotalSeconds;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Read active time", idx++, SensorType.Load, this, Settings);
        if (_lastPerformanceValues != null)
        {
            var valueDelta = throughputValues.ReadTime - _lastPerformanceValues.ReadTime;
            v = valueDelta.TotalSeconds / deltaTime.TotalSeconds;
        }
        else
        {
            v = null;
        }

        newPerformanceSensors.Add((s, v));

        s = new Sensor("Write active time", idx++, SensorType.Load, this, Settings);
        if (_lastPerformanceValues != null)
        {
            var valueDelta = throughputValues.WriteTime - _lastPerformanceValues.WriteTime;
            v = valueDelta.TotalSeconds / deltaTime.TotalSeconds;
        }
        else
        {
            v = null;
        }

        newPerformanceSensors.Add((s, v));

        s = new Sensor("Job queue length", idx++, SensorType.RawValue, this, Settings);
        v = throughputValues.QueueDepth;
        newPerformanceSensors.Add((s, v));

        s = new Sensor("Read throughput", idx++, SensorType.Throughput, this, Settings);
        if (_lastPerformanceValues != null)
        {
            double valueDelta = throughputValues.BytesRead - _lastPerformanceValues.BytesRead;
            v = valueDelta * BytesToMegabytes / deltaTime.TotalSeconds;
        }
        else
        {
            v = null;
        }

        newPerformanceSensors.Add((s, v));

        s = new Sensor("Write throughput", idx++, SensorType.Throughput, this, Settings);
        if (_lastPerformanceValues != null)
        {
            double valueDelta = throughputValues.BytesWritten - _lastPerformanceValues.BytesWritten;
            v = valueDelta * BytesToMegabytes / deltaTime.TotalSeconds;
        }
        else
        {
            v = null;
        }

        newPerformanceSensors.Add((s, v));

        _lastPerformanceValues = throughputValues;

        return newPerformanceSensors;
    }

    public override HardwareType HardwareType => HardwareType.HDD;

    protected virtual void UpdateSensors()
    {
        if (_performanceSensors.Count > 0)
        {
            var newValues = _smart.ReadThroughputValues();
            if (newValues != null)
            {
                var update = CreatePerformanceSensors(newValues);
                foreach (var s in _performanceSensors)
                {
                    var found = update.Single(x => x.Item1.Name == s.Sensor.Name);
                    if (found.value.HasValue)
                        s.Sensor.Value = found.value;
                    else
                        s.Sensor.Value = null;
                }
            }
        }
    }

    public override void Update()
    {
        if (_count == 0)
        {
            UpdateSensors();

            if (_usageSensor != null)
            {
                long totalSize = 0;
                long totalFreeSpace = 0;

                for (var i = 0; i < _driveInfos.Length; i++)
                {
                    if (!_driveInfos[i].IsReady)
                        continue;
                    try
                    {
                        totalSize += _driveInfos[i].TotalSize;
                        totalFreeSpace += _driveInfos[i].TotalFreeSpace;
                    }
                    catch (Exception x) when (x is IOException || x is UnauthorizedAccessException)
                    {
                        Logger.LogError($"Unable to read drive info for volume {_driveInfos[i].Name}");
                    }
                }

                if (totalSize > 0)
                    _usageSensor.Value = 100.0f - 100.0f * totalFreeSpace / totalSize;
                else
                    _usageSensor.Value = null;
            }
        }

        _count++;
        _count %= UpdateDivider;
    }

    protected abstract void GetReport(StringBuilder r);

    public override string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine(GetType().Name);
        r.AppendLine();
        r.AppendLine("Drive name: " + Name);
        r.AppendLine("Firmware version: " + FirmwareRevision);
        r.AppendLine();

        GetReport(r);

        foreach (var di in _driveInfos)
        {
            if (!di.IsReady)
                continue;
            try
            {
                r.AppendLine("Logical drive name: " + di.Name);
                r.AppendLine("Format: " + di.DriveFormat);
                r.AppendLine("Total size: " + di.TotalSize);
                r.AppendLine("Total free space: " + di.TotalFreeSpace);
                r.AppendLine();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return r.ToString();
    }

    public override void Traverse(IVisitor visitor)
    {
        foreach (var sensor in Sensors)
            sensor.Accept(visitor);
    }
}
