/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenHardwareMonitor.Hardware.TBalancer;

internal enum FtDevice : uint
{
    FtDevice232Bm,
    FtDevice232Am,
    FtDevice100Ax,
    FT_DEVICE_UNKNOWN,
    FtDevice2232C,
    FtDevice232R,
    FtDevice2232H,
    FtDevice4232H
}

internal enum FtStatus
{
    FT_OK,
    FT_INVALID_HANDLE,
    FT_DEVICE_NOT_FOUND,
    FT_DEVICE_NOT_OPENED,
    FT_IO_ERROR,
    FT_INSUFFICIENT_RESOURCES,
    FT_INVALID_PARAMETER,
    FT_INVALID_BAUD_RATE,
    FT_DEVICE_NOT_OPENED_FOR_ERASE,
    FT_DEVICE_NOT_OPENED_FOR_WRITE,
    FT_FAILED_TO_WRITE_DEVICE,
    FT_EEPROM_READ_FAILED,
    FT_EEPROM_WRITE_FAILED,
    FT_EEPROM_ERASE_FAILED,
    FT_EEPROM_NOT_PRESENT,
    FT_EEPROM_NOT_PROGRAMMED,
    FT_INVALID_ARGS,
    FT_OTHER_ERROR
}

internal enum FtFlowControl : ushort
{
    FT_FLOW_DTR_DSR = 512,
    FT_FLOW_NONE = 0,
    FT_FLOW_RTS_CTS = 256,
    FT_FLOW_XON_XOFF = 1024
}

internal enum FtPurge : uint
{
    FT_PURGE_RX = 1,
    FT_PURGE_TX = 2,
    FT_PURGE_ALL = 3
}

[StructLayout(LayoutKind.Sequential)]
internal struct FtHandle
{
    private readonly uint handle;
}

[StructLayout(LayoutKind.Sequential)]
internal struct FtDeviceInfoNode
{
    public uint Flags;
    public FtDevice Type;
    public uint ID;
    public uint LocId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
    public string SerialNumber;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string Description;

    public FtHandle Handle;
}

internal class Ftd2Xx
{
    public delegate FtStatus FtCreateDeviceInfoListDelegate(
        out uint numDevices);

    public delegate FtStatus FtGetDeviceInfoListDelegate(
        [Out] FtDeviceInfoNode[] deviceInfoNodes, ref uint length);

    public delegate FtStatus FtOpenDelegate(int device, out FtHandle handle);

    public delegate FtStatus FtCloseDelegate(FtHandle handle);

    public delegate FtStatus FtSetBaudRateDelegate(FtHandle handle,
        uint baudRate);

    public delegate FtStatus FtSetDataCharacteristicsDelegate(
        FtHandle handle, byte wordLength, byte stopBits, byte parity);

    public delegate FtStatus FtSetFlowControlDelegate(FtHandle handle,
        FtFlowControl flowControl, byte xon, byte xoff);

    public delegate FtStatus FtSetTimeoutsDelegate(FtHandle handle,
        uint readTimeout, uint writeTimeout);

    public delegate FtStatus FtWriteDelegate(FtHandle handle, byte[] buffer,
        uint bytesToWrite, out uint bytesWritten);

    public delegate FtStatus FtPurgeDelegate(FtHandle handle, FtPurge mask);

    public delegate FtStatus FtGetStatusDelegate(FtHandle handle,
        out uint amountInRxQueue, out uint amountInTxQueue, out uint eventStatus);

    public delegate FtStatus FtReadDelegate(FtHandle handle,
        [Out] byte[] buffer, uint bytesToRead, out uint bytesReturned);

    public delegate FtStatus FtReadByteDelegate(FtHandle handle,
        out byte buffer, uint bytesToRead, out uint bytesReturned);

    public delegate FtStatus FtGetDeviceInfoDetailDelegate(int dwIndex, ref uint lpdwFlags, ref uint lpdwType,
        ref uint lpdwId,
        ref uint lpdwLocId, [In] [Out] byte[] pcSerialNumber, [In] [Out] byte[] pcDescription,
        out FtHandle ftHandle);

    public static readonly FtCreateDeviceInfoListDelegate
        FtCreateDeviceInfoList = CreateDelegate<
            FtCreateDeviceInfoListDelegate>("FT_CreateDeviceInfoList");

    public static readonly FtGetDeviceInfoListDelegate
        FtGetDeviceInfoList = CreateDelegate<
            FtGetDeviceInfoListDelegate>("FT_GetDeviceInfoList");

    public static readonly FtGetDeviceInfoDetailDelegate
        FtGetDeviceInfoDetail = CreateDelegate<FtGetDeviceInfoDetailDelegate>("FT_GetDeviceInfoDetail");

    //[DllImport("ftd2xx.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    //public static extern FT_STATUS FT_GetDeviceInfoDetail(int dwIndex, ref uint lpdwFlags, ref uint lpdwType,
    //  ref uint lpdwID,
    //  ref uint lpdwLocId, [In, Out] byte[] pcSerialNumber, [In, Out] byte[] pcDescription,
    //  out FT_HANDLE ftHandle);

    public static readonly FtOpenDelegate
        FtOpen = CreateDelegate<
            FtOpenDelegate>("FT_Open");

    public static readonly FtCloseDelegate
        FtClose = CreateDelegate<
            FtCloseDelegate>("FT_Close");

    public static readonly FtSetBaudRateDelegate
        FtSetBaudRate = CreateDelegate<
            FtSetBaudRateDelegate>("FT_SetBaudRate");

    public static readonly FtSetDataCharacteristicsDelegate
        FtSetDataCharacteristics = CreateDelegate<
            FtSetDataCharacteristicsDelegate>("FT_SetDataCharacteristics");

    public static readonly FtSetFlowControlDelegate
        FtSetFlowControl = CreateDelegate<
            FtSetFlowControlDelegate>("FT_SetFlowControl");

    public static readonly FtSetTimeoutsDelegate
        FtSetTimeouts = CreateDelegate<
            FtSetTimeoutsDelegate>("FT_SetTimeouts");

    public static readonly FtWriteDelegate
        FtWrite = CreateDelegate<
            FtWriteDelegate>("FT_Write");

    public static readonly FtPurgeDelegate
        FtPurge = CreateDelegate<
            FtPurgeDelegate>("FT_Purge");

    public static readonly FtGetStatusDelegate
        FtGetStatus = CreateDelegate<
            FtGetStatusDelegate>("FT_GetStatus");

    public static readonly FtReadDelegate
        FtRead = CreateDelegate<
            FtReadDelegate>("FT_Read");

    public static readonly FtReadByteDelegate
        FtReadByte = CreateDelegate<
            FtReadByteDelegate>("FT_Read");

    private Ftd2Xx()
    {
    }

    public static FtStatus Write(FtHandle handle, byte[] buffer)
    {
        uint bytesWritten;
        var status = FtWrite(handle, buffer, (uint)buffer.Length,
            out bytesWritten);
        if (bytesWritten != buffer.Length)
            return FtStatus.FT_FAILED_TO_WRITE_DEVICE;
        else
            return status;
    }

    public static int BytesToRead(FtHandle handle)
    {
        uint amountInRxQueue;
        uint amountInTxQueue;
        uint eventStatus;
        if (FtGetStatus(handle, out amountInRxQueue, out amountInTxQueue,
                out eventStatus) == FtStatus.FT_OK)
            return (int)amountInRxQueue;
        else
            return 0;
    }

    public static byte ReadByte(FtHandle handle)
    {
        byte buffer;
        uint bytesReturned;
        var status = FtReadByte(handle, out buffer, 1, out bytesReturned);
        if (status != FtStatus.FT_OK || bytesReturned != 1)
            throw new InvalidOperationException();
        return buffer;
    }

    public static void Read(FtHandle handle, byte[] buffer)
    {
        uint bytesReturned;
        var status =
            FtRead(handle, buffer, (uint)buffer.Length, out bytesReturned);
        if (status != FtStatus.FT_OK || bytesReturned != buffer.Length)
            throw new InvalidOperationException();
    }

    private static string GetDllName()
    {
        if (OperatingSystem.IsUnix)
            return "libftd2xx.so";
        else
            return "ftd2xx.dll";
    }

    private static T CreateDelegate<T>(string entryPoint)
        where T : class
    {
        var attribute = new DllImportAttribute(GetDllName());
        attribute.CallingConvention = CallingConvention.StdCall;
        attribute.PreserveSig = true;
        attribute.EntryPoint = entryPoint;
        T newDelegate;
        PInvokeDelegateFactory.CreateDelegate(attribute, out newDelegate);
        return newDelegate;
    }
}
