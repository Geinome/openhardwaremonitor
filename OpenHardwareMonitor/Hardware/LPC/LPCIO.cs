/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class Lpcio
{
    private readonly List<ISuperIo> _superIOs = new();
    private readonly StringBuilder _report = new();

    // I/O Ports
    private readonly ushort[] _registerPorts = new ushort[] { 0x2E, 0x4E };
    private readonly ushort[] _valuePorts = new ushort[] { 0x2F, 0x4F };

    // Registers
    private const byte ChipIdRegister = 0x20;
    private const byte ChipRevisionRegister = 0x21;
    private const byte BaseAddressRegister = 0x60;

    private void ReportUnknownChip(LpcPort port, string type, int chip)
    {
        _report.Append("Chip ID: Unknown ");
        _report.Append(type);
        _report.Append(" with ID 0x");
        _report.Append(chip.ToString("X", CultureInfo.InvariantCulture));
        _report.Append(" at 0x");
        _report.Append(port.RegisterPort.ToString("X",
            CultureInfo.InvariantCulture));
        _report.Append("/0x");
        _report.AppendLine(port.ValuePort.ToString("X",
            CultureInfo.InvariantCulture));
        _report.AppendLine();
    }

    #region Winbond, Nuvoton, Fintek

    private const byte FintekVendorIdRegister = 0x23;
    private const ushort FintekVendorId = 0x1934;

    private const byte WinbondNuvotonHardwareMonitorLdn = 0x0B;

    private const byte F71858HardwareMonitorLdn = 0x02;
    private const byte FintekHardwareMonitorLdn = 0x04;

    private bool DetectWinbondFintek(LpcPort port)
    {
        port.WinbondNuvotonFintekEnter();

        byte logicalDeviceNumber = 0;
        var id = port.ReadByte(ChipIdRegister);
        var revision = port.ReadByte(ChipRevisionRegister);
        var chip = Chip.Unknown;
        switch (id)
        {
            case 0x05:
                switch (revision)
                {
                    case 0x07:
                        chip = Chip.F71858;
                        logicalDeviceNumber = F71858HardwareMonitorLdn;
                        break;
                    case 0x41:
                        chip = Chip.F71882;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x06:
                switch (revision)
                {
                    case 0x01:
                        chip = Chip.F71862;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x07:
                switch (revision)
                {
                    case 0x23:
                        chip = Chip.F71889F;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x08:
                switch (revision)
                {
                    case 0x14:
                        chip = Chip.F71869;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x09:
                switch (revision)
                {
                    case 0x01:
                        chip = Chip.F71808E;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                    case 0x09:
                        chip = Chip.F71889Ed;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x10:
                switch (revision)
                {
                    case 0x05:
                        chip = Chip.F71889Ad;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                    case 0x07:
                        chip = Chip.F71869A;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x11:
                switch (revision)
                {
                    case 0x06:
                        chip = Chip.F71878Ad;
                        logicalDeviceNumber = FintekHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x52:
                switch (revision)
                {
                    case 0x17:
                    case 0x3A:
                    case 0x41:
                        chip = Chip.W83627Hf;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x82:
                switch (revision & 0xF0)
                {
                    case 0x80:
                        chip = Chip.W83627Thf;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x85:
                switch (revision)
                {
                    case 0x41:
                        chip = Chip.W83687Thf;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0x88:
                switch (revision & 0xF0)
                {
                    case 0x50:
                    case 0x60:
                        chip = Chip.W83627Ehf;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xA0:
                switch (revision & 0xF0)
                {
                    case 0x20:
                        chip = Chip.W83627Dhg;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xA5:
                switch (revision & 0xF0)
                {
                    case 0x10:
                        chip = Chip.W83667Hg;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xB0:
                switch (revision & 0xF0)
                {
                    case 0x70:
                        chip = Chip.W83627Dhgp;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xB3:
                switch (revision & 0xF0)
                {
                    case 0x50:
                        chip = Chip.W83667Hgb;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xB4:
                switch (revision & 0xF0)
                {
                    case 0x70:
                        chip = Chip.Nct6771F;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xC3:
                switch (revision & 0xF0)
                {
                    case 0x30:
                        chip = Chip.Nct6776F;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xC4:
                switch (revision & 0xF0)
                {
                    case 0x50:
                        chip = Chip.Nct610X;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xC5:
                switch (revision & 0xF0)
                {
                    case 0x60:
                        chip = Chip.Nct6779D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xC8:
                switch (revision)
                {
                    case 0x03:
                        chip = Chip.Nct6791D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xC9:
                switch (revision)
                {
                    case 0x11:
                        chip = Chip.Nct6792D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                    case 0x13:
                        chip = Chip.Nct6792Da;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xD1:
                switch (revision)
                {
                    case 0x21:
                        chip = Chip.Nct6793D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xD3:
                switch (revision)
                {
                    case 0x52:
                        chip = Chip.Nct6795D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
            case 0xD4:
                switch (revision)
                {
                    case 0x23:
                        chip = Chip.Nct6796D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                    case 0x2A:
                        chip = Chip.Nct6796Dr;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                    case 0x2B:
                        chip = Chip.Nct6798D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                    case 0x51:
                        chip = Chip.Nct6797D;
                        logicalDeviceNumber = WinbondNuvotonHardwareMonitorLdn;
                        break;
                }

                break;
        }

        if (chip == Chip.Unknown)
        {
            if (id != 0 && id != 0xff)
            {
                port.WinbondNuvotonFintekExit();

                ReportUnknownChip(port, "Winbond / Nuvoton / Fintek",
                    (id << 8) | revision);
            }
        }
        else
        {
            port.Select(logicalDeviceNumber);
            var address = port.ReadWord(BaseAddressRegister);
            Thread.Sleep(1);
            var verify = port.ReadWord(BaseAddressRegister);

            var vendorId = port.ReadWord(FintekVendorIdRegister);

            // disable the hardware monitor i/o space lock on NCT679XD chips
            if (address == verify && (
                    chip == Chip.Nct6791D ||
                    chip == Chip.Nct6792D ||
                    chip == Chip.Nct6792Da ||
                    chip == Chip.Nct6793D ||
                    chip == Chip.Nct6795D ||
                    chip == Chip.Nct6796D ||
                    chip == Chip.Nct6796Dr ||
                    chip == Chip.Nct6797D ||
                    chip == Chip.Nct6798D))
                port.NuvotonDisableIoSpaceLock();

            port.WinbondNuvotonFintekExit();

            if (address != verify)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X",
                    CultureInfo.InvariantCulture));
                _report.AppendLine("Error: Address verification failed");
                _report.AppendLine();
                return false;
            }

            // some Fintek chips have address register offset 0x05 added already
            if ((address & 0x07) == 0x05)
                address &= 0xFFF8;

            if (address < 0x100 || (address & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Chip revision: 0x");
                _report.AppendLine(revision.ToString("X",
                    CultureInfo.InvariantCulture));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X",
                    CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            switch (chip)
            {
                case Chip.W83627Dhg:
                case Chip.W83627Dhgp:
                case Chip.W83627Ehf:
                case Chip.W83627Hf:
                case Chip.W83627Thf:
                case Chip.W83667Hg:
                case Chip.W83667Hgb:
                case Chip.W83687Thf:
                    _superIOs.Add(new W836Xx(chip, revision, address));
                    break;
                case Chip.Nct610X:
                case Chip.Nct6771F:
                case Chip.Nct6776F:
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
                    _superIOs.Add(new Nct677X(chip, revision, address, port));
                    break;
                case Chip.F71858:
                case Chip.F71862:
                case Chip.F71869:
                case Chip.F71878Ad:
                case Chip.F71869A:
                case Chip.F71882:
                case Chip.F71889Ad:
                case Chip.F71889Ed:
                case Chip.F71889F:
                case Chip.F71808E:
                    if (vendorId != FintekVendorId)
                    {
                        _report.Append("Chip ID: 0x");
                        _report.AppendLine(chip.ToString("X"));
                        _report.Append("Chip revision: 0x");
                        _report.AppendLine(revision.ToString("X",
                            CultureInfo.InvariantCulture));
                        _report.Append("Error: Invalid vendor ID 0x");
                        _report.AppendLine(vendorId.ToString("X",
                            CultureInfo.InvariantCulture));
                        _report.AppendLine();
                        return false;
                    }

                    _superIOs.Add(new F718Xx(chip, address));
                    break;
                default: break;
            }

            return true;
        }

        return false;
    }

    #endregion

    #region ITE

    private const byte It87EnvironmentControllerLdn = 0x04;
    private const byte It8705GpioLdn = 0x05;
    private const byte It87XxGpioLdn = 0x07;
    private const byte It87ChipVersionRegister = 0x22;

    private bool DetectIt87(LpcPort port)
    {
        // IT87XX can enter only on port 0x2E and 0x4E
        if (port.RegisterPort != 0x2E && port.RegisterPort != 0x4E)
            return false;

        port.It87Enter();

        var chipId = port.ReadWord(ChipIdRegister);
        Chip chip;
        switch (chipId)
        {
            case 0x8620:
                chip = Chip.It8620E;
                break;
            case 0x8628:
                chip = Chip.It8628E;
                break;
            case 0x8655:
                chip = Chip.It8655E;
                break;
            case 0x8665:
                chip = Chip.It8665E;
                break;
            case 0x8686:
                chip = Chip.It8686E;
                break;
            case 0x8688:
                chip = Chip.It8688E;
                break;
            case 0x8705:
                chip = Chip.It8705F;
                break;
            case 0x8712:
                chip = Chip.It8712F;
                break;
            case 0x8716:
                chip = Chip.It8716F;
                break;
            case 0x8718:
                chip = Chip.It8718F;
                break;
            case 0x8720:
                chip = Chip.It8720F;
                break;
            case 0x8721:
                chip = Chip.It8721F;
                break;
            case 0x8726:
                chip = Chip.It8726F;
                break;
            case 0x8728:
                chip = Chip.It8728F;
                break;
            case 0x8733:
                chip = Chip.It879Xe;
                break;
            case 0x8771:
                chip = Chip.It8771E;
                break;
            case 0x8772:
                chip = Chip.It8772E;
                break;
            default:
                chip = Chip.Unknown;
                break;
        }

        if (chip == Chip.Unknown)
        {
            if (chipId != 0 && chipId != 0xffff)
            {
                port.It87Exit();

                ReportUnknownChip(port, "ITE", chipId);
            }
        }
        else
        {
            port.Select(It87EnvironmentControllerLdn);
            var address = port.ReadWord(BaseAddressRegister);
            Thread.Sleep(1);
            var verify = port.ReadWord(BaseAddressRegister);

            var version = (byte)(port.ReadByte(It87ChipVersionRegister) & 0x0F);

            ushort gpioAddress;
            ushort gpioVerify;
            if (chip == Chip.It8705F)
            {
                port.Select(It8705GpioLdn);
                gpioAddress = port.ReadWord(BaseAddressRegister);
                Thread.Sleep(1);
                gpioVerify = port.ReadWord(BaseAddressRegister);
            }
            else
            {
                port.Select(It87XxGpioLdn);
                gpioAddress = port.ReadWord(BaseAddressRegister + 2);
                Thread.Sleep(1);
                gpioVerify = port.ReadWord(BaseAddressRegister + 2);
            }

            port.It87Exit();

            if (address != verify || address < 0x100 || (address & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid address 0x");
                _report.AppendLine(address.ToString("X",
                    CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            if (gpioAddress != gpioVerify || gpioAddress < 0x100 ||
                (gpioAddress & 0xF007) != 0)
            {
                _report.Append("Chip ID: 0x");
                _report.AppendLine(chip.ToString("X"));
                _report.Append("Error: Invalid GPIO address 0x");
                _report.AppendLine(gpioAddress.ToString("X",
                    CultureInfo.InvariantCulture));
                _report.AppendLine();
                return false;
            }

            _superIOs.Add(new It87Xx(chip, address, gpioAddress, version));
            return true;
        }

        return false;
    }

    #endregion

    #region SMSC

    private bool DetectSmsc(LpcPort port)
    {
        port.SmscEnter();

        var chipId = port.ReadWord(ChipIdRegister);
        Chip chip;
        switch (chipId)
        {
            default:
                chip = Chip.Unknown;
                break;
        }

        if (chip == Chip.Unknown)
        {
            if (chipId != 0 && chipId != 0xffff)
            {
                port.SmscExit();

                ReportUnknownChip(port, "SMSC", chipId);
            }
        }
        else
        {
            port.SmscExit();
            return true;
        }

        return false;
    }

    #endregion

    private void Detect()
    {
        for (var i = 0; i < _registerPorts.Length; i++)
        {
            var port = new LpcPort(_registerPorts[i], _valuePorts[i]);

            if (DetectWinbondFintek(port)) continue;

            if (DetectIt87(port)) continue;

            if (DetectSmsc(port)) continue;
        }
    }

    public Lpcio()
    {
        if (!Ring0.IsOpen)
            return;

        if (!Ring0.WaitIsaBusMutex(100))
            return;

        Detect();

        Ring0.ReleaseIsaBusMutex();
    }

    public ISuperIo[] SuperIo => _superIOs.ToArray();

    public string GetReport()
    {
        if (_report.Length > 0)
            return "LPCIO" + Environment.NewLine + Environment.NewLine + _report;
        else
            return null;
    }
}
