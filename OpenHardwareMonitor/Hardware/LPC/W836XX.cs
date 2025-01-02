/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class W836Xx : ISuperIo
{
    private readonly ushort _address;
    private readonly byte _revision;

    private readonly Chip _chip;

    private readonly double?[] _voltages = new double?[0];
    private readonly double?[] _temperatures = new double?[0];
    private readonly double?[] _fans = new double?[0];
    private readonly double?[] _controls = new double?[0];

    private readonly bool[] _peciTemperature = new bool[0];
    private readonly byte[] _voltageRegister = new byte[0];
    private readonly byte[] _voltageBank = new byte[0];
    private readonly double _voltageGain = 0.008;

    // Consts 
    private const ushort WinbondVendorId = 0x5CA3;
    private const byte HighByte = 0x80;

    // Hardware Monitor
    private const byte AddressRegisterOffset = 0x05;
    private const byte DataRegisterOffset = 0x06;

    // Hardware Monitor Registers
    private const byte VoltageVbatReg = 0x51;
    private const byte BankSelectRegister = 0x4E;
    private const byte VendorIdRegister = 0x4F;
    private const byte TemperatureSourceSelectReg = 0x49;

    private readonly byte[] _temperatureReg = new byte[] { 0x50, 0x50, 0x27 };
    private readonly byte[] _temperatureBank = new byte[] { 1, 2, 0 };

    private readonly byte[] _fanTachoReg =
        new byte[] { 0x28, 0x29, 0x2A, 0x3F, 0x53 };

    private readonly byte[] _fanTachoBank =
        new byte[] { 0, 0, 0, 0, 5 };

    private readonly byte[] _fanBitReg =
        new byte[] { 0x47, 0x4B, 0x4C, 0x59, 0x5D };

    private readonly byte[] _fanDivBit0 = new byte[] { 36, 38, 30, 8, 10 };
    private readonly byte[] _fanDivBit1 = new byte[] { 37, 39, 31, 9, 11 };
    private readonly byte[] _fanDivBit2 = new byte[] { 5, 6, 7, 23, 15 };

    private byte ReadByte(byte bank, byte register)
    {
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), BankSelectRegister);
        Ring0.WriteIoPort(
            (ushort)(_address + DataRegisterOffset), bank);
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), register);
        return Ring0.ReadIoPort(
            (ushort)(_address + DataRegisterOffset));
    }

    private void WriteByte(byte bank, byte register, byte value)
    {
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), BankSelectRegister);
        Ring0.WriteIoPort(
            (ushort)(_address + DataRegisterOffset), bank);
        Ring0.WriteIoPort(
            (ushort)(_address + AddressRegisterOffset), register);
        Ring0.WriteIoPort(
            (ushort)(_address + DataRegisterOffset), value);
    }

    public byte? ReadGpio(int index)
    {
        return null;
    }

    public void WriteGpio(int index, byte value)
    {
    }

    public void SetControl(int index, byte? value)
    {
    }

    public W836Xx(Chip chip, byte revision, ushort address)
    {
        this._address = address;
        this._revision = revision;
        this._chip = chip;

        if (!IsWinbondVendor())
            return;

        _temperatures = new double?[3];
        _peciTemperature = new bool[3];
        switch (chip)
        {
            case Chip.W83667Hg:
            case Chip.W83667Hgb:
                // note temperature sensor registers that read PECI
                var flag = ReadByte(0, TemperatureSourceSelectReg);
                _peciTemperature[0] = (flag & 0x04) != 0;
                _peciTemperature[1] = (flag & 0x40) != 0;
                _peciTemperature[2] = false;
                break;
            case Chip.W83627Dhg:
            case Chip.W83627Dhgp:
                // note temperature sensor registers that read PECI
                var sel = ReadByte(0, TemperatureSourceSelectReg);
                _peciTemperature[0] = (sel & 0x07) != 0;
                _peciTemperature[1] = (sel & 0x70) != 0;
                _peciTemperature[2] = false;
                break;
            default:
                // no PECI support
                _peciTemperature[0] = false;
                _peciTemperature[1] = false;
                _peciTemperature[2] = false;
                break;
        }

        switch (chip)
        {
            case Chip.W83627Ehf:
                _voltages = new double?[10];
                _voltageRegister = new byte[]
                {
                    0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x50, 0x51, 0x52
                };
                _voltageBank = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5, 5 };
                _voltageGain = 0.008;
                _fans = new double?[5];
                break;
            case Chip.W83627Dhg:
            case Chip.W83627Dhgp:
            case Chip.W83667Hg:
            case Chip.W83667Hgb:
                _voltages = new double?[9];
                _voltageRegister = new byte[]
                {
                    0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x50, 0x51
                };
                _voltageBank = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5, 5 };
                _voltageGain = 0.008;
                _fans = new double?[5];
                break;
            case Chip.W83627Hf:
            case Chip.W83627Thf:
            case Chip.W83687Thf:
                _voltages = new double?[7];
                _voltageRegister = new byte[]
                {
                    0x20, 0x21, 0x22, 0x23, 0x24, 0x50, 0x51
                };
                _voltageBank = new byte[] { 0, 0, 0, 0, 0, 5, 5 };
                _voltageGain = 0.016;
                _fans = new double?[3];
                break;
        }
    }

    private bool IsWinbondVendor()
    {
        var vendorId =
            (ushort)((ReadByte(HighByte, VendorIdRegister) << 8) |
                     ReadByte(0, VendorIdRegister));
        return vendorId == WinbondVendorId;
    }

    private static ulong SetBit(ulong target, int bit, int value)
    {
        if ((value & 1) != value)
            throw new ArgumentException("Value must be one bit only.");

        if (bit < 0 || bit > 63)
            throw new ArgumentException("Bit out of range.");

        var mask = (ulong)1 << bit;
        return value > 0 ? target | mask : target & ~mask;
    }

    public Chip Chip => _chip;
    public double?[] Voltages => _voltages;
    public double?[] Temperatures => _temperatures;
    public double?[] Fans => _fans;
    public double?[] Controls => _controls;

    public void Update()
    {
        if (!Ring0.WaitIsaBusMutex(10))
            return;

        for (var i = 0; i < _voltages.Length; i++)
            if (_voltageRegister[i] != VoltageVbatReg)
            {
                // two special VCore measurement modes for W83627THF
                double fvalue;
                if ((_chip == Chip.W83627Hf || _chip == Chip.W83627Thf ||
                     _chip == Chip.W83687Thf) && i == 0)
                {
                    var vrmConfiguration = ReadByte(0, 0x18);
                    int value = ReadByte(_voltageBank[i], _voltageRegister[i]);
                    if ((vrmConfiguration & 0x01) == 0)
                        fvalue = 0.016 * value; // VRM8 formula
                    else
                        fvalue = 0.00488 * value + 0.69; // VRM9 formula
                }
                else
                {
                    int value = ReadByte(_voltageBank[i], _voltageRegister[i]);
                    fvalue = _voltageGain * value;
                }

                if (fvalue > 0)
                    _voltages[i] = fvalue;
                else
                    _voltages[i] = null;
            }
            else
            {
                // Battery voltage
                var valid = (ReadByte(0, 0x5D) & 0x01) > 0;
                if (valid)
                    _voltages[i] = _voltageGain * ReadByte(5, VoltageVbatReg);
                else
                    _voltages[i] = null;
            }

        for (var i = 0; i < _temperatures.Length; i++)
        {
            var value = (sbyte)ReadByte(_temperatureBank[i],
                _temperatureReg[i]) << 1;
            if (_temperatureBank[i] > 0)
                value |= ReadByte(_temperatureBank[i],
                    (byte)(_temperatureReg[i] + 1)) >> 7;

            var temperature = value / 2.0f;
            if (temperature <= 125 && temperature >= -55 && !_peciTemperature[i])
                _temperatures[i] = temperature;
            else
                _temperatures[i] = null;
        }

        ulong bits = 0;
        for (var i = 0; i < _fanBitReg.Length; i++)
            bits = (bits << 8) | ReadByte(0, _fanBitReg[i]);
        var newBits = bits;
        for (var i = 0; i < _fans.Length; i++)
        {
            int count = ReadByte(_fanTachoBank[i], _fanTachoReg[i]);

            // assemble fan divisor
            var divisorBits = (int)(
                (((bits >> _fanDivBit2[i]) & 1) << 2) |
                (((bits >> _fanDivBit1[i]) & 1) << 1) |
                ((bits >> _fanDivBit0[i]) & 1));
            var divisor = 1 << divisorBits;

            var value = count < 0xff ? 1.35e6f / (count * divisor) : 0;
            _fans[i] = value;

            // update fan divisor
            if (count > 192 && divisorBits < 7)
                divisorBits++;
            if (count < 96 && divisorBits > 0)
                divisorBits--;

            newBits = SetBit(newBits, _fanDivBit2[i], (divisorBits >> 2) & 1);
            newBits = SetBit(newBits, _fanDivBit1[i], (divisorBits >> 1) & 1);
            newBits = SetBit(newBits, _fanDivBit0[i], divisorBits & 1);
        }

        // write new fan divisors 
        for (var i = _fanBitReg.Length - 1; i >= 0; i--)
        {
            var oldByte = (byte)(bits & 0xFF);
            var newByte = (byte)(newBits & 0xFF);
            bits = bits >> 8;
            newBits = newBits >> 8;
            if (oldByte != newByte)
                WriteByte(0, _fanBitReg[i], newByte);
        }

        Ring0.ReleaseIsaBusMutex();
    }

    public string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("LPC " + GetType().Name);
        r.AppendLine();
        r.Append("Chip ID: 0x");
        r.AppendLine(_chip.ToString("X"));
        r.Append("Chip revision: 0x");
        r.AppendLine(_revision.ToString("X", CultureInfo.InvariantCulture));
        r.Append("Base Adress: 0x");
        r.AppendLine(_address.ToString("X4", CultureInfo.InvariantCulture));
        r.AppendLine();

        if (!Ring0.WaitIsaBusMutex(100))
            return r.ToString();

        r.AppendLine("Hardware Monitor Registers");
        r.AppendLine();
        r.AppendLine("      00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        r.AppendLine();
        for (var i = 0; i <= 0x7; i++)
        {
            r.Append(" ");
            r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
            r.Append("  ");
            for (var j = 0; j <= 0xF; j++)
            {
                r.Append(" ");
                r.Append(ReadByte(0, (byte)((i << 4) | j)).ToString(
                    "X2", CultureInfo.InvariantCulture));
            }

            r.AppendLine();
        }

        for (var k = 1; k <= 15; k++)
        {
            r.AppendLine("Bank " + k);
            for (var i = 0x5; i < 0x6; i++)
            {
                r.Append(" ");
                r.Append((i << 4).ToString("X2", CultureInfo.InvariantCulture));
                r.Append("  ");
                for (var j = 0; j <= 0xF; j++)
                {
                    r.Append(" ");
                    r.Append(ReadByte((byte)k, (byte)((i << 4) | j)).ToString(
                        "X2", CultureInfo.InvariantCulture));
                }

                r.AppendLine();
            }
        }

        r.AppendLine();

        Ring0.ReleaseIsaBusMutex();

        return r.ToString();
    }
}
