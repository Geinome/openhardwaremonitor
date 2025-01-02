/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.CPU;

internal abstract class Amdcpu : GenericCpu
{
    private const byte PciBus = 0;
    private const byte PciBaseDevice = 0x18;
    private const byte DeviceVendorIdRegister = 0;
    private const ushort AmdVendorId = 0x1022;

    public Amdcpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
        : base(processorIndex, cpuid, settings)
    {
    }

    protected uint GetPciAddress(byte function, ushort deviceId)
    {
        // assemble the pci address
        var address = Ring0.GetPciAddress(PciBus,
            (byte)(PciBaseDevice + ProcessorIndex), function);

        // verify that we have the correct bus, device and function
        uint deviceVendor;
        if (!Ring0.ReadPciConfig(
                address, DeviceVendorIdRegister, out deviceVendor))
            return Ring0.InvalidPciAddress;

        if (deviceVendor != ((deviceId << 16) | AmdVendorId))
            return Ring0.InvalidPciAddress;

        return address;
    }
}
