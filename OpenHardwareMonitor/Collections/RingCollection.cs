/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2011 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Collections;

public class RingCollection<T> : IEnumerable<T>
{
    private T[] _array;

    // first item of collection
    private int _head;

    // index after the last item of the collection
    private int _tail;

    // number of items in the collection
    private int _size;

    public RingCollection() : this(0)
    {
    }

    public RingCollection(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException("capacity");
        _array = new T[capacity];
        _head = 0;
        _tail = 0;
        _size = 0;
    }

    public int Capacity
    {
        get { return _array.Length; }
        set
        {
            T[] newArray = new T[value];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, newArray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, newArray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newArray, _array.Length - _head, _tail);
                }
            }

            _array = newArray;
            _head = 0;
            _tail = _size == value ? 0 : _size;
        }
    }

    public void Clear()
    {
        if (_head < _tail)
        {
            Array.Clear(_array, _head, _size);
        }
        else
        {
            Array.Clear(_array, 0, _tail);
            Array.Clear(_array, _head, _array.Length - _head);
        }

        _head = 0;
        _tail = 0;
        _size = 0;
    }

    public void Append(T item)
    {
        if (_size == _array.Length)
        {
            int newCapacity = _array.Length * 3 / 2;
            if (newCapacity < _array.Length + 8)
                newCapacity = _array.Length + 8;
            Capacity = newCapacity;
        }

        _array[_tail] = item;
        _tail = _tail + 1 == _array.Length ? 0 : _tail + 1;
        _size++;
    }

    public T Remove()
    {
        if (_size == 0)
            throw new InvalidOperationException();

        T result = _array[_head];
        _array[_head] = default(T);
        _head = _head + 1 == _array.Length ? 0 : _head + 1;
        _size--;

        return result;
    }

    public int Count
    {
        get { return _size; }
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            int i = _head + index;
            if (i >= _array.Length)
                i -= _array.Length;
            return _array[i];
        }
        set
        {
            if (index < 0 || index >= _size)
                throw new IndexOutOfRangeException();
            int i = _head + index;
            if (i >= _array.Length)
                i -= _array.Length;
            _array[i] = value;
        }
    }

    public T First
    {
        get
        {
            if (_size == 0)
                throw new InvalidOperationException();
            return _array[_head];
        }
        set
        {
            if (_size == 0)
                throw new InvalidOperationException();
            _array[_head] = value;
        }
    }

    public T Last
    {
        get
        {
            if (_size == 0)
                throw new InvalidOperationException();
            return _array[_tail == 0 ? _array.Length - 1 : _tail - 1];
        }
        set
        {
            if (_size == 0)
                throw new InvalidOperationException();
            _array[_tail == 0 ? _array.Length - 1 : _tail - 1] = value;
        }
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(this);
    }

    private struct Enumerator : IEnumerator<T>, IEnumerator
    {
        private RingCollection<T> _collection;
        private int _index;

        public Enumerator(RingCollection<T> collection)
        {
            _collection = collection;
            _index = -1;
        }

        public void Dispose()
        {
            _index = -2;
        }

        public void Reset()
        {
            _index = -1;
        }

        public T Current
        {
            get
            {
                if (_index < 0)
                    throw new InvalidOperationException();
                return _collection[_index];
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (_index < 0)
                    throw new InvalidOperationException();
                return _collection[_index];
            }
        }

        public bool MoveNext()
        {
            if (_index == -2)
                return false;

            _index++;

            if (_index == _collection._size)
            {
                _index = -2;
                return false;
            }

            return true;
        }
    }
}
