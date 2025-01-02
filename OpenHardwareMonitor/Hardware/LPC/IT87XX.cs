/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Globalization;
using System.Text;
using System;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class It87Xx : ISuperIo
{
    private readonly ushort _address;
    private readonly Chip _chip;
    private readonly byte _version;

    private readonly ushort _gpioAddress;
    private readonly int _gpioCount;

    private readonly ushort _addressReg;
    private readonly ushort _dataReg;

    private readonly double?[] _voltages = new double?[0];
    private readonly double?[] _temperatures = new double?[0];
    private readonly double?[] _fans = new double?[0];
    private readonly double?[] _controls = new double?[0];

    private readonly float _voltageGain;
    private readonly bool _has16BitFanCounter;

    // Consts
    private const byte IteVendorId = 0x90;

    // Environment Controller
    private const byte AddressRegisterOffset = 0x05;
    private const byte DataRegisterOffset = 0x06;

    // Environment Controller Registers    
    private const byte ConfigurationRegister = 0x00;
    private const byte TemperatureBaseReg = 0x29;
    private const byte VendorIdRegister = 0x58;
    private const byte FanTachometerDivisorRegister = 0x0B;

    private readonly byte[] _fanTachometerReg =
        { 0x0d, 0x0e, 0x0f, 0x80, 0x82 };

    private readonly byte[] _fanTachometerExtReg =
        { 0x18, 0x19, 0x1a, 0x81, 0x83 };

    private const byte VoltageBaseReg = 0x20;
    private const byte FanMainCtrlReg = 0x13;
    private readonly byte[] _fanPwmCtrlReg;

    private readonly byte[] _fanPwmCtrlExtReg =
        { 0x63, 0x6b, 0x73, 0x7b, 0xa3 };

    private bool[] _restoreDefaultFanPwmControlRequired = new bool[5];
    private bool[] _initialFanOutputModeEnabled = new bool[3];
    private byte[] _initialFanPwmControl = new byte[5];
    private byte[] _initialFanPwmControlExt = new byte[5];

    private byte ReadByte(byte register, out bool valid)
    {
        Ring0.WriteIoPort(_addressReg, register);
        var value = Ring0.ReadIoPort(_dataReg);
        if (_chip == Chip.It8688E)
            valid = true;
        else
            valid = register == Ring0.ReadIoPort(_addressReg);
        return value;
    }

    private bool WriteByte(byte register, byte value)
    {
        Ring0.WriteIoPort(_addressReg, register);
        Ring0.WriteIoPort(_dataReg, value);
        return register == Ring0.ReadIoPort(_addressReg);
    }

    public byte? ReadGpio(int index)
    {
        if (index >= _gpioCount)
            return null;

        return Ring0.ReadIoPort((ushort)(_gpioAddress + index));
    }

    public void WriteGpio(int index, byte value)
    {
        if (index >= _gpioCount)
            return;

        Ring0.WriteIoPort((ushort)(_gpioAddress + index), value);
    }

    private void SaveDefaultFanPwmControl(int index)
    {
        if (!_restoreDefaultFanPwmControlRequired[index])
        {
            _initialFanPwmControl[index] = ReadByte(_fanPwmCtrlReg[index], out _);

            if (index < 3)
                _initialFanOutputModeEnabled[index] =
                    (ReadByte(FanMainCtrlReg, out _) & (1 << index)) > 0;

            if (_chip == Chip.It8721F ||
                _chip == Chip.It8665E ||
                _chip == Chip.It8686E ||
                _chip == Chip.It8688E ||
                _chip == Chip.It879Xe)
                _initialFanPwmControlExt[index] =
                    ReadByte(_fanPwmCtrlExtReg[index], out _);
            _restoreDefaultFanPwmControlRequired[index] = true;
        }
    }

    private void RestoreDefaultFanPwmControl(int index)
    {
        if (_restoreDefaultFanPwmControlRequired[index])
        {
            WriteByte(_fanPwmCtrlReg[index], _initialFanPwmControl[index]);

            if (index < 3)
            {
                var value = ReadByte(FanMainCtrlReg, out _);

                if ((value & (1 << index)) > 0 != _initialFanOutputModeEnabled[index])
                    WriteByte(FanMainCtrlReg, (byte)(value ^ (1 << index)));
            }

            if (_chip == Chip.It8721F ||
                _chip == Chip.It8665E ||
                _chip == Chip.It8686E ||
                _chip == Chip.It8688E ||
                _chip == Chip.It879Xe)
                WriteByte(_fanPwmCtrlExtReg[index], _initialFanPwmControlExt[index]);
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

            if (index < 3)
                if (!_initialFanOutputModeEnabled[index])
                    WriteByte(FanMainCtrlReg,
                        (byte)(ReadByte(FanMainCtrlReg, out _) | (1 << index)));

            if (_chip == Chip.It8721F ||
                _chip == Chip.It8665E ||
                _chip == Chip.It8686E ||
                _chip == Chip.It8688E ||
                _chip == Chip.It879Xe)
            {
                WriteByte(_fanPwmCtrlReg[index],
                    (byte)(_initialFanPwmControl[index] & 0x7F));
                WriteByte(_fanPwmCtrlExtReg[index], value.Value);
            }
            else
            {
                WriteByte(_fanPwmCtrlReg[index], (byte)(value.Value >> 1));
            }
        }
        else
        {
            RestoreDefaultFanPwmControl(index);
        }

        Ring0.ReleaseIsaBusMutex();
    }

    public It87Xx(Chip chip, ushort address, ushort gpioAddress, byte version)
    {
        _address = address;
        _chip = chip;
        _version = version;
        _addressReg = (ushort)(address + AddressRegisterOffset);
        _dataReg = (ushort)(address + DataRegisterOffset);
        _gpioAddress = gpioAddress;

        // Check vendor id
        bool valid;
        var vendorId = ReadByte(VendorIdRegister, out valid);
        if (!valid || vendorId != IteVendorId)
            return;

        // Bit 0x10 of the configuration register should always be 1
        var configuration = ReadByte(ConfigurationRegister, out valid);
        if ((configuration & 0x10) == 0 &&
            chip != Chip.It8655E && chip != Chip.It8665E)
            return;
        if (!valid)
            return;

        if (chip == Chip.It8665E)
            _fanPwmCtrlReg = new byte[] { 0x15, 0x16, 0x17, 0x1e, 0x1f };
        else
            _fanPwmCtrlReg = new byte[] { 0x15, 0x16, 0x17, 0x7f, 0xa7 };

        switch (chip)
        {
            case Chip.It8665E:
            case Chip.It8686E:
            case Chip.It8688E:
                _voltages = new double?[9];
                _temperatures = new double?[6];
                _fans = new double?[5];
                _controls = new double?[5];
                break;
            case Chip.It8655E:
                _voltages = new double?[9];
                _temperatures = new double?[6];
                _fans = new double?[3];
                break;
            case Chip.It879Xe:
                _voltages = new double?[9];
                _temperatures = new double?[3];
                _fans = new double?[3];
                _controls = new double?[3];
                break;
            case Chip.It8705F:
                _voltages = new double?[9];
                _temperatures = new double?[3];
                _fans = new double?[3];
                _controls = new double?[3];
                break;
            default:
                _voltages = new double?[9];
                _temperatures = new double?[3];
                _fans = new double?[5];
                _controls = new double?[3];
                break;
        }

        // set the voltage for the ADC LSB 
        switch (chip)
        {
            case Chip.It8620E:
            case Chip.It8628E:
            case Chip.It8686E:
            case Chip.It8688E:
            case Chip.It8721F:
            case Chip.It8728F:
            case Chip.It8771E:
            case Chip.It8772E:
                _voltageGain = 0.012f;
                break;
            case Chip.It8655E:
            case Chip.It8665E:
            case Chip.It879Xe:
                _voltageGain = 0.011f;
                break;
            default:
                _voltageGain = 0.016f;
                break;
        }

        // older IT8705F and IT8721F revisions do not have 16-bit fan counters
        if ((chip == Chip.It8705F && version < 3) ||
            (chip == Chip.It8712F && version < 8))
            _has16BitFanCounter = false;
        else
            _has16BitFanCounter = true;

        // Set the number of GPIO sets
        switch (chip)
        {
            case Chip.It8712F:
            case Chip.It8716F:
            case Chip.It8718F:
            case Chip.It8726F:
                _gpioCount = 5;
                break;
            case Chip.It8720F:
            case Chip.It8721F:
                _gpioCount = 8;
                break;
            default:
                _gpioCount = 0;
                break;
        }
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
        r.Append("Chip ID: 0x");
        r.AppendLine(_chip.ToString("X"));
        r.Append("Chip Version: 0x");
        r.AppendLine(
            _version.ToString("X", CultureInfo.InvariantCulture));
        r.Append("Base Address: 0x");
        r.AppendLine(
            _address.ToString("X4", CultureInfo.InvariantCulture));
        r.Append("GPIO Address: 0x");
        r.AppendLine(
            _gpioAddress.ToString("X4", CultureInfo.InvariantCulture));
        r.AppendLine();

        if (!Ring0.WaitIsaBusMutex(100))
            return r.ToString();

        r.AppendLine("Environment Controller Registers");
        r.AppendLine();
        r.AppendLine("      00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        r.AppendLine();
        for (var i = 0; i <= 0xA; i++)
        {
            r.Append(" ");
            r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
            r.Append("  ");
            for (var j = 0; j <= 0xF; j++)
            {
                r.Append(" ");
                bool valid;
                var value = ReadByte((byte)((i << 4) | j), out valid);
                r.Append(
                    valid ? value.ToString("X2", CultureInfo.InvariantCulture) : "??");
            }

            r.AppendLine();
        }

        r.AppendLine();

        r.AppendLine("GPIO Registers");
        r.AppendLine();
        for (var i = 0; i < _gpioCount; i++)
        {
            r.Append(" ");
            r.Append(ReadGpio(i).Value.ToString("X2",
                CultureInfo.InvariantCulture));
        }

        r.AppendLine();
        r.AppendLine();

        Ring0.ReleaseIsaBusMutex();

        return r.ToString();
    }

    public void Update()
    {
        if (!Ring0.WaitIsaBusMutex(10))
            return;

        for (var i = 0; i < _voltages.Length; i++)
        {
            bool valid;

            var value =
                _voltageGain * ReadByte((byte)(VoltageBaseReg + i), out valid);

            if (!valid)
                continue;
            if (value > 0)
                _voltages[i] = value;
            else
                _voltages[i] = null;
        }

        for (var i = 0; i < _temperatures.Length; i++)
        {
            bool valid;
            var value = (sbyte)ReadByte(
                (byte)(TemperatureBaseReg + i), out valid);
            if (!valid)
                continue;

            if (value < sbyte.MaxValue && value > 0)
                _temperatures[i] = value;
            else
                _temperatures[i] = null;
        }

        if (_has16BitFanCounter)
            for (var i = 0; i < _fans.Length; i++)
            {
                bool valid;
                int value = ReadByte(_fanTachometerReg[i], out valid);
                if (!valid)
                    continue;
                value |= ReadByte(_fanTachometerExtReg[i], out valid) << 8;
                if (!valid)
                    continue;

                if (value > 0x3f)
                    _fans[i] = value < 0xffff ? 1.35e6f / (value * 2) : 0;
                else
                    _fans[i] = null;
            }
        else
            for (var i = 0; i < _fans.Length; i++)
            {
                bool valid;
                int value = ReadByte(_fanTachometerReg[i], out valid);
                if (!valid)
                    continue;

                var divisor = 2;
                if (i < 2)
                {
                    int divisors = ReadByte(FanTachometerDivisorRegister, out valid);
                    if (!valid)
                        continue;
                    divisor = 1 << ((divisors >> (3 * i)) & 0x7);
                }

                if (value > 0)
                    _fans[i] = value < 0xff ? 1.35e6f / (value * divisor) : 0;
                else
                    _fans[i] = null;
            }

        for (var i = 0; i < _controls.Length; i++)
        {
            bool valid;
            var value = ReadByte(_fanPwmCtrlReg[i], out valid);
            if (!valid)
                continue;

            if ((value & 0x80) > 0)
            {
                // automatic operation (value can't be read)
                _controls[i] = null;
            }
            else
            {
                // software operation
                if (_chip == Chip.It8721F ||
                    _chip == Chip.It8665E ||
                    _chip == Chip.It8686E ||
                    _chip == Chip.It8688E ||
                    _chip == Chip.It879Xe)
                {
                    value = ReadByte(_fanPwmCtrlExtReg[i], out valid);
                    if (valid)
                        _controls[i] = Math.Round(value * 100.0f / 0xFF);
                }
                else
                {
                    _controls[i] = Math.Round((value & 0x7F) * 100.0f / 0x7F);
                }
            }
        }

        Ring0.ReleaseIsaBusMutex();
    }
}
