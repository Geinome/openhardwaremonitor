/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2015 Dawid Gan <deveee@gmail.com>

*/

using System;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class Nct677X : ISuperIo
{
    private readonly ushort _port;
    private readonly byte _revision;

    private readonly Chip _chip;
    private readonly LpcPort _lpcPort;

    private readonly bool _isNuvotonVendor;

    private readonly double?[] _voltages = new double?[0];
    private readonly double?[] _temperatures = new double?[0];
    private readonly double?[] _fans = new double?[0];
    private readonly double?[] _controls = new double?[0];

    // Hardware Monitor
    private const uint AddressRegisterOffset = 0x05;
    private const uint DataRegisterOffset = 0x06;
    private const byte BankSelectRegister = 0x4E;

    private byte ReadByte(ushort address)
    {
        var bank = (byte)(address >> 8);
        var register = (byte)(address & 0xFF);
        Ring0.WriteIoPort(_port + AddressRegisterOffset, BankSelectRegister);
        Ring0.WriteIoPort(_port + DataRegisterOffset, bank);
        Ring0.WriteIoPort(_port + AddressRegisterOffset, register);
        return Ring0.ReadIoPort(_port + DataRegisterOffset);
    }

    private void WriteByte(ushort address, byte value)
    {
        var bank = (byte)(address >> 8);
        var register = (byte)(address & 0xFF);
        Ring0.WriteIoPort(_port + AddressRegisterOffset, BankSelectRegister);
        Ring0.WriteIoPort(_port + DataRegisterOffset, bank);
        Ring0.WriteIoPort(_port + AddressRegisterOffset, register);
        Ring0.WriteIoPort(_port + DataRegisterOffset, value);
    }

    // Consts 
    private const ushort NuvotonVendorId = 0x5CA3;

    // Hardware Monitor Registers    
    private readonly ushort _vendorIdHighRegister;
    private readonly ushort _vendorIdLowRegister;

    private readonly ushort[] _fanPwmOutReg;
    private readonly ushort[] _fanPwmCommandReg;
    private readonly ushort[] _fanControlModeReg;

    private readonly ushort[] _fanRpmBaseRegister;
    private readonly int _minFanRpm;

    private readonly ushort[] _fanCountRegister;
    private readonly int _maxFanCount;
    private readonly int _minFanCount;

    private bool[] _restoreDefaultFanControlRequired = new bool[7];
    private byte[] _initialFanControlMode = new byte[7];
    private byte[] _initialFanPwmCommand = new byte[7];

    private readonly ushort[] _voltageRegisters;
    private readonly ushort _voltageVBatRegister;
    private readonly ushort _vBatMonitorControlRegister;

    private readonly byte[] _temperaturesSource;

    private readonly ushort[] _temperatureRegister;
    private readonly ushort[] _temperatureHalfRegister;
    private readonly int[] _temperatureHalfBit;
    private readonly ushort[] _temperatureSourceRegister;

    private readonly ushort?[] _alternateTemperatureRegister;

    private enum SourceNct6771F : byte
    {
        SYSTIN = 1,
        CPUTIN = 2,
        AUXTIN = 3,
        SMBUSMASTER = 4,
        PECI_0 = 5,
        PECI_1 = 6,
        PECI_2 = 7,
        PECI_3 = 8,
        PECI_4 = 9,
        PECI_5 = 10,
        PECI_6 = 11,
        PECI_7 = 12,
        PCH_CHIP_CPU_MAX_TEMP = 13,
        PCH_CHIP_TEMP = 14,
        PCH_CPU_TEMP = 15,
        PCH_MCH_TEMP = 16,
        PCH_DIM0_TEMP = 17,
        PCH_DIM1_TEMP = 18,
        PCH_DIM2_TEMP = 19,
        PCH_DIM3_TEMP = 20
    }

    private enum SourceNct6776F : byte
    {
        SYSTIN = 1,
        CPUTIN = 2,
        AUXTIN = 3,
        SMBUSMASTER_0 = 4,
        SMBUSMASTER_1 = 5,
        SMBUSMASTER_2 = 6,
        SMBUSMASTER_3 = 7,
        SMBUSMASTER_4 = 8,
        SMBUSMASTER_5 = 9,
        SMBUSMASTER_6 = 10,
        SMBUSMASTER_7 = 11,
        PECI_0 = 12,
        PECI_1 = 13,
        PCH_CHIP_CPU_MAX_TEMP = 14,
        PCH_CHIP_TEMP = 15,
        PCH_CPU_TEMP = 16,
        PCH_MCH_TEMP = 17,
        PCH_DIM0_TEMP = 18,
        PCH_DIM1_TEMP = 19,
        PCH_DIM2_TEMP = 20,
        PCH_DIM3_TEMP = 21,
        BYTE_TEMP = 22
    }

    private enum SourceNct67Xxd : byte
    {
        SYSTIN = 1,
        CPUTIN = 2,
        AUXTIN0 = 3,
        AUXTIN1 = 4,
        AUXTIN2 = 5,
        AUXTIN3 = 6,
        SMBUSMASTER_0 = 8,
        SMBUSMASTER_1 = 9,
        SMBUSMASTER_2 = 10,
        SMBUSMASTER_3 = 11,
        SMBUSMASTER_4 = 12,
        SMBUSMASTER_5 = 13,
        SMBUSMASTER_6 = 14,
        SMBUSMASTER_7 = 15,
        PECI_0 = 16,
        PECI_1 = 17,
        PCH_CHIP_CPU_MAX_TEMP = 18,
        PCH_CHIP_TEMP = 19,
        PCH_CPU_TEMP = 20,
        PCH_MCH_TEMP = 21,
        PCH_DIM0_TEMP = 22,
        PCH_DIM1_TEMP = 23,
        PCH_DIM2_TEMP = 24,
        PCH_DIM3_TEMP = 25,
        BYTE_TEMP = 26
    }

    private enum SourceNct610X : byte
    {
        SYSTIN = 1,
        CPUTIN = 2,
        AUXTIN = 3,
        SMBUSMASTER_0 = 4,
        SMBUSMASTER_1 = 5,
        SMBUSMASTER_2 = 6,
        SMBUSMASTER_3 = 7,
        SMBUSMASTER_4 = 8,
        SMBUSMASTER_5 = 9,
        SMBUSMASTER_6 = 10,
        SMBUSMASTER_7 = 11,
        PECI_0 = 12,
        PECI_1 = 13,
        PCH_CHIP_CPU_MAX_TEMP = 14,
        PCH_CHIP_TEMP = 15,
        PCH_CPU_TEMP = 16,
        PCH_MCH_TEMP = 17,
        PCH_DIM0_TEMP = 18,
        PCH_DIM1_TEMP = 19,
        PCH_DIM2_TEMP = 20,
        PCH_DIM3_TEMP = 21,
        BYTE_TEMP = 22
    }

    public Nct677X(Chip chip, byte revision, ushort port, LpcPort lpcPort)
    {
        this._chip = chip;
        this._revision = revision;
        this._port = port;
        this._lpcPort = lpcPort;

        if (chip == Chip.Nct610X)
        {
            _vendorIdHighRegister = 0x80FE;
            _vendorIdLowRegister = 0x00FE;

            _fanPwmOutReg = new ushort[] { 0x04A, 0x04B, 0x04C };
            _fanPwmCommandReg = new ushort[] { 0x119, 0x129, 0x139 };
            _fanControlModeReg = new ushort[] { 0x113, 0x123, 0x133 };

            _vBatMonitorControlRegister = 0x0318;
        }
        else
        {
            _vendorIdHighRegister = 0x804F;
            _vendorIdLowRegister = 0x004F;

            _fanPwmOutReg = new ushort[]
            {
                0x001, 0x003, 0x011, 0x013, 0x015, 0x017, 0x029
            };
            _fanPwmCommandReg = new ushort[]
            {
                0x109, 0x209, 0x309, 0x809, 0x909, 0xA09, 0xB09
            };
            _fanControlModeReg = new ushort[]
            {
                0x102, 0x202, 0x302, 0x802, 0x902, 0xA02, 0xB02
            };

            _vBatMonitorControlRegister = 0x005D;
        }

        _isNuvotonVendor = IsNuvotonVendor();

        if (!_isNuvotonVendor)
            return;

        switch (chip)
        {
            case Chip.Nct6771F:
            case Chip.Nct6776F:
                if (chip == Chip.Nct6771F)
                {
                    _fans = new double?[4];

                    // min RPM value with 16-bit fan counter
                    _minFanRpm = (int)(1.35e6 / 0xFFFF);

                    _temperaturesSource = new byte[]
                    {
                        (byte)SourceNct6771F.PECI_0,
                        (byte)SourceNct6771F.CPUTIN,
                        (byte)SourceNct6771F.AUXTIN,
                        (byte)SourceNct6771F.SYSTIN
                    };
                }
                else
                {
                    _fans = new double?[5];

                    // min RPM value with 13-bit fan counter
                    _minFanRpm = (int)(1.35e6 / 0x1FFF);

                    _temperaturesSource = new byte[]
                    {
                        (byte)SourceNct6776F.PECI_0,
                        (byte)SourceNct6776F.CPUTIN,
                        (byte)SourceNct6776F.AUXTIN,
                        (byte)SourceNct6776F.SYSTIN
                    };
                }

                _fanRpmBaseRegister = new ushort[]
                    { 0x656, 0x658, 0x65A, 0x65C, 0x65E };

                _controls = new double?[3];

                _voltages = new double?[9];
                _voltageRegisters = new ushort[]
                    { 0x020, 0x021, 0x022, 0x023, 0x024, 0x025, 0x026, 0x550, 0x551 };
                _voltageVBatRegister = 0x551;

                _temperatures = new double?[4];
                _temperatureRegister = new ushort[]
                    { 0x027, 0x073, 0x075, 0x077, 0x150, 0x250, 0x62B, 0x62C, 0x62D };
                _temperatureHalfRegister = new ushort[]
                    { 0, 0x074, 0x076, 0x078, 0x151, 0x251, 0x62E, 0x62E, 0x62E };
                _temperatureHalfBit = new int[]
                    { -1, 7, 7, 7, 7, 7, 0, 1, 2 };
                _temperatureSourceRegister = new ushort[]
                    { 0x621, 0x100, 0x200, 0x300, 0x622, 0x623, 0x624, 0x625, 0x626 };

                _alternateTemperatureRegister = new ushort?[]
                    { null, null, null, null };
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
                switch (chip)
                {
                    case Chip.Nct6791D:
                    case Chip.Nct6792D:
                    case Chip.Nct6792Da:
                    case Chip.Nct6793D:
                    case Chip.Nct6795D:
                        _fans = new double?[6];
                        _controls = new double?[6];
                        break;
                    case Chip.Nct6796D:
                    case Chip.Nct6796Dr:
                    case Chip.Nct6797D:
                    case Chip.Nct6798D:
                        _fans = new double?[7];
                        _controls = new double?[7];
                        break;
                    default:
                        _fans = new double?[5];
                        _controls = new double?[5];
                        break;
                }

                _fanCountRegister = new ushort[]
                    { 0x4B0, 0x4B2, 0x4B4, 0x4B6, 0x4B8, 0x4BA, 0x4CC };

                // max value for 13-bit fan counter
                _maxFanCount = 0x1FFF;

                // min value that could be transfered to 16-bit RPM registers
                _minFanCount = 0x15;

                _voltages = new double?[15];
                _voltageRegisters = new ushort[]
                {
                    0x480, 0x481, 0x482, 0x483, 0x484, 0x485, 0x486, 0x487, 0x488,
                    0x489, 0x48A, 0x48B, 0x48C, 0x48D, 0x48E
                };
                _voltageVBatRegister = 0x488;

                _temperatures = new double?[7];
                _temperaturesSource = new byte[]
                {
                    (byte)SourceNct67Xxd.PECI_0,
                    (byte)SourceNct67Xxd.CPUTIN,
                    (byte)SourceNct67Xxd.SYSTIN,
                    (byte)SourceNct67Xxd.AUXTIN0,
                    (byte)SourceNct67Xxd.AUXTIN1,
                    (byte)SourceNct67Xxd.AUXTIN2,
                    (byte)SourceNct67Xxd.AUXTIN3
                };

                _temperatureRegister = new ushort[]
                    { 0x027, 0x073, 0x075, 0x077, 0x079, 0x07B, 0x150 };
                _temperatureHalfRegister = new ushort[]
                    { 0, 0x074, 0x076, 0x078, 0x07A, 0x07C, 0x151 };
                _temperatureHalfBit = new int[]
                    { -1, 7, 7, 7, 7, 7, 7 };
                _temperatureSourceRegister = new ushort[]
                    { 0x621, 0x100, 0x200, 0x300, 0x800, 0x900, 0x622 };

                _alternateTemperatureRegister = new ushort?[]
                    { null, 0x491, 0x490, 0x492, 0x493, 0x494, 0x495 };

                break;
            case Chip.Nct610X:

                _fans = new double?[3];
                _controls = new double?[3];

                _fanRpmBaseRegister = new ushort[] { 0x030, 0x032, 0x034 };

                // min value RPM value with 13-bit fan counter
                _minFanRpm = (int)(1.35e6 / 0x1FFF);

                _voltages = new double?[9];
                _voltageRegisters = new ushort[]
                    { 0x300, 0x301, 0x302, 0x303, 0x304, 0x305, 0x307, 0x308, 0x309 };
                _voltageVBatRegister = 0x308;

                _temperatures = new double?[4];
                _temperaturesSource = new byte[]
                {
                    (byte)SourceNct610X.PECI_0,
                    (byte)SourceNct610X.SYSTIN,
                    (byte)SourceNct610X.CPUTIN,
                    (byte)SourceNct610X.AUXTIN
                };

                _temperatureRegister = new ushort[]
                    { 0x027, 0x018, 0x019, 0x01A };
                _temperatureHalfRegister = new ushort[]
                    { 0, 0x01B, 0x11B, 0x21B };
                _temperatureHalfBit = new int[]
                    { -1, 7, 7, 7 };
                _temperatureSourceRegister = new ushort[]
                    { 0x621, 0x100, 0x200, 0x300 };

                _alternateTemperatureRegister = new ushort?[]
                    { null, 0x018, 0x019, 0x01A };

                break;
        }
    }

    private bool IsNuvotonVendor()
    {
        return ((ReadByte(_vendorIdHighRegister) << 8) |
                ReadByte(_vendorIdLowRegister)) == NuvotonVendorId;
    }

    public byte? ReadGpio(int index)
    {
        return null;
    }

    public void WriteGpio(int index, byte value)
    {
    }


    private void SaveDefaultFanControl(int index)
    {
        if (!_restoreDefaultFanControlRequired[index])
        {
            _initialFanControlMode[index] = ReadByte(_fanControlModeReg[index]);
            _initialFanPwmCommand[index] = ReadByte(_fanPwmCommandReg[index]);
            _restoreDefaultFanControlRequired[index] = true;
        }
    }

    private void RestoreDefaultFanControl(int index)
    {
        if (_restoreDefaultFanControlRequired[index])
        {
            WriteByte(_fanControlModeReg[index], _initialFanControlMode[index]);
            WriteByte(_fanPwmCommandReg[index], _initialFanPwmCommand[index]);
            _restoreDefaultFanControlRequired[index] = false;
        }
    }

    public void SetControl(int index, byte? value)
    {
        if (!_isNuvotonVendor)
            return;

        if (index < 0 || index >= _controls.Length)
            throw new ArgumentOutOfRangeException("index");

        if (!Ring0.WaitIsaBusMutex(10))
            return;

        if (value.HasValue)
        {
            SaveDefaultFanControl(index);

            // set manual mode
            WriteByte(_fanControlModeReg[index], 0);

            // set output value
            WriteByte(_fanPwmCommandReg[index], value.Value);
        }
        else
        {
            RestoreDefaultFanControl(index);
        }

        Ring0.ReleaseIsaBusMutex();
    }

    public Chip Chip => _chip;
    public double?[] Voltages => _voltages;
    public double?[] Temperatures => _temperatures;
    public double?[] Fans => _fans;
    public double?[] Controls => _controls;

    private void DisableIoSpaceLock()
    {
        if (_chip != Chip.Nct6791D &&
            _chip != Chip.Nct6792D &&
            _chip != Chip.Nct6792Da &&
            _chip != Chip.Nct6793D &&
            _chip != Chip.Nct6795D &&
            _chip != Chip.Nct6796D &&
            _chip != Chip.Nct6796Dr &&
            _chip != Chip.Nct6797D &&
            _chip != Chip.Nct6798D)
            return;

        // the lock is disabled already if the vendor ID can be read
        if (IsNuvotonVendor())
            return;

        _lpcPort.WinbondNuvotonFintekEnter();
        _lpcPort.NuvotonDisableIoSpaceLock();
        _lpcPort.WinbondNuvotonFintekExit();
    }

    public void Update()
    {
        if (!_isNuvotonVendor)
            return;

        if (!Ring0.WaitIsaBusMutex(10))
            return;

        DisableIoSpaceLock();

        for (var i = 0; i < _voltages.Length; i++)
        {
            double value = 0.008f * ReadByte(_voltageRegisters[i]);
            var valid = value > 0;

            // check if battery voltage monitor is enabled
            if (valid && _voltageRegisters[i] == _voltageVBatRegister)
                valid = (ReadByte(_vBatMonitorControlRegister) & 0x01) > 0;

            _voltages[i] = valid ? value : (double?)null;
        }

        var temperatureSourceMask = 0;
        for (var i = _temperatureRegister.Length - 1; i >= 0; i--)
        {
            var value = (sbyte)ReadByte(_temperatureRegister[i]) << 1;
            if (_temperatureHalfBit[i] > 0)
                value |= (ReadByte(_temperatureHalfRegister[i]) >>
                          _temperatureHalfBit[i]) & 0x1;

            var source = ReadByte(_temperatureSourceRegister[i]);
            temperatureSourceMask |= 1 << source;

            float? temperature = 0.5f * value;
            if (temperature > 125 || temperature < -55)
                temperature = null;

            for (var j = 0; j < _temperatures.Length; j++)
                if (_temperaturesSource[j] == source)
                    _temperatures[j] = temperature;
        }

        for (var i = 0; i < _alternateTemperatureRegister.Length; i++)
        {
            if (!_alternateTemperatureRegister[i].HasValue)
                continue;

            if ((temperatureSourceMask & (1 << _temperaturesSource[i])) > 0)
                continue;

            double? temperature = (sbyte)
                ReadByte(_alternateTemperatureRegister[i].Value);

            if (temperature > 125 || temperature < -55)
                temperature = null;

            _temperatures[i] = temperature;
        }

        for (var i = 0; i < _fans.Length; i++)
            if (_fanCountRegister != null)
            {
                var high = ReadByte(_fanCountRegister[i]);
                var low = ReadByte((ushort)(_fanCountRegister[i] + 1));
                var count = (high << 5) | (low & 0x1F);
                if (count < _maxFanCount)
                {
                    if (count >= _minFanCount)
                        _fans[i] = 1.35e6f / count;
                    else
                        _fans[i] = null;
                }
                else
                {
                    _fans[i] = 0;
                }
            }
            else
            {
                var high = ReadByte(_fanRpmBaseRegister[i]);
                var low = ReadByte((ushort)(_fanRpmBaseRegister[i] + 1));
                var value = (high << 8) | low;

                _fans[i] = value > _minFanRpm ? value : 0;
            }

        for (var i = 0; i < _controls.Length; i++)
        {
            int value = ReadByte(_fanPwmOutReg[i]);
            _controls[i] = value / 2.55f;
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
        r.AppendLine(_port.ToString("X4", CultureInfo.InvariantCulture));
        r.AppendLine();

        if (!Ring0.WaitIsaBusMutex(100))
            return r.ToString();

        var addresses = new ushort[]
        {
            0x000, 0x010, 0x020, 0x030, 0x040, 0x050, 0x060, 0x070, 0x0F0,
            0x100, 0x110, 0x120, 0x130, 0x140, 0x150,
            0x200, 0x210, 0x220, 0x230, 0x240, 0x250, 0x260,
            0x300, 0x320, 0x330, 0x340, 0x360,
            0x400, 0x410, 0x420, 0x440, 0x450, 0x460, 0x480, 0x490, 0x4B0,
            0x4C0, 0x4F0,
            0x500, 0x550, 0x560,
            0x600, 0x610, 0x620, 0x630, 0x640, 0x650, 0x660, 0x670,
            0x700, 0x710, 0x720, 0x730,
            0x800, 0x820, 0x830, 0x840,
            0x900, 0x920, 0x930, 0x940, 0x960,
            0xA00, 0xA10, 0xA20, 0xA30, 0xA40, 0xA50, 0xA60, 0xA70,
            0xB00, 0xB10, 0xB20, 0xB30, 0xB50, 0xB60, 0xB70,
            0xC00, 0xC10, 0xC20, 0xC30, 0xC50, 0xC60, 0xC70,
            0xD00, 0xD10, 0xD20, 0xD30, 0xD50, 0xD60,
            0xE00, 0xE10, 0xE20, 0xE30,
            0xF00, 0xF10, 0xF20, 0xF30,
            0x8040, 0x80F0
        };

        r.AppendLine("Hardware Monitor Registers");
        r.AppendLine();
        r.AppendLine("        00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F");
        r.AppendLine();
        foreach (var address in addresses)
        {
            r.Append(" ");
            r.Append(address.ToString("X4", CultureInfo.InvariantCulture));
            r.Append("  ");
            for (ushort j = 0; j <= 0xF; j++)
            {
                r.Append(" ");
                r.Append(ReadByte((ushort)(address | j)).ToString(
                    "X2", CultureInfo.InvariantCulture));
            }

            r.AppendLine();
        }

        r.AppendLine();

        Ring0.ReleaseIsaBusMutex();

        return r.ToString();
    }
}
