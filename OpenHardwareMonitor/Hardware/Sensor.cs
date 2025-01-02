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
using System.IO.Compression;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware;

internal class Sensor : ISensor
{
    private readonly string _defaultName;
    private string _name;
    private readonly int _index;
    private readonly bool _defaultHidden;
    private readonly SensorType _sensorType;
    private readonly Hardware _hardware;
    private readonly ReadOnlyArray<IParameter> _parameters;
    private double? _currentValue;
    private double? _minValue;
    private double? _maxValue;

    private readonly RingCollection<SensorValue>
        _values = new();

    private readonly ISettings _settings;
    private IControl _control;

    private double _sum;
    private int _count;

    public Sensor(string name, int index, SensorType sensorType,
        Hardware hardware, ISettings settings) :
        this(name, index, sensorType, hardware, null, settings)
    {
    }

    public Sensor(string name, int index, SensorType sensorType,
        Hardware hardware, ParameterDescription[] parameterDescriptions,
        ISettings settings) :
        this(name, index, false, sensorType, hardware,
            parameterDescriptions, settings)
    {
    }

    public Sensor(string name, int index, bool defaultHidden,
        SensorType sensorType, Hardware hardware,
        ParameterDescription[] parameterDescriptions, ISettings settings)
    {
        this._index = index;
        this._defaultHidden = defaultHidden;
        this._sensorType = sensorType;
        this._hardware = hardware;
        var parameters = new Parameter[parameterDescriptions == null ? 0 : parameterDescriptions.Length];
        for (var i = 0; i < parameters.Length; i++)
            parameters[i] = new Parameter(parameterDescriptions[i], this, settings);
        this._parameters = parameters;

        this._settings = settings;
        _defaultName = name;
        this._name = settings.GetValue(
            new Identifier(Identifier, "name").ToString(), name);

        GetSensorValuesFromSettings();

        hardware.Closing += delegate(IHardware h) { SetSensorValuesToSettings(); };
    }

    private void SetSensorValuesToSettings()
    {
        if (_values.Count == 0) return;
        using (var m = new MemoryStream())
        {
            using (var c = new GZipStream(m, CompressionMode.Compress))
            using (var b = new BufferedStream(c, 65536))
            using (var writer = new BinaryWriter(b))
            {
                long t = 0;
                foreach (var sensorValue in _values)
                {
                    var v = sensorValue.Time.ToBinary();
                    writer.Write(v - t);
                    t = v;
                    writer.Write(sensorValue.Value);
                }

                writer.Flush();
            }

            _settings.SetValue(new Identifier(Identifier, "values").ToString(),
                Convert.ToBase64String(m.ToArray()));
        }
    }

    private void GetSensorValuesFromSettings()
    {
        var name = new Identifier(Identifier, "values").ToString();
        var s = _settings.GetValue(name, null);

        if (s == null)
        {
            _settings.Remove(name);
            return;
        }

        var array = Convert.FromBase64String(s);
        var now = DateTime.UtcNow;
        using (var m = new MemoryStream(array))
        using (var c = new GZipStream(m, CompressionMode.Decompress))
        using (var reader = new BinaryReader(c))
        {
            long t = 0;
            while (reader.PeekChar() != -1)
            {
                t += reader.ReadInt64();
                var time = DateTime.FromBinary(t);
                if (time > now)
                    break;
                var value = reader.ReadSingle();
                AppendValue(value, time);
            }
        }

        if (_values.Count > 0)
            AppendValue(float.NaN, DateTime.UtcNow);

        // remove the value string from the settings to reduce memory usage
        _settings.Remove(name);
    }

    private void AppendValue(double value, DateTime time)
    {
        if (_values.Count >= 2 && _values.Last.Value == value &&
            _values[_values.Count - 2].Value == value)
        {
            _values.Last = new SensorValue(value, time);
            return;
        }

        _values.Append(new SensorValue(value, time));
    }

    public IHardware Hardware => _hardware;

    public SensorType SensorType => _sensorType;

    public Identifier Identifier =>
        new(_hardware.Identifier,
            _sensorType.ToString().ToLowerInvariant(),
            _index.ToString(CultureInfo.InvariantCulture));

    public string Name
    {
        get => _name;
        set
        {
            if (!string.IsNullOrEmpty(value))
                _name = value;
            else
                _name = _defaultName;
            _settings.SetValue(new Identifier(Identifier, "name").ToString(), _name);
        }
    }

    public int Index => _index;

    public bool IsDefaultHidden => _defaultHidden;

    public IReadOnlyArray<IParameter> Parameters => _parameters;

    public double? Value
    {
        get => _currentValue;
        set
        {
            var now = DateTime.UtcNow;
            while (_values.Count > 0 && (now - _values.First.Time).TotalDays > 1)
                _values.Remove();

            if (value.HasValue)
            {
                _sum += value.Value;
                _count++;
                if (_count == 4)
                {
                    AppendValue(_sum / _count, now);
                    _sum = 0;
                    _count = 0;
                }
            }

            _currentValue = value;
            if (_minValue > value || !_minValue.HasValue)
                _minValue = value;
            if (_maxValue < value || !_maxValue.HasValue)
                _maxValue = value;
        }
    }

    public double? Min => _minValue;
    public double? Max => _maxValue;

    public void ResetMin()
    {
        _minValue = null;
    }

    public void ResetMax()
    {
        _maxValue = null;
    }

    public IEnumerable<SensorValue> Values => _values;

    public void Accept(IVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException("visitor");
        visitor.VisitSensor(this);
    }

    public void Traverse(IVisitor visitor)
    {
        foreach (var parameter in _parameters)
            parameter.Accept(visitor);
    }

    public IControl Control
    {
        get => _control;
        internal set => _control = value;
    }
}
