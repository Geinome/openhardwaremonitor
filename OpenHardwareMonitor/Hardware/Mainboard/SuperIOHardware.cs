/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Threading;
using OpenHardwareMonitor.Hardware.LPC;

namespace OpenHardwareMonitor.Hardware.Mainboard;

internal sealed class SuperIoHardware : Hardware
{
    private readonly Mainboard _mainboard;
    private readonly ISuperIo _superIo;

    private readonly List<Sensor> _voltages = new();
    private readonly List<Sensor> _temperatures = new();
    private readonly List<Sensor> _fans = new();
    private readonly List<Sensor> _controls = new();

    private delegate double? ReadValueDelegate(int index);

    private delegate void UpdateDelegate();

    // delegates for mainboard specific sensor reading code
    private readonly ReadValueDelegate _readVoltage;
    private readonly ReadValueDelegate _readTemperature;
    private readonly ReadValueDelegate _readFan;
    private readonly ReadValueDelegate _readControl;

    // delegate for post update mainboard specific code
    private readonly UpdateDelegate _postUpdate;

    // mainboard specific mutex
    private readonly Mutex _mutex;

    public SuperIoHardware(Mainboard mainboard, ISuperIo superIo,
        Manufacturer manufacturer, Model model, ISettings settings)
        : base(ChipName.GetName(superIo.Chip), new Identifier("lpc",
            superIo.Chip.ToString().ToLowerInvariant()), settings)
    {
        this._mainboard = mainboard;
        this._superIo = superIo;

        IList<Voltage> v;
        IList<Temperature> t;
        IList<Fan> f;
        IList<Ctrl> c;
        GetBoardSpecificConfiguration(superIo, manufacturer, model,
            out v, out t, out f, out c,
            out _readVoltage, out _readTemperature, out _readFan, out _readControl,
            out _postUpdate, out _mutex);

        CreateVoltageSensors(superIo, settings, v);
        CreateTemperatureSensors(superIo, settings, t);
        CreateFanSensors(superIo, settings, f);
        CreateControlSensors(superIo, settings, c);
    }

    private void CreateControlSensors(ISuperIo superIo, ISettings settings,
        IList<Ctrl> c)
    {
        foreach (var ctrl in c)
        {
            var index = ctrl.Index;
            if (index < superIo.Controls.Length)
            {
                var sensor = new Sensor(ctrl.Name, index, SensorType.Control,
                    this, settings);
                var control = new Control(sensor, settings, 0, 100);
                control.ControlModeChanged += (cc) =>
                {
                    switch (cc.ControlMode)
                    {
                        case ControlMode.Undefined:
                            return;
                        case ControlMode.Default:
                            superIo.SetControl(index, null);
                            break;
                        case ControlMode.Software:
                            superIo.SetControl(index, (byte)(cc.SoftwareValue * 2.55));
                            break;
                        default:
                            return;
                    }
                };
                control.SoftwareControlValueChanged += (cc) =>
                {
                    if (cc.ControlMode == ControlMode.Software)
                        superIo.SetControl(index, (byte)(cc.SoftwareValue * 2.55));
                };

                switch (control.ControlMode)
                {
                    case ControlMode.Undefined:
                        break;
                    case ControlMode.Default:
                        superIo.SetControl(index, null);
                        break;
                    case ControlMode.Software:
                        superIo.SetControl(index, (byte)(control.SoftwareValue * 2.55));
                        break;
                    default:
                        break;
                }

                sensor.Control = control;
                _controls.Add(sensor);
                ActivateSensor(sensor);
            }
        }
    }

    private void CreateFanSensors(ISuperIo superIo, ISettings settings,
        IList<Fan> f)
    {
        foreach (var fan in f)
            if (fan.Index < superIo.Fans.Length)
            {
                var sensor = new Sensor(fan.Name, fan.Index, SensorType.Fan,
                    this, settings);
                _fans.Add(sensor);
            }
    }

    private void CreateTemperatureSensors(ISuperIo superIo, ISettings settings,
        IList<Temperature> t)
    {
        foreach (var temperature in t)
            if (temperature.Index < superIo.Temperatures.Length)
            {
                var sensor = new Sensor(temperature.Name, temperature.Index,
                    SensorType.Temperature, this, new[]
                    {
                        new ParameterDescription("Offset [°C]", "Temperature offset.", 0)
                    }, settings);
                _temperatures.Add(sensor);
            }
    }

    private void CreateVoltageSensors(ISuperIo superIo, ISettings settings,
        IList<Voltage> v)
    {
        const string formula = "Voltage = value + (value - Vf) * Ri / Rf.";
        foreach (var voltage in v)
            if (voltage.Index < superIo.Voltages.Length)
            {
                var sensor = new Sensor(voltage.Name, voltage.Index,
                    voltage.Hidden, SensorType.Voltage, this, new[]
                    {
                        new ParameterDescription("Ri [kΩ]", "Input resistance.\n" +
                                                            formula, voltage.Ri),
                        new ParameterDescription("Rf [kΩ]", "Reference resistance.\n" +
                                                            formula, voltage.Rf),
                        new ParameterDescription("Vf [V]", "Reference voltage.\n" +
                                                           formula, voltage.Vf)
                    }, settings);
                _voltages.Add(sensor);
            }
    }

    private static void GetBoardSpecificConfiguration(ISuperIo superIo,
        Manufacturer manufacturer, Model model, out IList<Voltage> v,
        out IList<Temperature> t, out IList<Fan> f, out IList<Ctrl> c,
        out ReadValueDelegate readVoltage,
        out ReadValueDelegate readTemperature,
        out ReadValueDelegate readFan,
        out ReadValueDelegate readControl,
        out UpdateDelegate postUpdate, out Mutex mutex)
    {
        readVoltage = (index) => superIo.Voltages[index];
        readTemperature = (index) => superIo.Temperatures[index];
        readFan = (index) => superIo.Fans[index];
        readControl = (index) => superIo.Controls[index];

        postUpdate = () => { };
        mutex = null;

        v = new List<Voltage>();
        t = new List<Temperature>();
        f = new List<Fan>();
        c = new List<Ctrl>();

        switch (superIo.Chip)
        {
            case Chip.It8705F:
            case Chip.It8712F:
            case Chip.It8716F:
            case Chip.It8718F:
            case Chip.It8720F:
            case Chip.It8726F:
                GetIteConfigurationsA(superIo, manufacturer, model, v, t, f, c,
                    ref readFan, ref postUpdate, ref mutex);
                break;

            case Chip.It8620E:
            case Chip.It8628E:
            case Chip.It8655E:
            case Chip.It8665E:
            case Chip.It8686E:
            case Chip.It8688E:
            case Chip.It8721F:
            case Chip.It8728F:
            case Chip.It8771E:
            case Chip.It8772E:
                GetIteConfigurationsB(superIo, manufacturer, model, v, t, f, c);
                break;

            case Chip.It879Xe:
                GetIteConfigurationsC(superIo, manufacturer, model, v, t, f, c);
                break;

            case Chip.F71858:
                v.Add(new Voltage("VCC3V", 0, 150, 150));
                v.Add(new Voltage("VSB3V", 1, 150, 150));
                v.Add(new Voltage("Battery", 2, 150, 150));
                for (var i = 0; i < superIo.Temperatures.Length; i++)
                    t.Add(new Temperature("Temperature #" + (i + 1), i));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                break;
            case Chip.F71862:
            case Chip.F71869:
            case Chip.F71869A:
            case Chip.F71882:
            case Chip.F71889Ad:
            case Chip.F71889Ed:
            case Chip.F71889F:
            case Chip.F71808E:
                GetFintekConfiguration(superIo, manufacturer, model, v, t, f, c);
                break;

            case Chip.W83627Ehf:
                GetWinbondConfigurationEhf(manufacturer, model, v, t, f);
                break;
            case Chip.W83627Dhg:
            case Chip.W83627Dhgp:
            case Chip.W83667Hg:
            case Chip.W83667Hgb:
                GetWinbondConfigurationHg(manufacturer, model, v, t, f);
                break;
            case Chip.W83627Hf:
            case Chip.W83627Thf:
            case Chip.W83687Thf:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("Voltage #3", 2, true));
                v.Add(new Voltage("AVCC", 3, 34, 51));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("5VSB", 5, 34, 51));
                v.Add(new Voltage("VBAT", 6));
                t.Add(new Temperature("CPU", 0));
                t.Add(new Temperature("Auxiliary", 1));
                t.Add(new Temperature("System", 2));
                f.Add(new Fan("System Fan", 0));
                f.Add(new Fan("CPU Fan", 1));
                f.Add(new Fan("Auxiliary Fan", 2));
                break;
            case Chip.Nct6771F:
            case Chip.Nct6776F:
                GetNuvotonConfigurationF(superIo, manufacturer, model, v, t, f, c);
                break;
            case Chip.Nct610X:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #0", 1, true));
                v.Add(new Voltage("AVCC", 2, 34, 34));
                v.Add(new Voltage("3VCC", 3, 34, 34));
                v.Add(new Voltage("Voltage #1", 4, true));
                v.Add(new Voltage("Voltage #2", 5, true));
                v.Add(new Voltage("Reserved", 6, true));
                v.Add(new Voltage("3VSB", 7, 34, 34));
                v.Add(new Voltage("VBAT", 8, 34, 34));
                v.Add(new Voltage("Voltage #10", 9, true));
                t.Add(new Temperature("SYS", 1));
                t.Add(new Temperature("CPU Core", 2));
                t.Add(new Temperature("AUX", 3));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
            case Chip.Nct6779D:
            case Chip.Nct6791D:
            case Chip.Nct6792D:
            case Chip.Nct6792Da:
            case Chip.Nct6793D:
            case Chip.Nct6795D:
            case Chip.Nct6796D:
            case Chip.Nct6796Dr:
            case Chip.Nct6797D:
            case Chip.Nct6798D:
                GetNuvotonConfigurationD(superIo, manufacturer, model, v, t, f, c);
                break;
            default:
                GetDefaultConfiguration(superIo, v, t, f, c);
                break;
        }
    }

    private static void GetDefaultConfiguration(ISuperIo superIo,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        for (var i = 0; i < superIo.Voltages.Length; i++)
            v.Add(new Voltage("Voltage #" + (i + 1), i, true));
        for (var i = 0; i < superIo.Temperatures.Length; i++)
            t.Add(new Temperature("Temperature #" + (i + 1), i));
        for (var i = 0; i < superIo.Fans.Length; i++)
            f.Add(new Fan("Fan #" + (i + 1), i));
        for (var i = 0; i < superIo.Controls.Length; i++)
            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
    }

    private static void GetIteConfigurationsA(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c,
        ref ReadValueDelegate readFan, ref UpdateDelegate postUpdate,
        ref Mutex mutex)
    {
        switch (manufacturer)
        {
            case Manufacturer.ASUS:
                switch (model)
                {
                    case Model.CrosshairIiiFormula: // IT8720F
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("CPU", 0));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        break;
                    case Model.M2NSliDeluxe:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+3.3V", 1));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 30, 10));
                        v.Add(new Voltage("+5VSB", 7, 6.8f, 10));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("Chassis Fan #1", 1));
                        f.Add(new Fan("Power Fan", 2));
                        break;
                    case Model.M4A79XtdEvo: // IT8720F           
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("Chassis Fan #1", 1));
                        f.Add(new Fan("Chassis Fan #2", 2));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Voltage #8", 7, true));
                        v.Add(new Voltage("VBat", 8));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;

            case Manufacturer.AsRock:
                switch (model)
                {
                    case Model.P55Deluxe: // IT8720F
                        GetAsRockConfiguration(superIo, v, t, f,
                            ref readFan, ref postUpdate, ref mutex);
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Voltage #8", 7, true));
                        v.Add(new Voltage("VBat", 8));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        break;
                }

                ;
                break;

            case Manufacturer.DFI:
                switch (model)
                {
                    case Model.LpBiP45T2RsElite: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("FSB VTT", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 30, 10));
                        v.Add(new Voltage("NB Core", 5));
                        v.Add(new Voltage("VDIMM", 6));
                        v.Add(new Voltage("+5VSB", 7, 6.8f, 10));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("System", 1));
                        t.Add(new Temperature("Chipset", 2));
                        f.Add(new Fan("Fan #1", 0));
                        f.Add(new Fan("Fan #2", 1));
                        f.Add(new Fan("Fan #3", 2));
                        break;
                    case Model.LpDkP55T3EH9: // IT8720F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("VTT", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 30, 10));
                        v.Add(new Voltage("CPU PLL", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("+5VSB", 7, 6.8f, 10));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("Chipset", 0));
                        t.Add(new Temperature("CPU PWM", 1));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("Fan #1", 0));
                        f.Add(new Fan("Fan #2", 1));
                        f.Add(new Fan("Fan #3", 2));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("VTT", 1, true));
                        v.Add(new Voltage("+3.3V", 2, true));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10, 0, true));
                        v.Add(new Voltage("+12V", 4, 30, 10, 0, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("DRAM", 6, true));
                        v.Add(new Voltage("+5VSB", 7, 6.8f, 10, 0, true));
                        v.Add(new Voltage("VBat", 8));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;

            case Manufacturer.Gigabyte:
                switch (model)
                {
                    case Model._965P_S3: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 7, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        break;
                    case Model.Ep45Ds3R: // IT8718F
                    case Model.Ep45Ud3R:
                    case Model.X38_DS5:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 7, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #2", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #1", 3));
                        break;
                    case Model.EX58_EXTREME: // IT8720F                 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        t.Add(new Temperature("Northbridge", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #2", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #1", 3));
                        break;
                    case Model.P35_DS3: // IT8718F 
                    case Model.P35Ds3L: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 7, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("System Fan #2", 2));
                        f.Add(new Fan("Power Fan", 3));
                        break;
                    case Model.P55_UD4: // IT8720F
                    case Model.P55AUd3: // IT8720F
                    case Model.P55MUd4: // IT8720F                
                    case Model.H55_USB3: // IT8720F
                    case Model.Ex58Ud3R: // IT8720F 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 5, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #2", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #1", 3));
                        break;
                    case Model.H55NUsb3: // IT8720F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 5, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        break;
                    case Model.G41MCombo: // IT8718F
                    case Model.G41MtS2: // IT8718F
                    case Model.G41MtS2P: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 7, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        break;
                    case Model.Ga970AUd3: // IT8720F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("System Fan #2", 2));
                        f.Add(new Fan("Power Fan", 4));
                        c.Add(new Ctrl("PWM 1", 0));
                        c.Add(new Ctrl("PWM 2", 1));
                        c.Add(new Ctrl("PWM 3", 2));
                        break;
                    case Model.GaMa770TUd3: // IT8720F
                    case Model.GaMa770TUd3P: // IT8720F                
                    case Model.GaMa790XUd3P: // IT8720F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("System Fan #2", 2));
                        f.Add(new Fan("Power Fan", 3));
                        break;
                    case Model.GaMa78LmS2H: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        t.Add(new Temperature("VRM", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("System Fan #2", 2));
                        f.Add(new Fan("Power Fan", 3));
                        break;
                    case Model.GaMa785GmUs2H: // IT8718F
                    case Model.GaMa785GmtUd2H: // IT8718F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 4, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        f.Add(new Fan("NB Fan", 2));
                        break;
                    case Model.X58AUd3R: // IT8720F 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("+3.3V", 2));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10));
                        v.Add(new Voltage("+12V", 5, 24.3f, 8.2f));
                        v.Add(new Voltage("VBat", 8));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        t.Add(new Temperature("Northbridge", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #2", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #1", 3));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1, true));
                        v.Add(new Voltage("+3.3V", 2, true));
                        v.Add(new Voltage("+5V", 3, 6.8f, 10, 0, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Voltage #8", 7, true));
                        v.Add(new Voltage("VBat", 8));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;

            default:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("Voltage #3", 2, true));
                v.Add(new Voltage("Voltage #4", 3, true));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("Voltage #8", 7, true));
                v.Add(new Voltage("VBat", 8));
                for (var i = 0; i < superIo.Temperatures.Length; i++)
                    t.Add(new Temperature("Temperature #" + (i + 1), i));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
        }
    }

    private static void GetAsRockConfiguration(ISuperIo superIo,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f,
        ref ReadValueDelegate readFan, ref UpdateDelegate postUpdate,
        ref Mutex mutex)
    {
        v.Add(new Voltage("CPU VCore", 0));
        v.Add(new Voltage("+3.3V", 2));
        v.Add(new Voltage("+12V", 4, 30, 10));
        v.Add(new Voltage("+5V", 5, 6.8f, 10));
        v.Add(new Voltage("VBat", 8));
        t.Add(new Temperature("CPU", 0));
        t.Add(new Temperature("Motherboard", 1));
        f.Add(new Fan("CPU Fan", 0));
        f.Add(new Fan("Chassis Fan #1", 1));

        // this mutex is also used by the official ASRock tool
        mutex = new Mutex(false, "ASRockOCMark");

        var exclusiveAccess = false;
        try
        {
            exclusiveAccess = mutex.WaitOne(10, false);
        }
        catch (AbandonedMutexException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        // only read additional fans if we get exclusive access
        if (exclusiveAccess)
        {
            f.Add(new Fan("Chassis Fan #2", 2));
            f.Add(new Fan("Chassis Fan #3", 3));
            f.Add(new Fan("Power Fan", 4));

            readFan = (index) =>
            {
                if (index < 2)
                {
                    return superIo.Fans[index];
                }
                else
                {
                    // get GPIO 80-87
                    var gpio = superIo.ReadGpio(7);
                    if (!gpio.HasValue)
                        return null;

                    // read the last 3 fans based on GPIO 83-85
                    int[] masks = { 0x05, 0x03, 0x06 };
                    return ((gpio.Value >> 3) & 0x07) ==
                           masks[index - 2]
                        ? superIo.Fans[2]
                        : null;
                }
            };

            var fanIndex = 0;
            postUpdate = () =>
            {
                // get GPIO 80-87
                var gpio = superIo.ReadGpio(7);
                if (!gpio.HasValue)
                    return;

                // prepare the GPIO 83-85 for the next update
                int[] masks = { 0x05, 0x03, 0x06 };
                superIo.WriteGpio(7,
                    (byte)((gpio.Value & 0xC7) | (masks[fanIndex] << 3)));
                fanIndex = (fanIndex + 1) % 3;
            };
        }
    }

    private static void GetIteConfigurationsB(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        switch (manufacturer)
        {
            case Manufacturer.ECS:
                switch (model)
                {
                    case Model.A890GxmA: // IT8721F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("VDIMM", 1));
                        v.Add(new Voltage("NB Voltage", 2));
                        v.Add(new Voltage("Analog +3.3V", 3, 10, 10));
                        // v.Add(new Voltage("VDIMM", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("System", 1));
                        t.Add(new Temperature("Northbridge", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        f.Add(new Fan("Power Fan", 2));
                        break;
                    default:
                        v.Add(new Voltage("Voltage #1", 0, true));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Analog +3.3V", 3, 10, 10, 0, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            case Manufacturer.Gigabyte:
                switch (model)
                {
                    case Model.H61MDs2Rev12: // IT8728F
                    case Model.H61MUsb3B3Rev20: // IT8728F
                        v.Add(new Voltage("VTT", 0));
                        v.Add(new Voltage("+12V", 2, 30.9f, 10));
                        v.Add(new Voltage("CPU VCore", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan", 1));
                        break;
                    case Model.H67AUd3HB3: // IT8728F
                    case Model.H67AUsb3B3: // IT8728F                
                        v.Add(new Voltage("VTT", 0));
                        v.Add(new Voltage("+5V", 1, 15, 10));
                        v.Add(new Voltage("+12V", 2, 30.9f, 10));
                        v.Add(new Voltage("CPU VCore", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #2", 3));
                        break;
                    case Model.X570_AORUS_MASTER: // IT8688E
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+3.3V", 1, 29.4f, 45.3f));
                        v.Add(new Voltage("+12V", 2, 10f, 2f));
                        v.Add(new Voltage("+5V", 3, 15f, 10f));
                        v.Add(new Voltage("CPU VCore SOC", 4));
                        v.Add(new Voltage("CPU VDDP", 5));
                        v.Add(new Voltage("DRAM CH(A/B)", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 1f, 1f));
                        v.Add(new Voltage("VBat", 8, 1f, 1f));
                        t.Add(new Temperature("System 1", 0));
                        t.Add(new Temperature("EC_TEMP1", 1));
                        t.Add(new Temperature("CPU", 2));
                        t.Add(new Temperature("PCIEX16", 3));
                        t.Add(new Temperature("VRM MOS", 4));
                        t.Add(new Temperature("PCH", 5));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System 1 Fan", 1));
                        f.Add(new Fan("System 2 Fan", 2));
                        f.Add(new Fan("PCH Fan", 3));
                        f.Add(new Fan("CPU OPT Fan", 4));
                        c.Add(new Ctrl("CPU Fan", 0));
                        c.Add(new Ctrl("System 1 Fan", 1));
                        c.Add(new Ctrl("System 2 Fan", 2));
                        c.Add(new Ctrl("PCH Fan", 3));
                        c.Add(new Ctrl("CPU OPT Fan", 4));
                        break;
                    case Model.Z390_M_GAMING: // IT8688E
                    case Model.Z390_AORUS_ULTRA:
                    case Model.Z390_UD:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+3.3V", 1, 6.49f, 10));
                        v.Add(new Voltage("+12V", 2, 5f, 1));
                        v.Add(new Voltage("+5V", 3, 1.5f, 1));
                        v.Add(new Voltage("CPU VCCGT", 4));
                        v.Add(new Voltage("CPU VCCSA", 5));
                        v.Add(new Voltage("VDDQ", 6));
                        v.Add(new Voltage("DDRVTT", 7));
                        v.Add(new Voltage("PCHCore", 8));
                        t.Add(new Temperature("System1", 0));
                        t.Add(new Temperature("PCH", 1));
                        t.Add(new Temperature("CPU", 2));
                        t.Add(new Temperature("PCIEX16", 3));
                        t.Add(new Temperature("VRM MOS", 4));
                        t.Add(new Temperature("System2", 5));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("System Fan #2", 2));
                        f.Add(new Fan("System Fan #3", 3));
                        break;
                    case Model.Z68AD3HB3: // IT8728F
                        v.Add(new Voltage("VTT", 0));
                        v.Add(new Voltage("+3.3V", 1, 6.49f, 10));
                        v.Add(new Voltage("+12V", 2, 30.9f, 10));
                        v.Add(new Voltage("+5V", 3, 7.15f, 10));
                        v.Add(new Voltage("CPU VCore", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #1", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #2", 3));
                        break;
                    case Model.P67AUd3B3: // IT8728F
                    case Model.P67AUd3RB3: // IT8728F
                    case Model.P67AUd4B3: // IT8728F                
                    case Model.Z68ApD3: // IT8728F
                    case Model.Z68XUd3HB3: // IT8728F               
                        v.Add(new Voltage("VTT", 0));
                        v.Add(new Voltage("+3.3V", 1, 6.49f, 10));
                        v.Add(new Voltage("+12V", 2, 30.9f, 10));
                        v.Add(new Voltage("+5V", 3, 7.15f, 10));
                        v.Add(new Voltage("CPU VCore", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("System Fan #2", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("System Fan #1", 3));
                        break;
                    case Model.Z68XUd7B3: // IT8728F
                        v.Add(new Voltage("VTT", 0));
                        v.Add(new Voltage("+3.3V", 1, 6.49f, 10));
                        v.Add(new Voltage("+12V", 2, 30.9f, 10));
                        v.Add(new Voltage("+5V", 3, 7.15f, 10));
                        v.Add(new Voltage("CPU VCore", 5));
                        v.Add(new Voltage("DRAM", 6));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        t.Add(new Temperature("System 3", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("Power Fan", 1));
                        f.Add(new Fan("System Fan #1", 2));
                        f.Add(new Fan("System Fan #2", 3));
                        f.Add(new Fan("System Fan #3", 4));
                        break;
                    default:
                        v.Add(new Voltage("Voltage #1", 0, true));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            case Manufacturer.Shuttle:
                switch (model)
                {
                    case Model.FH67: // IT8772E 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("DRAM", 1));
                        v.Add(new Voltage("PCH VCCIO", 2));
                        v.Add(new Voltage("CPU VCCIO", 3));
                        v.Add(new Voltage("Graphic Voltage", 4));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        t.Add(new Temperature("System", 0));
                        t.Add(new Temperature("CPU", 1));
                        f.Add(new Fan("Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        break;
                    default:
                        v.Add(new Voltage("Voltage #1", 0, true));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("Voltage #1", 0, true));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("Voltage #3", 2, true));
                v.Add(new Voltage("Voltage #4", 3, true));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                v.Add(new Voltage("VBat", 8, 10, 10));
                for (var i = 0; i < superIo.Temperatures.Length; i++)
                    t.Add(new Temperature("Temperature #" + (i + 1), i));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
        }
    }

    private static void GetIteConfigurationsC(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        switch (manufacturer)
        {
            case Manufacturer.Gigabyte:
                switch (model)
                {
                    case Model.X570_AORUS_MASTER: // IT879XE
                        v.Add(new Voltage("CPU VDD18", 0));
                        v.Add(new Voltage("DDRVTT CH(A/B)", 1));
                        v.Add(new Voltage("Chipset Core", 2));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("CPU VDD18", 4));
                        v.Add(new Voltage("PM_CLDO12", 5));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 1f, 1f));
                        v.Add(new Voltage("VBat", 8, 1f, 1f));
                        t.Add(new Temperature("PCIEX8", 0));
                        t.Add(new Temperature("EC_TEMP2", 1));
                        t.Add(new Temperature("System 2", 2));
                        f.Add(new Fan("System 5 Pump", 0));
                        f.Add(new Fan("System 6 Pump", 1));
                        f.Add(new Fan("System 4 Fan", 2));
                        break;
                    default:
                        v.Add(new Voltage("Voltage #1", 0, true));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                        v.Add(new Voltage("VBat", 8, 10, 10));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("Voltage #1", 0, true));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("Voltage #3", 2, true));
                v.Add(new Voltage("Voltage #4", 3, true));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("Standby +3.3V", 7, 10, 10, 0, true));
                v.Add(new Voltage("VBat", 8, 10, 10));
                for (var i = 0; i < superIo.Temperatures.Length; i++)
                    t.Add(new Temperature("Temperature #" + (i + 1), i));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
        }
    }

    private static void GetFintekConfiguration(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        switch (manufacturer)
        {
            case Manufacturer.EVGA:
                switch (model)
                {
                    case Model.X58SliClassified: // F71882 
                        v.Add(new Voltage("VCC3V", 0, 150, 150));
                        v.Add(new Voltage("CPU VCore", 1, 47, 100));
                        v.Add(new Voltage("DIMM", 2, 47, 100));
                        v.Add(new Voltage("CPU VTT", 3, 24, 100));
                        v.Add(new Voltage("IOH Vcore", 4, 24, 100));
                        v.Add(new Voltage("+5V", 5, 51, 12));
                        v.Add(new Voltage("+12V", 6, 56, 6.8f));
                        v.Add(new Voltage("3VSB", 7, 150, 150));
                        v.Add(new Voltage("VBat", 8, 150, 150));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("VREG", 1));
                        t.Add(new Temperature("System", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("Power Fan", 1));
                        f.Add(new Fan("Chassis Fan", 2));
                        break;
                    default:
                        v.Add(new Voltage("VCC3V", 0, 150, 150));
                        v.Add(new Voltage("CPU VCore", 1));
                        v.Add(new Voltage("Voltage #3", 2, true));
                        v.Add(new Voltage("Voltage #4", 3, true));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("VSB3V", 7, 150, 150));
                        v.Add(new Voltage("VBat", 8, 150, 150));
                        for (var i = 0; i < superIo.Temperatures.Length; i++)
                            t.Add(new Temperature("Temperature #" + (i + 1), i));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("VCC3V", 0, 150, 150));
                v.Add(new Voltage("CPU VCore", 1));
                v.Add(new Voltage("Voltage #3", 2, true));
                v.Add(new Voltage("Voltage #4", 3, true));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                if (superIo.Chip != Chip.F71808E)
                    v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("VSB3V", 7, 150, 150));
                v.Add(new Voltage("VBat", 8, 150, 150));
                for (var i = 0; i < superIo.Temperatures.Length; i++)
                    t.Add(new Temperature("Temperature #" + (i + 1), i));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));

                break;
        }
    }

    private static void GetNuvotonConfigurationF(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        switch (manufacturer)
        {
            case Manufacturer.ASUS:
                switch (model)
                {
                    case Model.P8P67: // NCT6776F
                    case Model.P8P67Evo: // NCT6776F
                    case Model.P8P67Pro: // NCT6776F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 11, 1));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 12, 3));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Auxiliary", 2));
                        t.Add(new Temperature("Motherboard", 3));
                        f.Add(new Fan("Chassis Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("Chassis Fan #2", 3));
                        c.Add(new Ctrl("Chassis Fan #2", 0));
                        c.Add(new Ctrl("CPU Fan", 1));
                        c.Add(new Ctrl("Chassis Fan #1", 2));
                        break;
                    case Model.P8P67MPro: // NCT6776F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 11, 1));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 12, 3));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 3));
                        f.Add(new Fan("Chassis Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Chassis Fan #2", 2));
                        f.Add(new Fan("Power Fan", 3));
                        f.Add(new Fan("Auxiliary Fan", 4));
                        break;
                    case Model.P8Z68VPro: // NCT6776F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 11, 1));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 12, 3));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Auxiliary", 2));
                        t.Add(new Temperature("Motherboard", 3));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan #" + (i + 1), i));
                        break;
                    case Model.P9X79: // NCT6776F
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 11, 1));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 12, 3));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 3));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU Core", 0));
                        t.Add(new Temperature("Temperature #1", 1));
                        t.Add(new Temperature("Temperature #2", 2));
                        t.Add(new Temperature("Temperature #3", 3));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("AVCC", 2, 34, 34));
                v.Add(new Voltage("3VCC", 3, 34, 34));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("3VSB", 7, 34, 34));
                v.Add(new Voltage("VBAT", 8, 34, 34));
                t.Add(new Temperature("CPU Core", 0));
                t.Add(new Temperature("Temperature #1", 1));
                t.Add(new Temperature("Temperature #2", 2));
                t.Add(new Temperature("Temperature #3", 3));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
        }
    }

    private static void GetNuvotonConfigurationD(ISuperIo superIo,
        Manufacturer manufacturer, Model model,
        IList<Voltage> v, IList<Temperature> t, IList<Fan> f, IList<Ctrl> c)
    {
        switch (manufacturer)
        {
            case Manufacturer.ASUS:
                switch (model)
                {
                    case Model.P8Z77V: // NCT6779D
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        v.Add(new Voltage("VTT", 9));
                        v.Add(new Voltage("Voltage #11", 10, true));
                        v.Add(new Voltage("Voltage #12", 11, true));
                        v.Add(new Voltage("Voltage #13", 12, true));
                        v.Add(new Voltage("Voltage #14", 13, true));
                        v.Add(new Voltage("Voltage #15", 14, true));
                        t.Add(new Temperature("CPU Core", 0));
                        t.Add(new Temperature("Auxiliary", 1));
                        t.Add(new Temperature("Motherboard", 2));
                        f.Add(new Fan("Chassis Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Chassis Fan #2", 2));
                        f.Add(new Fan("Chassis Fan #3", 3));
                        c.Add(new Ctrl("Chassis Fan #1", 0));
                        c.Add(new Ctrl("CPU  Fan", 1));
                        c.Add(new Ctrl("Chassis Fan #2", 2));
                        c.Add(new Ctrl("Chassis Fan #3", 3));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        v.Add(new Voltage("VTT", 9));
                        v.Add(new Voltage("Voltage #11", 10, true));
                        v.Add(new Voltage("Voltage #12", 11, true));
                        v.Add(new Voltage("Voltage #13", 12, true));
                        v.Add(new Voltage("Voltage #14", 13, true));
                        v.Add(new Voltage("Voltage #15", 14, true));
                        t.Add(new Temperature("CPU Core", 0));
                        t.Add(new Temperature("Temperature #1", 1));
                        t.Add(new Temperature("Temperature #2", 2));
                        t.Add(new Temperature("Temperature #3", 3));
                        t.Add(new Temperature("Temperature #4", 4));
                        t.Add(new Temperature("Temperature #5", 5));
                        t.Add(new Temperature("Temperature #6", 6));
                        for (var i = 0; i < superIo.Fans.Length; i++)
                            f.Add(new Fan("Fan #" + (i + 1), i));
                        for (var i = 0; i < superIo.Controls.Length; i++)
                            c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("AVCC", 2, 34, 34));
                v.Add(new Voltage("3VCC", 3, 34, 34));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("3VSB", 7, 34, 34));
                v.Add(new Voltage("VBAT", 8, 34, 34));
                v.Add(new Voltage("VTT", 9));
                v.Add(new Voltage("Voltage #11", 10, true));
                v.Add(new Voltage("Voltage #12", 11, true));
                v.Add(new Voltage("Voltage #13", 12, true));
                v.Add(new Voltage("Voltage #14", 13, true));
                v.Add(new Voltage("Voltage #15", 14, true));
                t.Add(new Temperature("CPU Core", 0));
                t.Add(new Temperature("Temperature #1", 1));
                t.Add(new Temperature("Temperature #2", 2));
                t.Add(new Temperature("Temperature #3", 3));
                t.Add(new Temperature("Temperature #4", 4));
                t.Add(new Temperature("Temperature #5", 5));
                t.Add(new Temperature("Temperature #6", 6));
                for (var i = 0; i < superIo.Fans.Length; i++)
                    f.Add(new Fan("Fan #" + (i + 1), i));
                for (var i = 0; i < superIo.Controls.Length; i++)
                    c.Add(new Ctrl("Fan Control #" + (i + 1), i));
                break;
        }
    }

    private static void GetWinbondConfigurationEhf(Manufacturer manufacturer,
        Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f)
    {
        switch (manufacturer)
        {
            case Manufacturer.AsRock:
                switch (model)
                {
                    case Model.Aod790Gx128M: // W83627EHF
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 4, 10, 10));
                        v.Add(new Voltage("+5V", 5, 20, 10));
                        v.Add(new Voltage("+12V", 6, 28, 5));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 2));
                        f.Add(new Fan("CPU Fan", 0));
                        f.Add(new Fan("Chassis Fan", 1));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        v.Add(new Voltage("Voltage #10", 9, true));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Auxiliary", 1));
                        t.Add(new Temperature("System", 2));
                        f.Add(new Fan("System Fan", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Auxiliary Fan", 2));
                        f.Add(new Fan("CPU Fan #2", 3));
                        f.Add(new Fan("Auxiliary Fan #2", 4));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("AVCC", 2, 34, 34));
                v.Add(new Voltage("3VCC", 3, 34, 34));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("3VSB", 7, 34, 34));
                v.Add(new Voltage("VBAT", 8, 34, 34));
                v.Add(new Voltage("Voltage #10", 9, true));
                t.Add(new Temperature("CPU", 0));
                t.Add(new Temperature("Auxiliary", 1));
                t.Add(new Temperature("System", 2));
                f.Add(new Fan("System Fan", 0));
                f.Add(new Fan("CPU Fan", 1));
                f.Add(new Fan("Auxiliary Fan", 2));
                f.Add(new Fan("CPU Fan #2", 3));
                f.Add(new Fan("Auxiliary Fan #2", 4));
                break;
        }
    }

    private static void GetWinbondConfigurationHg(Manufacturer manufacturer,
        Model model, IList<Voltage> v, IList<Temperature> t, IList<Fan> f)
    {
        switch (manufacturer)
        {
            case Manufacturer.AsRock:
                switch (model)
                {
                    case Model._880GMH_USB3: // W83627DHG-P
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 5, 15, 7.5f));
                        v.Add(new Voltage("+12V", 6, 56, 10));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 2));
                        f.Add(new Fan("Chassis Fan", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Power Fan", 2));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Auxiliary", 1));
                        t.Add(new Temperature("System", 2));
                        f.Add(new Fan("System Fan", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Auxiliary Fan", 2));
                        f.Add(new Fan("CPU Fan #2", 3));
                        f.Add(new Fan("Auxiliary Fan #2", 4));
                        break;
                }

                break;
            case Manufacturer.ASUS:
                switch (model)
                {
                    case Model.P6T: // W83667HG
                    case Model.P6X58DE: // W83667HG                 
                    case Model.RampageIiGene: // W83667HG 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 11.5f, 1.91f));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 15, 7.5f));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 2));
                        f.Add(new Fan("Chassis Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("Chassis Fan #2", 3));
                        f.Add(new Fan("Chassis Fan #3", 4));
                        break;
                    case Model.RampageExtreme: // W83667HG 
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("+12V", 1, 12, 2));
                        v.Add(new Voltage("Analog +3.3V", 2, 34, 34));
                        v.Add(new Voltage("+3.3V", 3, 34, 34));
                        v.Add(new Voltage("+5V", 4, 15, 7.5f));
                        v.Add(new Voltage("Standby +3.3V", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Motherboard", 2));
                        f.Add(new Fan("Chassis Fan #1", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Power Fan", 2));
                        f.Add(new Fan("Chassis Fan #2", 3));
                        f.Add(new Fan("Chassis Fan #3", 4));
                        break;
                    default:
                        v.Add(new Voltage("CPU VCore", 0));
                        v.Add(new Voltage("Voltage #2", 1, true));
                        v.Add(new Voltage("AVCC", 2, 34, 34));
                        v.Add(new Voltage("3VCC", 3, 34, 34));
                        v.Add(new Voltage("Voltage #5", 4, true));
                        v.Add(new Voltage("Voltage #6", 5, true));
                        v.Add(new Voltage("Voltage #7", 6, true));
                        v.Add(new Voltage("3VSB", 7, 34, 34));
                        v.Add(new Voltage("VBAT", 8, 34, 34));
                        t.Add(new Temperature("CPU", 0));
                        t.Add(new Temperature("Auxiliary", 1));
                        t.Add(new Temperature("System", 2));
                        f.Add(new Fan("System Fan", 0));
                        f.Add(new Fan("CPU Fan", 1));
                        f.Add(new Fan("Auxiliary Fan", 2));
                        f.Add(new Fan("CPU Fan #2", 3));
                        f.Add(new Fan("Auxiliary Fan #2", 4));
                        break;
                }

                break;
            default:
                v.Add(new Voltage("CPU VCore", 0));
                v.Add(new Voltage("Voltage #2", 1, true));
                v.Add(new Voltage("AVCC", 2, 34, 34));
                v.Add(new Voltage("3VCC", 3, 34, 34));
                v.Add(new Voltage("Voltage #5", 4, true));
                v.Add(new Voltage("Voltage #6", 5, true));
                v.Add(new Voltage("Voltage #7", 6, true));
                v.Add(new Voltage("3VSB", 7, 34, 34));
                v.Add(new Voltage("VBAT", 8, 34, 34));
                t.Add(new Temperature("CPU", 0));
                t.Add(new Temperature("Auxiliary", 1));
                t.Add(new Temperature("System", 2));
                f.Add(new Fan("System Fan", 0));
                f.Add(new Fan("CPU Fan", 1));
                f.Add(new Fan("Auxiliary Fan", 2));
                f.Add(new Fan("CPU Fan #2", 3));
                f.Add(new Fan("Auxiliary Fan #2", 4));
                break;
        }
    }

    public override HardwareType HardwareType => HardwareType.SuperIo;

    public override IHardware Parent => _mainboard;


    public override string GetReport()
    {
        return _superIo.GetReport();
    }

    public override void Update()
    {
        _superIo.Update();

        foreach (var sensor in _voltages)
        {
            var value = _readVoltage(sensor.Index);
            if (value.HasValue)
            {
                sensor.Value = value + (value - sensor.Parameters[2].Value) *
                    sensor.Parameters[0].Value / sensor.Parameters[1].Value;
                ActivateSensor(sensor);
            }
        }

        foreach (var sensor in _temperatures)
        {
            var value = _readTemperature(sensor.Index);
            if (value.HasValue)
            {
                sensor.Value = value + sensor.Parameters[0].Value;
                ActivateSensor(sensor);
            }
        }

        foreach (var sensor in _fans)
        {
            var value = _readFan(sensor.Index);
            if (value.HasValue)
            {
                sensor.Value = value;
                if (value.Value > 0)
                    ActivateSensor(sensor);
            }
        }

        foreach (var sensor in _controls)
        {
            var value = _readControl(sensor.Index);
            sensor.Value = value;
        }

        _postUpdate();
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var sensor in _controls)
            // restore all controls back to default
            _superIo.SetControl(sensor.Index, null);
        base.Dispose(disposing);
    }

    private class Voltage
    {
        public readonly string Name;
        public readonly int Index;
        public readonly float Ri;
        public readonly float Rf;
        public readonly float Vf;
        public readonly bool Hidden;

        public Voltage(string name, int index) :
            this(name, index, false)
        {
        }

        public Voltage(string name, int index, bool hidden) :
            this(name, index, 0, 1, 0, hidden)
        {
        }

        public Voltage(string name, int index, float ri, float rf) :
            this(name, index, ri, rf, 0, false)
        {
        }

        // float ri = 0, float rf = 1, float vf = 0, bool hidden = false) 

        public Voltage(string name, int index,
            float ri, float rf, float vf, bool hidden)
        {
            Name = name;
            Index = index;
            Ri = ri;
            Rf = rf;
            Vf = vf;
            Hidden = hidden;
        }
    }

    private class Temperature
    {
        public readonly string Name;
        public readonly int Index;

        public Temperature(string name, int index)
        {
            Name = name;
            Index = index;
        }
    }

    private class Fan
    {
        public readonly string Name;
        public readonly int Index;

        public Fan(string name, int index)
        {
            Name = name;
            Index = index;
        }
    }

    private class Ctrl
    {
        public readonly string Name;
        public readonly int Index;

        public Ctrl(string name, int index)
        {
            Name = name;
            Index = index;
        }
    }
}
