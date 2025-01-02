/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2014 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;

namespace OpenHardwareMonitor.Hardware;

internal delegate void ControlEventHandler(Control control);

internal class Control : IControl
{
    private readonly Identifier _identifier;
    private readonly ISettings _settings;
    private ControlMode _mode;
    private float _softwareValue;
    private float _minSoftwareValue;
    private float _maxSoftwareValue;

    public Control(ISensor sensor, ISettings settings, float minSoftwareValue,
        float maxSoftwareValue)
    {
        _identifier = new Identifier(sensor.Identifier, "control");
        this._settings = settings;
        this._minSoftwareValue = minSoftwareValue;
        this._maxSoftwareValue = maxSoftwareValue;

        if (!float.TryParse(settings.GetValue(
                    new Identifier(_identifier, "value").ToString(), "0"),
                NumberStyles.Float, CultureInfo.InvariantCulture,
                out _softwareValue))
            _softwareValue = 0;
        int mode;
        if (!int.TryParse(settings.GetValue(
                    new Identifier(_identifier, "mode").ToString(),
                    ((int)ControlMode.Undefined).ToString(CultureInfo.InvariantCulture)),
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out mode))
            this._mode = ControlMode.Undefined;
        else
            this._mode = (ControlMode)mode;
    }

    public Identifier Identifier => _identifier;

    public ControlMode ControlMode
    {
        get => _mode;
        private set
        {
            if (_mode != value)
            {
                _mode = value;
                if (ControlModeChanged != null)
                    ControlModeChanged(this);
                _settings.SetValue(new Identifier(_identifier, "mode").ToString(),
                    ((int)_mode).ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    public float SoftwareValue
    {
        get => _softwareValue;
        private set
        {
            if (_softwareValue != value)
            {
                _softwareValue = value;
                if (SoftwareControlValueChanged != null)
                    SoftwareControlValueChanged(this);
                _settings.SetValue(new Identifier(_identifier,
                        "value").ToString(),
                    value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    public void SetDefault()
    {
        ControlMode = ControlMode.Default;
    }

    public float MinSoftwareValue => _minSoftwareValue;

    public float MaxSoftwareValue => _maxSoftwareValue;

    public void SetSoftware(float value)
    {
        ControlMode = ControlMode.Software;
        SoftwareValue = value;
    }

    internal event ControlEventHandler ControlModeChanged;
    internal event ControlEventHandler SoftwareControlValueChanged;
}
