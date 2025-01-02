/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.TBalancer;

internal class Balancer : Hardware
{
    private readonly int _portIndex;
    private readonly byte _protocolVersion;
    private readonly Sensor[] _digitalTemperatures = new Sensor[8];
    private readonly Sensor[] _analogTemperatures = new Sensor[4];
    private readonly Sensor[] _sensorhubTemperatures = new Sensor[6];
    private readonly Sensor[] _sensorhubFlows = new Sensor[2];
    private readonly Sensor[] _fans = new Sensor[4];
    private readonly Sensor[] _controls = new Sensor[4];
    private readonly Sensor[] _miniNgTemperatures = new Sensor[4];
    private readonly Sensor[] _miniNgFans = new Sensor[4];
    private readonly Sensor[] _miniNgControls = new Sensor[4];
    private readonly List<ISensor> _deactivating = new();

    private FtHandle _handle;
    private byte[] _data = new byte[285];
    private byte[] _primaryData = new byte[0];
    private byte[] _alternativeData = new byte[0];

    public const byte Startflag = 100;
    public const byte Endflag = 254;

    private delegate void MethodDelegate();

    private readonly MethodDelegate _alternativeRequest;

    public Balancer(int portIndex, byte protocolVersion, ISettings settings)
        : base("T-Balancer bigNG", new Identifier("bigng",
            portIndex.ToString(CultureInfo.InvariantCulture)), settings)
    {
        this._portIndex = portIndex;
        this._protocolVersion = protocolVersion;

        var parameter = new[]
        {
            new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
        };
        var offset = 0;
        for (var i = 0; i < _digitalTemperatures.Length; i++)
            _digitalTemperatures[i] = new Sensor("Digital Sensor " + i,
                offset + i, SensorType.Temperature, this, parameter, settings);
        offset += _digitalTemperatures.Length;

        for (var i = 0; i < _analogTemperatures.Length; i++)
            _analogTemperatures[i] = new Sensor("Analog Sensor " + (i + 1),
                offset + i, SensorType.Temperature, this, parameter, settings);
        offset += _analogTemperatures.Length;

        for (var i = 0; i < _sensorhubTemperatures.Length; i++)
            _sensorhubTemperatures[i] = new Sensor("Sensorhub Sensor " + i,
                offset + i, SensorType.Temperature, this, parameter, settings);
        offset += _sensorhubTemperatures.Length;

        for (var i = 0; i < _miniNgTemperatures.Length; i++)
            _miniNgTemperatures[i] = new Sensor("miniNG #" + (i / 2 + 1) +
                                               " Sensor " + (i % 2 + 1), offset + i, SensorType.Temperature,
                this, parameter, settings);
        offset += _miniNgTemperatures.Length;

        for (var i = 0; i < _sensorhubFlows.Length; i++)
            _sensorhubFlows[i] = new Sensor("Flowmeter " + (i + 1),
                i, SensorType.Flow, this, new[]
                {
                    new ParameterDescription("Impulse Rate",
                        "The impulse rate of the flowmeter in pulses/L", 509)
                }, settings);

        for (var i = 0; i < _controls.Length; i++)
            _controls[i] = new Sensor("Fan Channel " + i, i, SensorType.Control,
                this, settings);

        for (var i = 0; i < _miniNgControls.Length; i++)
            _miniNgControls[i] = new Sensor("miniNG #" + (i / 2 + 1) +
                                           " Fan Channel " + (i % 2 + 1), 4 + i, SensorType.Control, this,
                settings);

        _alternativeRequest = new MethodDelegate(DelayedAlternativeRequest);

        Open();
        Update();
    }

    protected override void ActivateSensor(ISensor sensor)
    {
        _deactivating.Remove(sensor);
        base.ActivateSensor(sensor);
    }

    protected override void DeactivateSensor(ISensor sensor)
    {
        if (_deactivating.Contains(sensor))
        {
            _deactivating.Remove(sensor);
            base.DeactivateSensor(sensor);
        }
        else if (Active.Contains(sensor))
        {
            _deactivating.Add(sensor);
        }
    }

    private void ReadminiNg(int number)
    {
        var offset = 1 + number * 65;

        if (_data[offset + 61] != Endflag)
            return;

        for (var i = 0; i < 2; i++)
        {
            var sensor = _miniNgTemperatures[number * 2 + i];
            if (_data[offset + 7 + i] > 0)
            {
                sensor.Value = 0.5f * _data[offset + 7 + i] +
                               sensor.Parameters[0].Value;
                ActivateSensor(sensor);
            }
            else
            {
                DeactivateSensor(sensor);
            }
        }

        for (var i = 0; i < 2; i++)
        {
            if (_miniNgFans[number * 2 + i] == null)
                _miniNgFans[number * 2 + i] =
                    new Sensor("miniNG #" + (number + 1) + " Fan Channel " + (i + 1),
                        4 + number * 2 + i, SensorType.Fan, this, Settings);

            var sensor = _miniNgFans[number * 2 + i];

            sensor.Value = 20.0f * _data[offset + 43 + 2 * i];
            ActivateSensor(sensor);
        }

        for (var i = 0; i < 2; i++)
        {
            var sensor = _miniNgControls[number * 2 + i];
            sensor.Value = _data[offset + 15 + i];
            ActivateSensor(sensor);
        }
    }

    private void ReadData()
    {
        Ftd2Xx.Read(_handle, _data);

        if (_data[0] != Startflag)
        {
            Ftd2Xx.FtPurge(_handle, FtPurge.FT_PURGE_RX);
            return;
        }

        if (_data[1] == 255 || _data[1] == 88)
        {
            // bigNG

            if (_data[274] != _protocolVersion)
                return;

            if (_primaryData.Length == 0)
                _primaryData = new byte[_data.Length];
            _data.CopyTo(_primaryData, 0);

            for (var i = 0; i < _digitalTemperatures.Length; i++)
                if (_data[238 + i] > 0)
                {
                    _digitalTemperatures[i].Value = 0.5f * _data[238 + i] +
                                                   _digitalTemperatures[i].Parameters[0].Value;
                    ActivateSensor(_digitalTemperatures[i]);
                }
                else
                {
                    DeactivateSensor(_digitalTemperatures[i]);
                }

            for (var i = 0; i < _analogTemperatures.Length; i++)
                if (_data[260 + i] > 0)
                {
                    _analogTemperatures[i].Value = 0.5f * _data[260 + i] +
                                                  _analogTemperatures[i].Parameters[0].Value;
                    ActivateSensor(_analogTemperatures[i]);
                }
                else
                {
                    DeactivateSensor(_analogTemperatures[i]);
                }

            for (var i = 0; i < _sensorhubTemperatures.Length; i++)
                if (_data[246 + i] > 0)
                {
                    _sensorhubTemperatures[i].Value = 0.5f * _data[246 + i] +
                                                     _sensorhubTemperatures[i].Parameters[0].Value;
                    ActivateSensor(_sensorhubTemperatures[i]);
                }
                else
                {
                    DeactivateSensor(_sensorhubTemperatures[i]);
                }

            for (var i = 0; i < _sensorhubFlows.Length; i++)
                if (_data[231 + i] > 0 && _data[234] > 0)
                {
                    var pulsesPerSecond = _data[231 + i] * 4.0f / _data[234];
                    var pulsesPerLiter = _sensorhubFlows[i].Parameters[0].Value;
                    _sensorhubFlows[i].Value = pulsesPerSecond * 3600 / pulsesPerLiter;
                    ActivateSensor(_sensorhubFlows[i]);
                }
                else
                {
                    DeactivateSensor(_sensorhubFlows[i]);
                }

            for (var i = 0; i < _fans.Length; i++)
            {
                var maxRpm = 11.5f * ((_data[149 + 2 * i] << 8) | _data[148 + 2 * i]);

                if (_fans[i] == null)
                    _fans[i] = new Sensor("Fan Channel " + i, i, SensorType.Fan,
                        this, new[]
                        {
                            new ParameterDescription("MaxRPM",
                                "Maximum revolutions per minute (RPM) of the fan.", maxRpm)
                        }, Settings);

                float value;
                if ((_data[136] & (1 << i)) == 0) // pwm mode
                    value = 0.02f * _data[137 + i];
                else // analog mode
                    value = 0.01f * _data[141 + i];

                _fans[i].Value = _fans[i].Parameters[0].Value * value;
                ActivateSensor(_fans[i]);

                _controls[i].Value = 100 * value;
                ActivateSensor(_controls[i]);
            }
        }
        else if (_data[1] == 253)
        {
            // miniNG #1
            if (_alternativeData.Length == 0)
                _alternativeData = new byte[_data.Length];
            _data.CopyTo(_alternativeData, 0);

            ReadminiNg(0);

            if (_data[66] == 253) // miniNG #2
                ReadminiNg(1);
        }
    }

    public override HardwareType HardwareType => HardwareType.Balancer;

    public override string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("T-Balancer bigNG");
        r.AppendLine();
        r.Append("Port Index: ");
        r.AppendLine(_portIndex.ToString(CultureInfo.InvariantCulture));
        r.AppendLine();

        r.AppendLine("Primary System Information Answer");
        r.AppendLine();
        r.AppendLine("       00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        r.AppendLine();
        for (var i = 0; i <= 0x11; i++)
        {
            r.Append(" ");
            r.Append((i << 4).ToString("X3", CultureInfo.InvariantCulture));
            r.Append("  ");
            for (var j = 0; j <= 0xF; j++)
            {
                var index = (i << 4) | j;
                if (index < _primaryData.Length)
                {
                    r.Append(" ");
                    r.Append(_primaryData[index].ToString("X2", CultureInfo.InvariantCulture));
                }
            }

            r.AppendLine();
        }

        r.AppendLine();

        if (_alternativeData.Length > 0)
        {
            r.AppendLine("Alternative System Information Answer");
            r.AppendLine();
            r.AppendLine("       00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
            r.AppendLine();
            for (var i = 0; i <= 0x11; i++)
            {
                r.Append(" ");
                r.Append((i << 4).ToString("X3", CultureInfo.InvariantCulture));
                r.Append("  ");
                for (var j = 0; j <= 0xF; j++)
                {
                    var index = (i << 4) | j;
                    if (index < _alternativeData.Length)
                    {
                        r.Append(" ");
                        r.Append(_alternativeData[index].ToString("X2", CultureInfo.InvariantCulture));
                    }
                }

                r.AppendLine();
            }

            r.AppendLine();
        }

        return r.ToString();
    }

    private void DelayedAlternativeRequest()
    {
        System.Threading.Thread.Sleep(500);
        Ftd2Xx.Write(_handle, new byte[] { 0x37 });
    }

    public void Open()
    {
        Ftd2Xx.FtOpen(_portIndex, out _handle);
        Ftd2Xx.FtSetBaudRate(_handle, 19200);
        Ftd2Xx.FtSetDataCharacteristics(_handle, 8, 1, 0);
        Ftd2Xx.FtSetFlowControl(_handle, FtFlowControl.FT_FLOW_RTS_CTS, 0x11,
            0x13);
        Ftd2Xx.FtSetTimeouts(_handle, 1000, 1000);
        Ftd2Xx.FtPurge(_handle, FtPurge.FT_PURGE_ALL);
    }

    public override void Update()
    {
        while (Ftd2Xx.BytesToRead(_handle) >= 285)
            ReadData();
        if (Ftd2Xx.BytesToRead(_handle) == 1)
            Ftd2Xx.ReadByte(_handle);

        Ftd2Xx.Write(_handle, new byte[] { 0x38 });
        _alternativeRequest.BeginInvoke(null, null);
    }

    protected override void Dispose(bool disposing)
    {
        Ftd2Xx.FtClose(_handle);
        base.Dispose(disposing);
    }
}
