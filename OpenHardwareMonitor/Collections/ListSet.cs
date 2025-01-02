/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2013 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Collections;

public class ListSet<T> : IEnumerable<T>
{
    private readonly List<T> _list = new();

    public bool Add(T item)
    {
        if (_list.Contains(item))
            return false;

        _list.Add(item);
        return true;
    }

    public bool Remove(T item)
    {
        if (!_list.Contains(item))
            return false;

        _list.Remove(item);
        return true;
    }

    public bool Contains(T item)
    {
        return _list.Contains(item);
    }

    public T[] ToArray()
    {
        return _list.ToArray();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    public int Count => _list.Count;
}
