/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class F718Xx : ISuperIo
{
    private readonly ushort _address;
    private readonly Chip _chip;

    private readonly double?[] _voltages;
    private readonly double?[] _temperatures;
    private readonly double?[] _fans;
    private readonly double?[] _controls;

    // Hardware Monitor
    private const byte AddressRegisterOffset = 0x05;
    private const byte DataRegisterOffset = 0x06;

    private const byte PwmValuesOffset = 0x2D;

    // Hardware Monitor Registers
    private const byte VoltageBaseReg = 0x20;
    private const byte TemperatureConfigReg = 0x69;
    private const byte TemperatureBaseReg = 0x70;

    private readonly byte[] _fanTachometerReg =
        new byte[] { 0xA0, 0xB0, 0xC0, 0xD0 };

    private readonly byte[] _fanPwmReg =
        new byte[] { 0xA3, 0xB3, 0xC3, 0xD3 };

    private bool[] _restoreDefaultFanPwmControlRequired = new bool[4];
    private byte[] _initialFanPwmControl = new byte[4];

    private byte ReadByte(byte register)
    {
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), register);
        return Ring0.ReadIoPort((ushort)(_address + DataRegisterOffset));
    }

    private void WriteByte(byte register, byte value)
    {
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), register);
        Ring0.WriteIoPort((ushort)(_address + DataRegisterOffset), value);
    }

    public byte? ReadGpio(int index)
    {
        return null;
    }

    public void WriteGpio(int index, byte value)
    {
    }

    private void SaveDefaultFanPwmControl(int index)
    {
        if (!_restoreDefaultFanPwmControlRequired[index])
        {
            _initialFanPwmControl[index] = ReadByte(_fanPwmReg[index]);
            _restoreDefaultFanPwmControlRequired[index] = true;
        }
    }

    private void RestoreDefaultFanPwmControl(int index)
    {
        if (_restoreDefaultFanPwmControlRequired[index])
        {
            WriteByte(_fanPwmReg[index], _initialFanPwmControl[index]);
            _restoreDefaultFanPwmControlRequired[index] = false;
        }
    }

    public void SetControl(int index, byte? value)
    {
        if (index < 0 || index >= _controls.Length)
            throw new ArgumentOutOfRangeException("index");

        if (!Ring0.WaitIsaBusMutex(10))
            return;

        if (value.HasValue)
        {
            SaveDefaultFanPwmControl(index);

            WriteByte(_fanPwmReg[index], value.Value);
        }
        else
        {
            RestoreDefaultFanPwmControl(index);
        }

        Ring0.ReleaseIsaBusMutex();
    }

    public F718Xx(Chip chip, ushort address)
    {
        _address = address;
        _chip = chip;

        _voltages = new double?[chip == Chip.F71858 ? 3 : 9];
        _temperatures = new double?[chip == Chip.F71808E ? 2 : 3];
        _fans = new double?[chip == Chip.F71882 || chip == Chip.F71858 ? 4 : 3];
        _controls = new double?[chip == Chip.F71878Ad ? 3 : 0];
    }

    public Chip Chip => _chip;
    public double?[] Voltages => _voltages;
    public double?[] Temperatures => _temperatures;
    public double?[] Fans => _fans;
    public double?[] Controls => _controls;

    public string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("LPC " + GetType().Name);
        r.AppendLine();
        r.Append("Base Adress: 0x");
        r.AppendLine(_address.ToString("X4", CultureInfo.InvariantCulture));
        r.AppendLine();

        if (!Ring0.WaitIsaBusMutex(100))
            return r.ToString();

        r.AppendLine("Hardware Monitor Registers");
        r.AppendLine();
        r.AppendLine("      00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        r.AppendLine();
        for (var i = 0; i <= 0xF; i++)
        {
            r.Append(" ");
            r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
            r.Append("  ");
            for (var j = 0; j <= 0xF; j++)
            {
                r.Append(" ");
                r.Append(ReadByte((byte)((i << 4) | j)).ToString("X2",
                    CultureInfo.InvariantCulture));
            }

            r.AppendLine();
        }

        r.AppendLine();

        Ring0.ReleaseIsaBusMutex();

        return r.ToString();
    }

    public void Update()
    {
        if (!Ring0.WaitIsaBusMutex(10))
            return;

        for (var i = 0; i < _voltages.Length; i++)
            if (_chip == Chip.F71808E && i == 6)
            {
                // 0x26 is reserved on F71808E
                _voltages[i] = 0;
            }
            else
            {
                int value = ReadByte((byte)(VoltageBaseReg + i));
                _voltages[i] = 0.008 * value;
            }

        for (var i = 0; i < _temperatures.Length; i++)
            switch (_chip)
            {
                case Chip.F71858:
                {
                    var tableMode = 0x3 & ReadByte(TemperatureConfigReg);
                    int high =
                        ReadByte((byte)(TemperatureBaseReg + 2 * i));
                    int low =
                        ReadByte((byte)(TemperatureBaseReg + 2 * i + 1));
                    if (high != 0xbb && high != 0xcc)
                    {
                        var bits = 0;
                        switch (tableMode)
                        {
                            case 0:
                                bits = 0;
                                break;
                            case 1:
                                bits = 0;
                                break;
                            case 2:
                                bits = (high & 0x80) << 8;
                                break;
                            case 3:
                                bits = (low & 0x01) << 15;
                                break;
                        }

                        bits |= high << 7;
                        bits |= (low & 0xe0) >> 1;
                        var value = (short)(bits & 0xfff0);
                        _temperatures[i] = value / 128.0;
                    }
                    else
                    {
                        _temperatures[i] = null;
                    }
                }
                    break;
                default:
                {
                    var value = (sbyte)ReadByte((byte)(
                        TemperatureBaseReg + 2 * (i + 1)));
                    if (value < sbyte.MaxValue && value > 0)
                        _temperatures[i] = value;
                    else
                        _temperatures[i] = null;
                }
                    break;
            }

        for (var i = 0; i < _fans.Length; i++)
        {
            var value = ReadByte(_fanTachometerReg[i]) << 8;
            value |= ReadByte((byte)(_fanTachometerReg[i] + 1));

            if (value > 0)
                _fans[i] = value < 0x0fff ? 1.5e6 / value : 0;
            else
                _fans[i] = null;
        }

        for (var i = 0; i < _controls.Length; i++) _controls[i] = ReadByte((byte)(PwmValuesOffset + i)) * 100.0 / 0xFF;

        Ring0.ReleaseIsaBusMutex();
    }
}
