/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2011 Christian Vallières

*/

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Nvidia;

internal enum NvStatus
{
    OK = 0,
    ERROR = -1,
    LIBRARY_NOT_FOUND = -2,
    NO_IMPLEMENTATION = -3,
    API_NOT_INTIALIZED = -4,
    INVALID_ARGUMENT = -5,
    NVIDIA_DEVICE_NOT_FOUND = -6,
    END_ENUMERATION = -7,
    INVALID_HANDLE = -8,
    INCOMPATIBLE_STRUCT_VERSION = -9,
    HANDLE_INVALIDATED = -10,
    OPENGL_CONTEXT_NOT_CURRENT = -11,
    NO_GL_EXPERT = -12,
    INSTRUMENTATION_DISABLED = -13,
    EXPECTED_LOGICAL_GPU_HANDLE = -100,
    EXPECTED_PHYSICAL_GPU_HANDLE = -101,
    EXPECTED_DISPLAY_HANDLE = -102,
    INVALID_COMBINATION = -103,
    NOT_SUPPORTED = -104,
    PORTID_NOT_FOUND = -105,
    EXPECTED_UNATTACHED_DISPLAY_HANDLE = -106,
    INVALID_PERF_LEVEL = -107,
    DEVICE_BUSY = -108,
    NV_PERSIST_FILE_NOT_FOUND = -109,
    PERSIST_DATA_NOT_FOUND = -110,
    EXPECTED_TV_DISPLAY = -111,
    EXPECTED_TV_DISPLAY_ON_DCONNECTOR = -112,
    NO_ACTIVE_SLI_TOPOLOGY = -113,
    SLI_RENDERING_MODE_NOTALLOWED = -114,
    EXPECTED_DIGITAL_FLAT_PANEL = -115,
    ARGUMENT_EXCEED_MAX_SIZE = -116,
    DEVICE_SWITCHING_NOT_ALLOWED = -117,
    TESTING_CLOCKS_NOT_SUPPORTED = -118,
    UNKNOWN_UNDERSCAN_CONFIG = -119,
    TIMEOUT_RECONFIGURING_GPU_TOPO = -120,
    DATA_NOT_FOUND = -121,
    EXPECTED_ANALOG_DISPLAY = -122,
    NO_VIDLINK = -123,
    REQUIRES_REBOOT = -124,
    INVALID_HYBRID_MODE = -125,
    MIXED_TARGET_TYPES = -126,
    SYSWOW64_NOT_SUPPORTED = -127,
    IMPLICIT_SET_GPU_TOPOLOGY_CHANGE_NOT_ALLOWED = -128,
    REQUEST_USER_TO_CLOSE_NON_MIGRATABLE_APPS = -129,
    OUT_OF_MEMORY = -130,
    WAS_STILL_DRAWING = -131,
    FILE_NOT_FOUND = -132,
    TOO_MANY_UNIQUE_STATE_OBJECTS = -133,
    INVALID_CALL = -134,
    D3D101LibraryNotFound = -135,
    FUNCTION_NOT_FOUND = -136
}

internal enum NvThermalController
{
    NONE = 0,
    GPU_INTERNAL,
    ADM1032,
    MAX6649,
    MAX1617,
    LM99,
    LM89,
    LM64,
    ADT7473,
    SBMAX6649,
    VBIOSEVT,
    OS,
    UNKNOWN = -1
}

internal enum NvThermalTarget
{
    NONE = 0,
    GPU = 1,
    MEMORY = 2,
    POWER_SUPPLY = 4,
    BOARD = 8,
    ALL = 15,
    UNKNOWN = -1
};

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvSensor
{
    public NvThermalController Controller;
    public uint DefaultMinTemp;
    public uint DefaultMaxTemp;
    public uint CurrentTemp;
    public NvThermalTarget Target;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvGpuThermalSettings
{
    public uint Version;
    public uint Count;

    [MarshalAs(UnmanagedType.ByValArray,
        SizeConst = Nvapi.MaxThermalSensorsPerGpu)]
    public NvSensor[] Sensor;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NvDisplayHandle
{
    private readonly IntPtr ptr;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NvPhysicalGpuHandle
{
    private readonly IntPtr ptr;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvClocks
{
    public uint Version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.MaxClocksPerGpu)]
    public uint[] Clock;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvUtilizationDomainEx
{
    public bool Present;
    public int Percentage;
}

public enum UtilizationDomain
{
    GPU,
    FrameBuffer,
    VideoEngine,
    BusInterface
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvDynamicPstatesInfoEx
{
    public uint Version;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.NvapiMaxGpuUtilizations)]
    public NvUtilizationDomainEx[] UtilizationDomains;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvUtilizationDomain
{
    public bool Present;
    public int Percentage;
    public ulong Reserved;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvDynamicPstatesInfo
{
    public uint Version;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.NvapiMaxGpuUtilizations)]
    public NvUtilizationDomain[] UtilizationDomains;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvCooler
{
    public int Type;
    public int Controller;
    public int DefaultMin;
    public int DefaultMax;
    public int CurrentMin;
    public int CurrentMax;
    public int CurrentLevel;
    public int DefaultPolicy;
    public int CurrentPolicy;
    public int Target;
    public int ControlType;
    public int Active;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvGpuCoolerSettings
{
    public uint Version;
    public uint Count;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.MaxCoolerPerGpu)]
    public NvCooler[] Cooler;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvLevel
{
    public int Level;
    public int Policy;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvGpuCoolerLevels
{
    public uint Version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.MaxCoolerPerGpu)]
    public NvLevel[] Levels;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvGpuMemoryInfo
{
    public uint Version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Nvapi.MaxMemoryValuesPerGpu)]
    public uint[] Values;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvDisplayDriverMemoryInfo
{
    public uint Version;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst =
        Nvapi.MaxMemoryValuesPerGpu)]
    public uint[] Values;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvDisplayDriverVersion
{
    public uint Version;
    public uint DriverVersion;
    public uint BldChangeListNum;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Nvapi.ShortStringMax)]
    public string BuildBranch;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Nvapi.ShortStringMax)]
    public string Adapter;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvFanCoolersStatus
{
    public uint Version;
    public uint Count;

    public ulong Reserved1;
    public ulong Reserved2;
    public ulong Reserved3;
    public ulong Reserved4;

    [MarshalAs(UnmanagedType.ByValArray,
        SizeConst = Nvapi.MaxFanCoolersStatusItems)]
    internal NvFanCoolersStatusItem[] Items;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal struct NvFanCoolersStatusItem
{
    public uint Type;
    public uint CurrentRpm;
    public uint CurrentMinLevel;
    public uint CurrentMaxLevel;
    public uint CurrentLevel;

    public uint Reserved1;
    public uint Reserved2;
    public uint Reserved3;
    public uint Reserved4;
    public uint Reserved5;
    public uint Reserved6;
    public uint Reserved7;
    public uint Reserved8;
}

internal class Nvapi
{
    public const int MaxPhysicalGpus = 64;
    public const int ShortStringMax = 64;

    public const int MaxThermalSensorsPerGpu = 3;
    public const int MaxClocksPerGpu = 0x120;
    public const int NvapiMaxGpuUtilizations = 8;
    public const int MaxCoolerPerGpu = 20;
    public const int MaxMemoryValuesPerGpu = 5;
    public const int MaxFanCoolersStatusItems = 32;

    public static readonly uint GpuThermalSettingsVer = (uint)
        Marshal.SizeOf(typeof(NvGpuThermalSettings)) | 0x10000;

    public static readonly uint GpuClocksVer = (uint)
        Marshal.SizeOf(typeof(NvClocks)) | 0x20000;

    public static readonly uint GpuDynamicPstatesInfoExVer = (uint)
        Marshal.SizeOf(typeof(NvDynamicPstatesInfoEx)) | 0x10000;

    public static readonly uint GpuDynamicPstatesInfoVer = (uint)
        Marshal.SizeOf(typeof(NvDynamicPstatesInfo)) | 0x10000;

    public static readonly uint GpuCoolerSettingsVer = (uint)
        Marshal.SizeOf(typeof(NvGpuCoolerSettings)) | 0x20000;

    public static readonly uint GpuMemoryInfoVer = (uint)
        Marshal.SizeOf(typeof(NvGpuMemoryInfo)) | 0x20000;

    public static readonly uint DisplayDriverMemoryInfoVer = (uint)
        Marshal.SizeOf(typeof(NvDisplayDriverMemoryInfo)) | 0x20000;

    public static readonly uint DisplayDriverVersionVer = (uint)
        Marshal.SizeOf(typeof(NvDisplayDriverVersion)) | 0x10000;

    public static readonly uint GpuCoolerLevelsVer = (uint)
        Marshal.SizeOf(typeof(NvGpuCoolerLevels)) | 0x10000;

    public static readonly uint GpuFanCoolersStatusVer = (uint)
        Marshal.SizeOf(typeof(NvFanCoolersStatus)) | 0x10000;

    private delegate IntPtr NvapiQueryInterfaceDelegate(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiInitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvStatus NvApiGpuGetFullNameDelegate(
        NvPhysicalGpuHandle gpuHandle, StringBuilder name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetThermalSettingsDelegate(
        NvPhysicalGpuHandle gpuHandle, int sensorIndex,
        ref NvGpuThermalSettings gpuThermalSettings);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiEnumNvidiaDisplayHandleDelegate(int thisEnum,
        ref NvDisplayHandle displayHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGetPhysicalGpUsFromDisplayDelegate(
        NvDisplayHandle displayHandle, [Out] NvPhysicalGpuHandle[] gpuHandles,
        out uint gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiEnumPhysicalGpUsDelegate(
        [Out] NvPhysicalGpuHandle[] gpuHandles, out int gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetTachReadingDelegate(
        NvPhysicalGpuHandle gpuHandle, out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetAllClocksDelegate(
        NvPhysicalGpuHandle gpuHandle, ref NvClocks clocks);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetDynamicPstatesInfoExDelegate(
        NvPhysicalGpuHandle gpuHandle,
        ref NvDynamicPstatesInfoEx dynamicPstatesInfoEx);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetDynamicPstatesInfoDelegate(
        NvPhysicalGpuHandle gpuHandle,
        ref NvDynamicPstatesInfo dynamicPstatesInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetCoolerSettingsDelegate(
        NvPhysicalGpuHandle gpuHandle, int coolerIndex,
        ref NvGpuCoolerSettings gpuCoolerSettings);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuSetCoolerLevelsDelegate(
        NvPhysicalGpuHandle gpuHandle, int coolerIndex,
        ref NvGpuCoolerLevels gpuCoolerLevels);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetMemoryInfoDelegate(
        NvPhysicalGpuHandle gpuHandle, ref NvGpuMemoryInfo memoryInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGetDisplayDriverMemoryInfoDelegate(
        NvDisplayHandle displayHandle, ref NvDisplayDriverMemoryInfo memoryInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGetDisplayDriverVersionDelegate(
        NvDisplayHandle displayHandle, [In] [Out] ref NvDisplayDriverVersion
            displayDriverVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGetInterfaceVersionStringDelegate(
        StringBuilder version);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetPciIdentifiersDelegate(
        NvPhysicalGpuHandle gpuHandle, out uint deviceId, out uint subSystemId,
        out uint revisionId, out uint extDeviceId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuGetBusIdDelegate(
        NvPhysicalGpuHandle gpuHandle, out uint busId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvStatus NvApiGpuClientFanCoolersGetStatusDelegate(
        NvPhysicalGpuHandle gpuHandle, ref NvFanCoolersStatus fanCoolersStatus);

    private static readonly bool Available;
    private static readonly NvapiQueryInterfaceDelegate NvapiQueryInterface;
    private static readonly NvApiInitializeDelegate NvApiInitialize;

    private static readonly NvApiGpuGetFullNameDelegate
        NvApiGpuGetFullName;

    private static readonly NvApiGetInterfaceVersionStringDelegate
        NvApiGetInterfaceVersionString;

    public static readonly NvApiGpuGetThermalSettingsDelegate
        NvApiGpuGetThermalSettings;

    public static readonly NvApiEnumNvidiaDisplayHandleDelegate
        NvApiEnumNvidiaDisplayHandle;

    public static readonly NvApiGetPhysicalGpUsFromDisplayDelegate
        NvApiGetPhysicalGpUsFromDisplay;

    public static readonly NvApiEnumPhysicalGpUsDelegate
        NvApiEnumPhysicalGpUs;

    public static readonly NvApiGpuGetTachReadingDelegate
        NvApiGpuGetTachReading;

    public static readonly NvApiGpuGetAllClocksDelegate
        NvApiGpuGetAllClocks;

    public static readonly NvApiGpuGetDynamicPstatesInfoExDelegate
        NvApiGpuGetDynamicPstatesInfoEx;

    public static readonly NvApiGpuGetDynamicPstatesInfoDelegate
        NvApiGpuGetDynamicPstatesInfo;

    public static readonly NvApiGpuGetCoolerSettingsDelegate
        NvApiGpuGetCoolerSettings;

    public static readonly NvApiGpuSetCoolerLevelsDelegate
        NvApiGpuSetCoolerLevels;

    public static readonly NvApiGpuGetMemoryInfoDelegate
        NvApiGpuGetMemoryInfo;

    public static readonly NvApiGetDisplayDriverMemoryInfoDelegate
        NvApiGetDisplayDriverMemoryInfo;

    public static readonly NvApiGetDisplayDriverVersionDelegate
        NvApiGetDisplayDriverVersion;

    public static readonly NvApiGpuGetPciIdentifiersDelegate
        NvApiGpuGetPciIdentifiers;

    public static readonly NvApiGpuGetBusIdDelegate
        NvApiGpuGetBusId;

    public static readonly NvApiGpuClientFanCoolersGetStatusDelegate
        NvApiGpuClientFanCoolersGetStatus;

    private Nvapi()
    {
    }

    public static NvStatus NvAPI_GPU_GetFullName(NvPhysicalGpuHandle gpuHandle,
        out string name)
    {
        var builder = new StringBuilder(ShortStringMax);
        NvStatus status;
        if (NvApiGpuGetFullName != null)
            status = NvApiGpuGetFullName(gpuHandle, builder);
        else
            status = NvStatus.FUNCTION_NOT_FOUND;
        name = builder.ToString();
        return status;
    }

    public static NvStatus NvAPI_GetInterfaceVersionString(out string version)
    {
        var builder = new StringBuilder(ShortStringMax);
        NvStatus status;
        if (NvApiGetInterfaceVersionString != null)
            status = NvApiGetInterfaceVersionString(builder);
        else
            status = NvStatus.FUNCTION_NOT_FOUND;
        version = builder.ToString();
        return status;
    }

    private static string GetDllName()
    {
        if (IntPtr.Size == 4)
            return "nvapi.dll";
        else
            return "nvapi64.dll";
    }

    private static void GetDelegate<T>(uint id, out T newDelegate)
        where T : class
    {
        var ptr = NvapiQueryInterface(id);
        if (ptr != IntPtr.Zero)
            newDelegate =
                Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        else
            newDelegate = null;
    }

    static Nvapi()
    {
        var attribute = new DllImportAttribute(GetDllName());
        attribute.CallingConvention = CallingConvention.Cdecl;
        attribute.PreserveSig = true;
        attribute.EntryPoint = "nvapi_QueryInterface";
        PInvokeDelegateFactory.CreateDelegate(attribute,
            out NvapiQueryInterface);

        try
        {
            GetDelegate(0x0150E828, out NvApiInitialize);
        }
        catch (DllNotFoundException)
        {
            return;
        }
        catch (EntryPointNotFoundException)
        {
            return;
        }
        catch (ArgumentNullException)
        {
            return;
        }

        if (NvApiInitialize() == NvStatus.OK)
        {
            GetDelegate(0xE3640A56, out NvApiGpuGetThermalSettings);
            GetDelegate(0xCEEE8E9F, out NvApiGpuGetFullName);
            GetDelegate(0x9ABDD40D, out NvApiEnumNvidiaDisplayHandle);
            GetDelegate(0x34EF9506, out NvApiGetPhysicalGpUsFromDisplay);
            GetDelegate(0xE5AC921F, out NvApiEnumPhysicalGpUs);
            GetDelegate(0x5F608315, out NvApiGpuGetTachReading);
            GetDelegate(0x1BD69F49, out NvApiGpuGetAllClocks);
            GetDelegate(0x60DED2ED, out NvApiGpuGetDynamicPstatesInfoEx);
            GetDelegate(0x189A1FDF, out NvApiGpuGetDynamicPstatesInfo);
            GetDelegate(0xDA141340, out NvApiGpuGetCoolerSettings);
            GetDelegate(0x891FA0AE, out NvApiGpuSetCoolerLevels);
            GetDelegate(0x07F9B368, out NvApiGpuGetMemoryInfo);
            GetDelegate(0x774AA982, out NvApiGetDisplayDriverMemoryInfo);
            GetDelegate(0xF951A4D1, out NvApiGetDisplayDriverVersion);
            GetDelegate(0x01053FA5, out NvApiGetInterfaceVersionString);
            GetDelegate(0x2DDFB66E, out NvApiGpuGetPciIdentifiers);
            GetDelegate(0x1BE0B8E5, out NvApiGpuGetBusId);
            GetDelegate(0x35AED5E8, out NvApiGpuClientFanCoolersGetStatus);

            Available = true;
        }
    }

    public static bool IsAvailable => Available;
}
