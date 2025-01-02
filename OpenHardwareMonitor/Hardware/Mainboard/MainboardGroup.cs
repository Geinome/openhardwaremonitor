/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2010 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.Mainboard;

internal class MainboardGroup : IGroup
{
    private readonly Mainboard[] _mainboards;

    public MainboardGroup(Smbios smbios, ISettings settings)
    {
        _mainboards = new Mainboard[1];
        _mainboards[0] = new Mainboard(smbios, settings);
    }

    public void Close()
    {
        foreach (var mainboard in _mainboards)
            mainboard.Close();
    }

    public string GetReport()
    {
        return null;
    }

    public IReadOnlyList<IHardware> Hardware => _mainboards;
}
