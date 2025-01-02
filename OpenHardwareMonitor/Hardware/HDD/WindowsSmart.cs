/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware.HDD;

internal class WindowsSmart : ISmart
{
    protected enum DriveCommand : uint
    {
        GetVersion = 0x00074080,
        SendDriveCommand = 0x0007c084,
        ReceiveDriveData = 0x0007c088,
        DiskPerformance = 0x00070020 // IOCTL_DISK_PERFORMANCE
    }

    protected enum RegisterCommand : byte
    {
        /// <summary>
        /// SMART data requested.
        /// </summary>
        SmartCmd = 0xB0,

        /// <summary>
        /// Identify data is requested.
        /// </summary>
        IdCmd = 0xEC
    }

    protected enum RegisterFeature : byte
    {
        /// <summary>
        /// Read SMART data.
        /// </summary>
        SmartReadData = 0xD0,

        /// <summary>
        /// Read SMART thresholds.
        /// </summary>
        SmartReadThresholds = 0xD1, /* obsolete */

        /// <summary>
        /// Autosave SMART data.
        /// </summary>
        SmartAutosave = 0xD2,

        /// <summary>
        /// Save SMART attributes.
        /// </summary>
        SmartSaveAttr = 0xD3,

        /// <summary>
        /// Set SMART to offline immediately.
        /// </summary>
        SmartImmediateOffline = 0xD4,

        /// <summary>
        /// Read SMART log.
        /// </summary>
        SmartReadLog = 0xD5,

        /// <summary>
        /// Write SMART log.
        /// </summary>
        SmartWriteLog = 0xD6,

        /// <summary>
        /// Write SMART thresholds.
        /// </summary>
        SmartWriteThresholds = 0xD7, /* obsolete */

        /// <summary>
        /// Enable SMART.
        /// </summary>
        SmartEnableOperations = 0xD8,

        /// <summary>
        /// Disable SMART.
        /// </summary>
        SmartDisableOperations = 0xD9,

        /// <summary>
        /// Get SMART status.
        /// </summary>
        SmartStatus = 0xDA,

        /// <summary>
        /// Set SMART to offline automatically.
        /// </summary>
        SmartAutoOffline = 0xDB /* obsolete */
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct CommandBlockRegisters
    {
        public RegisterFeature Features;
        public byte SectorCount;
        public byte LBALow;
        public byte LBAMid;
        public byte LBAHigh;
        public byte Device;
        public RegisterCommand Command;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriveCommandParameter
    {
        public uint BufferSize;
        public CommandBlockRegisters Registers;
        public byte DriveNumber;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriverStatus
    {
        public byte DriverError;
        public byte IDEError;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriveCommandResult
    {
        public uint BufferSize;
        public DriverStatus DriverStatus;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriveSmartReadDataResult
    {
        public uint BufferSize;
        public DriverStatus DriverStatus;
        public byte Version;
        public byte Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDriveAttributes)]
        public DriveAttributeValue[] Attributes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct DiskPerformance
    {
        public long BytesRead;
        public long BytesWritten;
        public long ReadTime;
        public long WriteTime;
        public long IdleTime;
        public uint ReadCount;
        public uint WriteCount;
        public uint QueueDepth;
        public uint SplitCount;
        public long QueryTime;
        public uint StorageDeviceNumber;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] StorageManagerName;

        public uint pad;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriveSmartReadThresholdsResult
    {
        public uint BufferSize;
        public DriverStatus DriverStatus;
        public byte Version;
        public byte Reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxDriveAttributes)]
        public DriveThresholdValue[] Thresholds;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct Identify
    {
        public ushort GeneralConfiguration;
        public ushort NumberOfCylinders;
        public ushort Reserved;
        public ushort NumberOfHeads;
        public ushort UnformattedBytesPerTrack;
        public ushort UnformattedBytesPerSector;
        public ushort SectorsPerTrack;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public ushort[] VendorUnique;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] SerialNumber;

        public ushort BufferType;
        public ushort BufferSectorSize;
        public ushort NumberOfEccBytes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] FirmwareRevision;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] ModelNumber;

        public ushort MoreVendorUnique;
        public ushort DoubleWordIo;
        public ushort Capabilities;
        public ushort MoreReserved;
        public ushort PioCycleTimingMode;
        public ushort DmaCycleTimingMode;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 406)]
        public byte[] More;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct DriveIdentifyResult
    {
        public uint BufferSize;
        public DriverStatus DriverStatus;
        public Identify Identify;
    }

    private const byte SmartLbaMid = 0x4F;
    private const byte SmartLbaHi = 0xC2;

    private const int MaxDriveAttributes = 512;

    private readonly SafeHandle _handle;
    private int _driveNumber;
    private ILogger _logger;
    private int _performanceStartupRetries;

    public WindowsSmart(int driveNumber)
    {
        _driveNumber = driveNumber;
        _handle = NativeMethods.CreateFile(@"\\.\PhysicalDrive" + driveNumber, FileAccess.ReadWrite,
            FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
        _logger = this.GetCurrentClassLogger();
        _performanceStartupRetries = 0;
    }

    public bool IsValid => !_handle.IsInvalid;

    public void Close()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool EnableSmart()
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException("WindowsATASmart");

        var parameter = new DriveCommandParameter();
        DriveCommandResult result;
        uint bytesReturned;

        parameter.DriveNumber = (byte)_driveNumber;
        parameter.Registers.Features = RegisterFeature.SmartEnableOperations;
        parameter.Registers.LBAMid = SmartLbaMid;
        parameter.Registers.LBAHigh = SmartLbaHi;
        parameter.Registers.Command = RegisterCommand.SmartCmd;

        return NativeMethods.DeviceIoControl(_handle, DriveCommand.SendDriveCommand,
            ref parameter, Marshal.SizeOf(parameter), out result,
            Marshal.SizeOf(typeof(DriveCommandResult)), out bytesReturned,
            IntPtr.Zero);
    }

    public DriveAttributeValue[] ReadSmartData()
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException("WindowsATASmart");

        var parameter = new DriveCommandParameter();
        DriveSmartReadDataResult result;
        uint bytesReturned;

        parameter.DriveNumber = (byte)_driveNumber;
        parameter.Registers.Features = RegisterFeature.SmartReadData;
        parameter.Registers.LBAMid = SmartLbaMid;
        parameter.Registers.LBAHigh = SmartLbaHi;
        parameter.Registers.Command = RegisterCommand.SmartCmd;

        var isValid = NativeMethods.DeviceIoControl(_handle,
            DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
            out result, Marshal.SizeOf(typeof(DriveSmartReadDataResult)),
            out bytesReturned, IntPtr.Zero);

        return isValid ? result.Attributes : new DriveAttributeValue[0];
    }

    public DrivePerformanceValues ReadThroughputValues()
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException("WindowsATASmart");

        var dpf = new DiskPerformance();
        var size = Marshal.SizeOf<DiskPerformance>();
        var sizeUsed = 0;
        if (NativeMethods.DeviceIoControl(_handle, DriveCommand.DiskPerformance, IntPtr.Zero, 0, ref dpf, size,
                out sizeUsed, IntPtr.Zero) && sizeUsed >= size)
            return new DrivePerformanceValues(dpf);

        if (_performanceStartupRetries++ < 5)
        {
            var errorCode = Marshal.GetLastWin32Error();
            _logger.LogError(
                $"Unable to obtain Disk Performance values for drive {_driveNumber}: GetLastError returns {errorCode}");

            // If the above call failed, try whether we just need to enable the performance counters (disabled by default on recent versions of Windows Server)
            try
            {
                var proc = Process.Start("diskperf.exe", "-Y");
                if (proc != null) proc.WaitForExit(5000);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is Win32Exception ||
                                       ex is InvalidOperationException)
            {
                _logger.LogError(ex, ex.Message);
            }

            // Try again.
            if (NativeMethods.DeviceIoControl(_handle, DriveCommand.DiskPerformance, IntPtr.Zero, 0, ref dpf, size,
                    out sizeUsed, IntPtr.Zero) && sizeUsed >= size)
                return new DrivePerformanceValues(dpf);
        }

        return null;
    }

    public DriveThresholdValue[] ReadSmartThresholds()
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException("WindowsATASmart");

        var parameter = new DriveCommandParameter();
        DriveSmartReadThresholdsResult result;
        uint bytesReturned = 0;

        parameter.DriveNumber = (byte)_driveNumber;
        parameter.Registers.Features = RegisterFeature.SmartReadThresholds;
        parameter.Registers.LBAMid = SmartLbaMid;
        parameter.Registers.LBAHigh = SmartLbaHi;
        parameter.Registers.Command = RegisterCommand.SmartCmd;

        var isValid = NativeMethods.DeviceIoControl(_handle,
            DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
            out result, Marshal.SizeOf(typeof(DriveSmartReadThresholdsResult)),
            out bytesReturned, IntPtr.Zero);

        return isValid ? result.Thresholds : new DriveThresholdValue[0];
    }

    private string GetString(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i += 2)
        {
            chars[i] = (char)bytes[i + 1];
            chars[i + 1] = (char)bytes[i];
        }

        return new string(chars).Trim(new char[] { ' ', '\0' });
    }

    public bool ReadNameAndFirmwareRevision(out string name, out string firmwareRevision)
    {
        if (_handle.IsClosed)
            throw new ObjectDisposedException("WindowsATASmart");

        var parameter = new DriveCommandParameter();
        DriveIdentifyResult result;
        uint bytesReturned;

        parameter.DriveNumber = (byte)_driveNumber;
        parameter.Registers.Command = RegisterCommand.IdCmd;

        var valid = NativeMethods.DeviceIoControl(_handle,
            DriveCommand.ReceiveDriveData, ref parameter, Marshal.SizeOf(parameter),
            out result, Marshal.SizeOf(typeof(DriveIdentifyResult)),
            out bytesReturned, IntPtr.Zero);

        if (!valid)
        {
            name = null;
            firmwareRevision = null;
            return false;
        }

        name = GetString(result.Identify.ModelNumber);
        firmwareRevision = GetString(result.Identify.FirmwareRevision);
        return true;
    }

    public void Dispose()
    {
        Close();
    }

    protected void Dispose(bool disposing)
    {
        if (disposing)
            if (!_handle.IsClosed)
                _handle.Close();
    }

    protected static class NativeMethods
    {
        private const string Kernel = "kernel32.dll";

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes,
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeHandle handle,
            DriveCommand command, ref DriveCommandParameter parameter,
            int parameterSize, out DriveSmartReadDataResult result, int resultSize,
            out uint bytesReturned, IntPtr overlapped);

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeHandle handle,
            DriveCommand command, IntPtr inBuffer, int inputSize, ref DiskPerformance performance,
            int resultSize,
            out int bytesReturned, IntPtr overlapped);

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeHandle handle,
            DriveCommand command, ref DriveCommandParameter parameter,
            int parameterSize, out DriveSmartReadThresholdsResult result,
            int resultSize, out uint bytesReturned, IntPtr overlapped);

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeHandle handle,
            DriveCommand command, ref DriveCommandParameter parameter,
            int parameterSize, out DriveCommandResult result, int resultSize,
            out uint bytesReturned, IntPtr overlapped);

        [DllImport(Kernel, CallingConvention = CallingConvention.Winapi,
            CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern bool DeviceIoControl(SafeHandle handle,
            DriveCommand command, ref DriveCommandParameter parameter,
            int parameterSize, out DriveIdentifyResult result, int resultSize,
            out uint bytesReturned, IntPtr overlapped);
    }
}
