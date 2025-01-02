/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Collections;

public struct Pair<TF, TS>
{
    private TF _first;
    private TS _second;

    public Pair(TF first, TS second)
    {
        this._first = first;
        this._second = second;
    }

    public TF First
    {
        get => _first;
        set => _first = value;
    }

    public TS Second
    {
        get => _second;
        set => _second = value;
    }

    public override int GetHashCode()
    {
        return (_first != null ? _first.GetHashCode() : 0) ^
               (_second != null ? _second.GetHashCode() : 0);
    }
}
