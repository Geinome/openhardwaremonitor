/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.RAM;

internal class GenericRam : Hardware
{
    private const double OneGigabyte = 1024 * 1024 * 1024;
    private Sensor _loadSensor;
    private Sensor _usedMemory;
    private Sensor _availableMemory;
    private Sensor _totalPhysicalMemory;
    private Sensor _commitLimit; // Virtual memory commit limit (may increase if page file is allowed to grow)
    private Sensor _currentCommit; // Currently commited virtual memory
    private Sensor _kernelSize;
    private Sensor _processCount;
    private Sensor _threadCount;
    private Sensor _handleCount;

    public GenericRam(string name, ISettings settings)
        : base(name, new Identifier("ram"), settings)
    {
        _loadSensor = new Sensor("Memory", 0, SensorType.Load, this, settings);
        ActivateSensor(_loadSensor);

        _usedMemory = new Sensor("Used Memory", 0, SensorType.Data, this,
            settings);
        ActivateSensor(_usedMemory);

        _availableMemory = new Sensor("Available Memory", 1, SensorType.Data, this,
            settings);
        ActivateSensor(_availableMemory);

        _totalPhysicalMemory = new Sensor("Total Physical Memory", 2, SensorType.Data, this, settings);
        ActivateSensor(_totalPhysicalMemory);

        _commitLimit = new Sensor("Virtual memory commit limit", 3, SensorType.Data, this, settings);
        ActivateSensor(_commitLimit);

        _currentCommit = new Sensor("Virtual memory in use", 4, SensorType.Data, this, settings);
        ActivateSensor(_currentCommit);

        _kernelSize = new Sensor("Kernel memory usage", 5, SensorType.Data, this, settings);
        ActivateSensor(_kernelSize);

        _processCount = new Sensor("Processes", 0, SensorType.RawValue, this, settings);
        ActivateSensor(_processCount);

        _threadCount = new Sensor("Threads", 1, SensorType.RawValue, this, settings);
        ActivateSensor(_threadCount);

        _handleCount = new Sensor("Handles", 2, SensorType.RawValue, this, settings);
        ActivateSensor(_handleCount);
    }

    public override HardwareType HardwareType => HardwareType.RAM;

    public override void Update()
    {
        var status = new NativeMethods.MemoryStatusEx();
        status.Length = checked((uint)Marshal.SizeOf(
            typeof(NativeMethods.MemoryStatusEx)));

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            return;

        _loadSensor.Value = 100.0f -
                           100.0f * status.AvailablePhysicalMemory /
                           status.TotalPhysicalMemory;

        _usedMemory.Value = (status.TotalPhysicalMemory
                            - status.AvailablePhysicalMemory) / OneGigabyte;

        _availableMemory.Value = status.AvailablePhysicalMemory / OneGigabyte;

        _totalPhysicalMemory.Value = status.TotalPhysicalMemory / OneGigabyte;

        var performanceInfo = new NativeMethods.PerformanceInformation();
        performanceInfo.cb = (uint)Marshal.SizeOf<NativeMethods.PerformanceInformation>();

        try
        {
            // The function is only available in the kernel as of Windows 7
            if (!NativeMethods.GetPerformanceInfo(ref performanceInfo, performanceInfo.cb)) return;
        }
        catch (EntryPointNotFoundException)
        {
            return;
        }

        _commitLimit.Value = performanceInfo.CommitLimit * performanceInfo.PageSize / OneGigabyte;
        _currentCommit.Value = performanceInfo.CommitTotal * performanceInfo.PageSize / OneGigabyte;

        _kernelSize.Value = performanceInfo.KernelNonpaged * performanceInfo.PageSize / OneGigabyte;

        _processCount.Value = performanceInfo.ProcessCount;
        _threadCount.Value = performanceInfo.ThreadCount;
        _handleCount.Value = performanceInfo.HandleCount;
    }

    private class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysicalMemory;
            public ulong AvailablePhysicalMemory;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PerformanceInformation
        {
            public uint cb;
            public nint CommitTotal;
            public nint CommitLimit;
            public nint CommitPeak;
            public nint PhysicalTotal;
            public nint PhysicalAvailable;
            public nint SystemCache;
            public nint KernelTotal;
            public nint KernelPaged;
            public nint KernelNonpaged;
            public nint PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalMemoryStatusEx(
            ref MemoryStatusEx buffer);


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, EntryPoint = "K32GetPerformanceInfo")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetPerformanceInfo(
            ref PerformanceInformation pPerformanceInformation,
            uint cb
        );
    }
}
