/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Reflection;
using LibreHardwareMonitor.Hardware.Network;

namespace OpenHardwareMonitor.Hardware;

public class Computer : IComputer
{
    private readonly List<IGroup> _groups = new();
    private readonly ISettings _settings;

    private Smbios _smbios;

    private bool _open;

    private bool _mainboardEnabled;
    private bool _cpuEnabled;
    private bool _ramEnabled;
    private bool _gpuEnabled;
    private bool _fanControllerEnabled;
    private bool _hddEnabled;
    private bool _networkEnabled;

    public Computer()
    {
        _settings = new Settings();
    }

    public Computer(ISettings settings)
    {
        this._settings = settings ?? new Settings();
    }

    private void Add(IGroup group)
    {
        if (_groups.Contains(group))
            return;

        _groups.Add(group);

        if (HardwareAdded != null)
            foreach (var hardware in group.Hardware)
                HardwareAdded(hardware);
    }

    private void Remove(IGroup group)
    {
        if (!_groups.Contains(group))
            return;

        _groups.Remove(group);

        if (HardwareRemoved != null)
        {
            var listCopy = group.Hardware.ToList(); // Make below enumeration thread safe
            foreach (var hardware in listCopy)
            {
                HardwareRemoved(hardware);
                hardware.Dispose();
            }
        }

        group.Close();
    }

    private void RemoveType<T>() where T : IGroup
    {
        var list = new List<IGroup>();
        foreach (var group in _groups)
            if (group is T)
                list.Add(group);

        foreach (var group in list) Remove(group);
    }

    public void Open()
    {
        if (_open)
            return;

        _smbios = new Smbios();

        Ring0.Open();
        Opcode.Open();

        AddGroups();

        _open = true;
    }

    private void AddGroups()
    {
        if (_mainboardEnabled)
            Add(new Mainboard.MainboardGroup(_smbios, _settings));

        if (_cpuEnabled)
            Add(new CPU.CpuGroup(_settings));

        if (_ramEnabled)
            Add(new RAM.RamGroup(_smbios, _settings));

        if (_gpuEnabled)
        {
            Add(new ATI.AtiGroup(_settings));
            Add(new Nvidia.NvidiaGroup(_settings));
        }

        if (_fanControllerEnabled)
        {
            Add(new TBalancer.BalancerGroup(_settings));
            Add(new Heatmaster.HeatmasterGroup(_settings));
        }

        if (_networkEnabled) Add(new NetworkGroup(_settings));

        if (_hddEnabled)
            Add(new HDD.HarddriveGroup(_settings));
    }

    public void Reset()
    {
        if (!_open)
            return;

        RemoveGroups();
        AddGroups();
    }

    public bool MainboardEnabled
    {
        get => _mainboardEnabled;

        set
        {
            if (_open && value != _mainboardEnabled)
            {
                if (value)
                    Add(new Mainboard.MainboardGroup(_smbios, _settings));
                else
                    RemoveType<Mainboard.MainboardGroup>();
            }

            _mainboardEnabled = value;
        }
    }

    public bool CpuEnabled
    {
        get => _cpuEnabled;

        set
        {
            if (_open && value != _cpuEnabled)
            {
                if (value)
                    Add(new CPU.CpuGroup(_settings));
                else
                    RemoveType<CPU.CpuGroup>();
            }

            _cpuEnabled = value;
        }
    }

    public bool RamEnabled
    {
        get => _ramEnabled;

        set
        {
            if (_open && value != _ramEnabled)
            {
                if (value)
                    Add(new RAM.RamGroup(_smbios, _settings));
                else
                    RemoveType<RAM.RamGroup>();
            }

            _ramEnabled = value;
        }
    }

    public bool GpuEnabled
    {
        get => _gpuEnabled;

        set
        {
            if (_open && value != _gpuEnabled)
            {
                if (value)
                {
                    Add(new ATI.AtiGroup(_settings));
                    Add(new Nvidia.NvidiaGroup(_settings));
                }
                else
                {
                    RemoveType<ATI.AtiGroup>();
                    RemoveType<Nvidia.NvidiaGroup>();
                }
            }

            _gpuEnabled = value;
        }
    }

    public bool FanControllerEnabled
    {
        get => _fanControllerEnabled;

        set
        {
            if (_open && value != _fanControllerEnabled)
            {
                if (value)
                {
                    Add(new TBalancer.BalancerGroup(_settings));
                    Add(new Heatmaster.HeatmasterGroup(_settings));
                }
                else
                {
                    RemoveType<TBalancer.BalancerGroup>();
                    RemoveType<Heatmaster.HeatmasterGroup>();
                }
            }

            _fanControllerEnabled = value;
        }
    }

    public bool HddEnabled
    {
        get => _hddEnabled;

        set
        {
            if (_open && value != _hddEnabled)
            {
                if (value)
                    Add(new HDD.HarddriveGroup(_settings));
                else
                    RemoveType<HDD.HarddriveGroup>();
            }

            _hddEnabled = value;
        }
    }

    public bool NetworkEnabled
    {
        get => _networkEnabled;
        set
        {
            if (_open && value != _networkEnabled)
            {
                if (value)
                    Add(new NetworkGroup(_settings));
                else
                    RemoveType<NetworkGroup>();
            }

            _networkEnabled = value;
        }
    }

    public IHardware[] Hardware
    {
        get
        {
            var list = new List<IHardware>();
            foreach (var group in _groups)
            foreach (var hardware in group.Hardware)
                list.Add(hardware);
            return list.ToArray();
        }
    }

    private static void NewSection(TextWriter writer)
    {
        for (var i = 0; i < 8; i++)
            writer.Write("----------");
        writer.WriteLine();
        writer.WriteLine();
    }

    private static int CompareSensor(ISensor a, ISensor b)
    {
        var c = a.SensorType.CompareTo(b.SensorType);
        if (c == 0)
            return a.Index.CompareTo(b.Index);
        else
            return c;
    }

    private static void ReportHardwareSensorTree(
        IHardware hardware, TextWriter w, string space)
    {
        w.WriteLine("{0}|", space);
        w.WriteLine("{0}+- {1} ({2})",
            space, hardware.Name, hardware.Identifier);
        var sensors = hardware.Sensors;
        Array.Sort(sensors, CompareSensor);
        foreach (var sensor in sensors)
            w.WriteLine("{0}|  +- {1,-14} : {2,8:G6} {3,8:G6} {4,8:G6} ({5})",
                space, sensor.Name, sensor.Value, sensor.Min, sensor.Max,
                sensor.Identifier);
        foreach (var subHardware in hardware.SubHardware)
            ReportHardwareSensorTree(subHardware, w, "|  ");
    }

    private static void ReportHardwareParameterTree(
        IHardware hardware, TextWriter w, string space)
    {
        w.WriteLine("{0}|", space);
        w.WriteLine("{0}+- {1} ({2})",
            space, hardware.Name, hardware.Identifier);
        var sensors = hardware.Sensors;
        Array.Sort(sensors, CompareSensor);
        foreach (var sensor in sensors)
        {
            var innerSpace = space + "|  ";
            if (sensor.Parameters.Length > 0)
            {
                w.WriteLine("{0}|", innerSpace);
                w.WriteLine("{0}+- {1} ({2})",
                    innerSpace, sensor.Name, sensor.Identifier);
                foreach (var parameter in sensor.Parameters)
                {
                    var innerInnerSpace = innerSpace + "|  ";
                    w.WriteLine("{0}+- {1} : {2}",
                        innerInnerSpace, parameter.Name,
                        string.Format(CultureInfo.InvariantCulture, "{0} : {1}",
                            parameter.DefaultValue, parameter.Value));
                }
            }
        }

        foreach (var subHardware in hardware.SubHardware)
            ReportHardwareParameterTree(subHardware, w, "|  ");
    }

    private static void ReportHardware(IHardware hardware, TextWriter w)
    {
        var hardwareReport = hardware.GetReport();
        if (!string.IsNullOrEmpty(hardwareReport))
        {
            NewSection(w);
            w.Write(hardwareReport);
        }

        foreach (var subHardware in hardware.SubHardware)
            ReportHardware(subHardware, w);
    }

    public string GetReport()
    {
        using (var w = new StringWriter(CultureInfo.InvariantCulture))
        {
            w.WriteLine();
            w.WriteLine("Open Hardware Monitor Report");
            w.WriteLine();

            var version = typeof(Computer).Assembly.GetName().Version;

            NewSection(w);
            w.Write("Version: ");
            w.WriteLine(version.ToString());
            w.WriteLine();

            NewSection(w);
            w.Write("Common Language Runtime: ");
            w.WriteLine(Environment.Version.ToString());
            w.Write("Operating System: ");
            w.WriteLine(Environment.OSVersion.ToString());
            w.Write("Process Type: ");
            w.WriteLine(IntPtr.Size == 4 ? "32-Bit" : "64-Bit");
            w.WriteLine();

            var r = Ring0.GetReport();
            if (r != null)
            {
                NewSection(w);
                w.Write(r);
                w.WriteLine();
            }

            NewSection(w);
            w.WriteLine("Sensors");
            w.WriteLine();
            foreach (var group in _groups)
            foreach (var hardware in group.Hardware)
                ReportHardwareSensorTree(hardware, w, "");
            w.WriteLine();

            NewSection(w);
            w.WriteLine("Parameters");
            w.WriteLine();
            foreach (var group in _groups)
            foreach (var hardware in group.Hardware)
                ReportHardwareParameterTree(hardware, w, "");
            w.WriteLine();

            foreach (var group in _groups)
            {
                var report = group.GetReport();
                if (!string.IsNullOrEmpty(report))
                {
                    NewSection(w);
                    w.Write(report);
                }

                var hardwareArray = group.Hardware;
                foreach (var hardware in hardwareArray)
                    ReportHardware(hardware, w);
            }

            return w.ToString();
        }
    }

    public void Close()
    {
        if (!_open)
            return;

        RemoveGroups();

        Opcode.Close();
        Ring0.Close();

        _smbios = null;

        _open = false;
    }

    private void RemoveGroups()
    {
        while (_groups.Count > 0)
        {
            var group = _groups[_groups.Count - 1];
            Remove(group);
        }
    }

    public event HardwareEventHandler HardwareAdded;
    public event HardwareEventHandler HardwareRemoved;

    public void Accept(IVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException("visitor");
        visitor.VisitComputer(this);
    }

    public void Traverse(IVisitor visitor)
    {
        foreach (var group in _groups)
        foreach (var hardware in group.Hardware)
            hardware.Accept(visitor);
    }

    private class Settings : ISettings
    {
        public bool Contains(string name)
        {
            return false;
        }

        public void SetValue(string name, string value)
        {
        }

        public string GetValue(string name, string value)
        {
            return value;
        }

        public void Remove(string name)
        {
        }
    }
}
