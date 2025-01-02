/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.RAM;

internal class RamGroup : IGroup
{
    private Hardware[] _hardware;

    public RamGroup(Smbios smbios, ISettings settings)
    {
        // No implementation for RAM on Unix systems
        if (OperatingSystem.IsUnix)
        {
            _hardware = new Hardware[0];
            return;
        }

        _hardware = new Hardware[] { new GenericRam("Generic Memory", settings) };
    }

    public string GetReport()
    {
        return null;
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public void Close()
    {
        foreach (var ram in _hardware)
            ram.Dispose();
    }
}
