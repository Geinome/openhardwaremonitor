/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2011 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.HDD;

internal class HarddriveGroup : IGroup
{
    private const int MaxDrives = 32;

    private readonly List<AbstractStorage> _hardware = new();

    public HarddriveGroup(ISettings settings)
    {
        if (OperatingSystem.IsUnix)
            return;

        // A bit of a hack to make sure we get a 1:1 relationship between physical drives and SCSI Disks (as which NVME drives are recognized, see
        // NVMeGeneric.GetDeviceInfo for further details
        NvMeGeneric previousNvmeDisk = null;
        for (var drive = 0; drive < MaxDrives; drive++)
        {
            var instance =
                AbstractStorage.CreateInstance(drive, previousNvmeDisk, settings);
            if (instance != null) _hardware.Add(instance);

            if (instance is NvMeGeneric nvme) previousNvmeDisk = nvme;
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware.ToArray();

    public string GetReport()
    {
        return null;
    }

    public void Close()
    {
        foreach (var hdd in _hardware)
            hdd.Dispose();
    }
}
