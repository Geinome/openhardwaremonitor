/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

namespace OpenHardwareMonitor.Hardware.LPC;

internal class LpcPort
{
    private readonly ushort _registerPort;
    private readonly ushort _valuePort;

    public LpcPort(ushort registerPort, ushort valuePort)
    {
        _registerPort = registerPort;
        _valuePort = valuePort;
    }

    public ushort RegisterPort => _registerPort;

    public ushort ValuePort => _valuePort;

    private const byte DevcieSelectRegister = 0x07;
    private const byte ConfigurationControlRegister = 0x02;

    public byte ReadByte(byte register)
    {
        Ring0.WriteIoPort(_registerPort, register);
        return Ring0.ReadIoPort(_valuePort);
    }

    public void WriteByte(byte register, byte value)
    {
        Ring0.WriteIoPort(_registerPort, register);
        Ring0.WriteIoPort(_valuePort, value);
    }

    public ushort ReadWord(byte register)
    {
        return (ushort)((ReadByte(register) << 8) |
                        ReadByte((byte)(register + 1)));
    }

    public void Select(byte logicalDeviceNumber)
    {
        Ring0.WriteIoPort(_registerPort, DevcieSelectRegister);
        Ring0.WriteIoPort(_valuePort, logicalDeviceNumber);
    }

    public void WinbondNuvotonFintekEnter()
    {
        Ring0.WriteIoPort(_registerPort, 0x87);
        Ring0.WriteIoPort(_registerPort, 0x87);
    }

    public void WinbondNuvotonFintekExit()
    {
        Ring0.WriteIoPort(_registerPort, 0xAA);
    }

    private const byte NuvotonHardwareMonitorIoSpaceLock = 0x28;

    public void NuvotonDisableIoSpaceLock()
    {
        var options = ReadByte(NuvotonHardwareMonitorIoSpaceLock);

        // if the i/o space lock is enabled
        if ((options & 0x10) > 0)
            // disable the i/o space lock
            WriteByte(NuvotonHardwareMonitorIoSpaceLock,
                (byte)(options & ~0x10));
    }

    public void It87Enter()
    {
        Ring0.WriteIoPort(_registerPort, 0x87);
        Ring0.WriteIoPort(_registerPort, 0x01);
        Ring0.WriteIoPort(_registerPort, 0x55);

        if (_registerPort == 0x4E)
            Ring0.WriteIoPort(_registerPort, 0xAA);
        else
            Ring0.WriteIoPort(_registerPort, 0x55);
    }

    public void It87Exit()
    {
        // do not exit config mode for secondary super IO
        if (_registerPort != 0x4E)
        {
            Ring0.WriteIoPort(_registerPort, ConfigurationControlRegister);
            Ring0.WriteIoPort(_valuePort, 0x02);
        }
    }

    public void SmscEnter()
    {
        Ring0.WriteIoPort(_registerPort, 0x55);
    }

    public void SmscExit()
    {
        Ring0.WriteIoPort(_registerPort, 0xAA);
    }
}
