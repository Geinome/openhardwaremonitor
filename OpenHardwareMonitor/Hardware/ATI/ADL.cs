/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware.ATI;

[StructLayout(LayoutKind.Sequential)]
internal struct AdlAdapterInfo
{
    public int Size;
    public int AdapterIndex;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string UDID;

    public int BusNumber;
    public int DeviceNumber;
    public int FunctionNumber;
    public int VendorID;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string AdapterName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string DisplayName;

    public int Present;
    public int Exist;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string DriverPath;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string DriverPathExt;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Adl.AdlMaxPath)]
    public string PNPString;

    public int OSDisplayIndex;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlpmActivity
{
    public int Size;
    public int EngineClock;
    public int MemoryClock;
    public int Vddc;
    public int ActivityPercent;
    public int CurrentPerformanceLevel;
    public int CurrentBusSpeed;
    public int CurrentBusLanes;
    public int MaximumBusLanes;
    public int Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlTemperature
{
    public int Size;
    public int Temperature;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlFanSpeedValue
{
    public int Size;
    public int SpeedType;
    public int FanSpeed;
    public int Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlFanSpeedInfo
{
    public int Size;
    public int Flags;
    public int MinPercent;
    public int MaxPercent;
    public int MinRPM;
    public int MaxRPM;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlSingleSensorData
{
    public bool Supported;
    public int Value;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlpmLogDataOutput
{
    public int Size;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = Adl.AdlPmlogMaxSensors)]
    public AdlSingleSensorData[] Sensors;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlodParameterRange
{
    public int Min;
    public int Max;
    public int Step;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlodParameters
{
    public int Size;
    public int NumberOfPerformanceLevels;
    public int ActivityReportingSupported;
    public int DiscretePerformanceLevels;
    public int Reserved;
    public AdlodParameterRange EngineClock;
    public AdlodParameterRange MemoryClock;
    public AdlodParameterRange Vddc;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlodnPerformanceStatus
{
    public int CoreClock;
    public int MemoryClock;
    public int DCEFClock;
    public int GFXClock;
    public int UVDClock;
    public int VCEClock;
    public int GPUActivityPercent;
    public int CurrentCorePerformanceLevel;
    public int CurrentMemoryPerformanceLevel;
    public int CurrentDCEFPerformanceLevel;
    public int CurrentGFXPerformanceLevel;
    public int UVDPerformanceLevel;
    public int VCEPerformanceLevel;
    public int CurrentBusSpeed;
    public int CurrentBusLanes;
    public int MaximumBusLanes;
    public int VDDC;
    public int VDDCI;
}

[StructLayout(LayoutKind.Sequential)]
internal struct AdlVersionsInfo
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string DriverVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string CatalystVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string CatalystWebLink;
}

internal enum AdlodnCurrentPowerType
{
    TOTAL_POWER = 0,
    PPT_POWER,
    SOCKET_POWER,
    CHIP_POWER
}

internal enum AdlodnTemperatureType
{
    CORE = 1,
    MEMORY = 2,
    VRM_CORE = 3,
    VRM_MEMORY = 4,
    LIQUID = 5,
    PLX = 6,
    HOTSPOT = 7
}

internal enum AdlSensorType
{
    CLK_GFXCLK = 1,
    CLK_MEMCLK = 2,
    CLK_SOCCLK = 3,
    CLK_UVDCLK1 = 4,
    CLK_UVDCLK2 = 5,
    CLK_VCECLK = 6,
    CLK_VCNCLK = 7,
    TEMPERATURE_EDGE = 8,
    TEMPERATURE_MEM = 9,
    TEMPERATURE_VRVDDC = 10,
    TEMPERATURE_VRMVDD = 11,
    TEMPERATURE_LIQUID = 12,
    TEMPERATURE_PLX = 13,
    FAN_RPM = 14,
    FAN_PERCENTAGE = 15,
    SOC_VOLTAGE = 16,
    SOC_POWER = 17,
    SOC_CURRENT = 18,
    INFO_ACTIVITY_GFX = 19,
    INFO_ACTIVITY_MEM = 20,
    GFX_VOLTAGE = 21,
    MEM_VOLTAGE = 22,
    ASIC_POWER = 23,
    TEMPERATURE_VRSOC = 24,
    TEMPERATURE_VRMVDD0 = 25,
    TEMPERATURE_VRMVDD1 = 26,
    TEMPERATURE_HOTSPOT = 27,
    TEMPERATURE_GFX = 28,
    TEMPERATURE_SOC = 29,
    GFX_POWER = 30,
    GFX_CURRENT = 31,
    TEMPERATURE_CPU = 32,
    CPU_POWER = 33,
    CLK_CPUCLK = 34,
    THROTTLER_STATUS = 35,
    ClkVcn1Clk1 = 36,
    ClkVcn1Clk2 = 37,
    SMART_POWERSHIFT_CPU = 38,
    SMART_POWERSHIFT_DGPU = 39
}

internal enum AdlStatus : int
{
    /// <summary>
    /// All OK, but need to wait.
    /// </summary>  
    OK_WAIT = 4,

    /// <summary>
    /// All OK, but need restart.
    /// </summary>  
    OK_RESTART = 3,

    /// <summary>
    /// All OK but need mode change.
    /// </summary>
    OK_MODE_CHANGE = 2,

    /// <summary>
    /// All OK, but with warning.
    /// </summary>
    OK_WARNING = 1,

    /// <summary>
    /// ADL function completed successfully.
    /// </summary>
    OK = 0,

    /// <summary>
    /// Generic Error. Most likely one or more of the Escape calls to the driver 
    /// failed!
    /// </summary>
    ERR = -1,

    /// <summary>
    /// ADL not initialized.
    /// </summary>
    ERR_NOT_INIT = -2,

    /// <summary>
    /// One of the parameter passed is invalid.
    /// </summary>
    ERR_INVALID_PARAM = -3,

    /// <summary>
    /// One of the parameter size is invalid.
    /// </summary>
    ERR_INVALID_PARAM_SIZE = -4,

    /// <summary>
    /// Invalid ADL index passed.
    /// </summary>
    ERR_INVALID_ADL_IDX = -5,

    /// <summary>
    /// Invalid controller index passed.
    /// </summary>
    ERR_INVALID_CONTROLLER_IDX = -6,

    /// <summary>
    /// Invalid display index passed.
    /// </summary>
    ERR_INVALID_DIPLAY_IDX = -7,

    /// <summary>
    /// Function not supported by the driver.
    /// </summary>
    ERR_NOT_SUPPORTED = -8,

    /// <summary>
    /// Null Pointer error.
    /// </summary>
    ERR_NULL_POINTER = -9,

    /// <summary>
    /// Call can't be made due to disabled adapter.
    /// </summary>
    ERR_DISABLED_ADAPTER = -10,

    /// <summary>
    /// Invalid Callback.
    /// </summary>
    ERR_INVALID_CALLBACK = -11,

    /// <summary>
    /// Display Resource conflict.
    /// </summary>
    ERR_RESOURCE_CONFLICT = -12,

    /// <summary>
    /// Failed to update some of the values. Can be returned by set request that 
    /// include multiple values if not all values were successfully committed.
    /// </summary>
    ERR_SET_INCOMPLETE = -20,

    /// <summary>
    /// There's no Linux XDisplay in Linux Console environment. 
    /// </summary>
    ERR_NO_XDISPLAY = -21
}

internal class Adl
{
    public const int AdlMaxPath = 256;
    public const int AdlMaxAdapters = 40;
    public const int AdlMaxDisplays = 40;
    public const int AdlMaxDevicename = 32;
    public const int AdlDriverOk = 0;
    public const int AdlMaxGlsyncPorts = 8;
    public const int AdlMaxGlsyncPortLeds = 8;
    public const int AdlMaxNumDisplaymodes = 1024;
    public const int AdlPmlogMaxSensors = 256;

    public const int AdlDlFanctrlSpeedTypePercent = 1;
    public const int AdlDlFanctrlSpeedTypeRpm = 2;

    public const int AdlDlFanctrlSupportsPercentRead = 1;
    public const int AdlDlFanctrlSupportsPercentWrite = 2;
    public const int AdlDlFanctrlSupportsRpmRead = 4;
    public const int AdlDlFanctrlSupportsRpmWrite = 8;
    public const int AdlDlFanctrlFlagUserDefinedSpeed = 1;

    public const int AtiVendorId = 0x1002;

    private delegate AdlStatus AdlMainControlCreateDelegate(
        AdlMainMemoryAllocDelegate callback, int enumConnectedAdapters);

    private delegate AdlStatus AdlAdapterAdapterInfoGetDelegate(IntPtr info,
        int size);

    public delegate AdlStatus AdlMainControlDestroyDelegate();

    public delegate AdlStatus AdlAdapterNumberOfAdaptersGetDelegate(
        ref int numAdapters);

    public delegate AdlStatus AdlAdapterIdGetDelegate(int adapterIndex,
        out int adapterId);

    public delegate AdlStatus AdlDisplayAdapterIdGetDelegate(int adapterIndex,
        out int adapterId);

    public delegate int AdlAdapterActiveGetDelegate(int adapterIndex,
        out int status);

    public delegate AdlStatus AdlOverdrive5CurrentActivityGetDelegate(
        int iAdapterIndex, ref AdlpmActivity activity);

    public delegate AdlStatus AdlOverdrive5TemperatureGetDelegate(int adapterIndex,
        int thermalControllerIndex, ref AdlTemperature temperature);

    public delegate AdlStatus AdlOverdrive5FanSpeedGetDelegate(int adapterIndex,
        int thermalControllerIndex, ref AdlFanSpeedValue fanSpeedValue);

    public delegate AdlStatus AdlOverdrive5FanSpeedInfoGetDelegate(
        int adapterIndex, int thermalControllerIndex,
        ref AdlFanSpeedInfo fanSpeedInfo);

    public delegate AdlStatus AdlOverdrive5FanSpeedToDefaultSetDelegate(
        int adapterIndex, int thermalControllerIndex);

    public delegate AdlStatus AdlOverdrive5FanSpeedSetDelegate(int adapterIndex,
        int thermalControllerIndex, ref AdlFanSpeedValue fanSpeedValue);

    public delegate AdlStatus AdlOverdriveCapsDelegate(int adapterIndex,
        out int supported, out int enabled, out int version);

    private delegate AdlStatus Adl2MainControlCreateDelegate(
        AdlMainMemoryAllocDelegate callback, int enumConnectedAdapters,
        out IntPtr context);

    public delegate AdlStatus Adl2MainControlDestroyDelegate(IntPtr context);

    public delegate AdlStatus Adl2OverdriveNTemperatureGetDelegate(IntPtr context,
        int adapterIndex, AdlodnTemperatureType temperatureType,
        out int temperature);

    public delegate AdlStatus Adl2Overdrive6CurrentPowerGetDelegate(IntPtr context,
        int adapterIndex, AdlodnCurrentPowerType powerType,
        out int currentValue);

    public delegate AdlStatus Adl2NewQueryPmLogDataGetDelegate(IntPtr context,
        int adapterIndex, out AdlpmLogDataOutput dataOutput);

    public delegate AdlStatus AdlOverdrive5OdParametersGetDelegate(
        int adapterIndex, out AdlodParameters parameters);

    public delegate AdlStatus Adl2OverdriveNPerformanceStatusGetDelegate(
        IntPtr context, int adapterIndex,
        out AdlodnPerformanceStatus performanceStatus);

    public delegate AdlStatus AdlGraphicsVersionsGetDelegate(
        out AdlVersionsInfo versionInfo);

    private static AdlMainControlCreateDelegate
        _adlMainControlCreate;

    private static AdlAdapterAdapterInfoGetDelegate
        _adlAdapterAdapterInfoGet;

    public static AdlMainControlDestroyDelegate
        AdlMainControlDestroy;

    public static AdlAdapterNumberOfAdaptersGetDelegate
        AdlAdapterNumberOfAdaptersGet;

    public static AdlAdapterIdGetDelegate
        AdlAdapterIdGet;

    public static AdlDisplayAdapterIdGetDelegate
        AdlDisplayAdapterIdGet;

    public static AdlAdapterActiveGetDelegate
        AdlAdapterActiveGet;

    public static AdlOverdrive5CurrentActivityGetDelegate
        AdlOverdrive5CurrentActivityGet;

    public static AdlOverdrive5TemperatureGetDelegate
        AdlOverdrive5TemperatureGet;

    public static AdlOverdrive5FanSpeedGetDelegate
        AdlOverdrive5FanSpeedGet;

    public static AdlOverdrive5FanSpeedInfoGetDelegate
        AdlOverdrive5FanSpeedInfoGet;

    public static AdlOverdrive5FanSpeedToDefaultSetDelegate
        AdlOverdrive5FanSpeedToDefaultSet;

    public static AdlOverdrive5FanSpeedSetDelegate
        AdlOverdrive5FanSpeedSet;

    public static AdlOverdriveCapsDelegate
        AdlOverdriveCaps;

    private static Adl2MainControlCreateDelegate
        _adl2MainControlCreate;

    public static Adl2MainControlDestroyDelegate
        Adl2MainControlDestroy;

    public static Adl2OverdriveNTemperatureGetDelegate
        Adl2OverdriveNTemperatureGet;

    public static Adl2Overdrive6CurrentPowerGetDelegate
        Adl2Overdrive6CurrentPowerGet;

    public static Adl2NewQueryPmLogDataGetDelegate
        Adl2NewQueryPmLogDataGet;

    public static AdlOverdrive5OdParametersGetDelegate
        AdlOverdrive5OdParametersGet;

    public static Adl2OverdriveNPerformanceStatusGetDelegate
        Adl2OverdriveNPerformanceStatusGet;

    public static AdlGraphicsVersionsGetDelegate
        AdlGraphicsVersionsGet;

    private static string _dllName;

    private static void GetDelegate<T>(string entryPoint, out T newDelegate)
        where T : class
    {
        var attribute = new DllImportAttribute(_dllName);
        attribute.CallingConvention = CallingConvention.Cdecl;
        attribute.PreserveSig = true;
        attribute.EntryPoint = entryPoint;
        PInvokeDelegateFactory.CreateDelegate(attribute, out newDelegate);
    }

    private static void CreateDelegates(string name)
    {
        if (OperatingSystem.IsUnix)
            _dllName = name + ".so";
        else
            _dllName = name + ".dll";

        GetDelegate("ADL_Main_Control_Create",
            out _adlMainControlCreate);
        GetDelegate("ADL_Adapter_AdapterInfo_Get",
            out _adlAdapterAdapterInfoGet);
        GetDelegate("ADL_Main_Control_Destroy",
            out AdlMainControlDestroy);
        GetDelegate("ADL_Adapter_NumberOfAdapters_Get",
            out AdlAdapterNumberOfAdaptersGet);
        GetDelegate("ADL_Adapter_ID_Get",
            out AdlAdapterIdGet);
        GetDelegate("ADL_Display_AdapterID_Get",
            out AdlDisplayAdapterIdGet);
        GetDelegate("ADL_Adapter_Active_Get",
            out AdlAdapterActiveGet);
        GetDelegate("ADL_Overdrive5_CurrentActivity_Get",
            out AdlOverdrive5CurrentActivityGet);
        GetDelegate("ADL_Overdrive5_Temperature_Get",
            out AdlOverdrive5TemperatureGet);
        GetDelegate("ADL_Overdrive5_FanSpeed_Get",
            out AdlOverdrive5FanSpeedGet);
        GetDelegate("ADL_Overdrive5_FanSpeedInfo_Get",
            out AdlOverdrive5FanSpeedInfoGet);
        GetDelegate("ADL_Overdrive5_FanSpeedToDefault_Set",
            out AdlOverdrive5FanSpeedToDefaultSet);
        GetDelegate("ADL_Overdrive5_FanSpeed_Set",
            out AdlOverdrive5FanSpeedSet);
        GetDelegate("ADL_Overdrive_Caps",
            out AdlOverdriveCaps);
        GetDelegate("ADL2_Main_Control_Create",
            out _adl2MainControlCreate);
        GetDelegate("ADL2_Main_Control_Destroy",
            out Adl2MainControlDestroy);
        GetDelegate("ADL2_OverdriveN_Temperature_Get",
            out Adl2OverdriveNTemperatureGet);
        GetDelegate("ADL2_Overdrive6_CurrentPower_Get",
            out Adl2Overdrive6CurrentPowerGet);
        GetDelegate("ADL2_New_QueryPMLogData_Get",
            out Adl2NewQueryPmLogDataGet);
        GetDelegate("ADL_Overdrive5_ODParameters_Get",
            out AdlOverdrive5OdParametersGet);
        GetDelegate("ADL2_OverdriveN_PerformanceStatus_Get",
            out Adl2OverdriveNPerformanceStatusGet);
        GetDelegate("ADL_Graphics_Versions_Get",
            out AdlGraphicsVersionsGet);
    }

    static Adl()
    {
        CreateDelegates("atiadlxx");
    }

    private Adl()
    {
    }

    public static AdlStatus ADL_Main_Control_Create(int enumConnectedAdapters)
    {
        try
        {
            try
            {
                return _adlMainControlCreate(_mainMemoryAlloc,
                    enumConnectedAdapters);
            }
            catch (Exception x)
            {
                Logging.LogError(x, "Unable to open ADL_Main_Control using atiadlxx");
                CreateDelegates("atiadlxy");
                return _adlMainControlCreate(_mainMemoryAlloc,
                    enumConnectedAdapters);
            }
        }
        catch (Exception x)
        {
            Logging.LogError(x, "Unable to open ADL_Main_Control using atiadlxy");
            return AdlStatus.ERR;
        }
    }

    public static AdlStatus ADL2_Main_Control_Create(int enumConnectedAdapters,
        out IntPtr context)
    {
        try
        {
            var result = _adl2MainControlCreate(_mainMemoryAlloc,
                enumConnectedAdapters, out context);
            if (result != AdlStatus.OK)
                context = IntPtr.Zero;
            return result;
        }
        catch (Exception x)
        {
            Logging.LogError(x, "Unable to open ADL2_Main_Control");
            context = IntPtr.Zero;
            return AdlStatus.ERR;
        }
    }

    public static AdlStatus ADL_Adapter_AdapterInfo_Get(AdlAdapterInfo[] info)
    {
        var elementSize = Marshal.SizeOf(typeof(AdlAdapterInfo));
        var size = info.Length * elementSize;
        var ptr = Marshal.AllocHGlobal(size);
        var status = _adlAdapterAdapterInfoGet(ptr, size);
        for (var i = 0; i < info.Length; i++)
            info[i] = (AdlAdapterInfo)
                Marshal.PtrToStructure((IntPtr)((long)ptr + i * elementSize),
                    typeof(AdlAdapterInfo));
        Marshal.FreeHGlobal(ptr);

        // the ADLAdapterInfo.VendorID field reported by ADL is wrong on 
        // Windows systems (parse error), so we fix this here
        for (var i = 0; i < info.Length; i++)
        {
            // try Windows UDID format
            var m = Regex.Match(info[i].UDID, "PCI_VEN_([A-Fa-f0-9]{1,4})&.*");
            if (m.Success && m.Groups.Count == 2)
            {
                info[i].VendorID = Convert.ToInt32(m.Groups[1].Value, 16);
                continue;
            }

            // if above failed, try Unix UDID format
            m = Regex.Match(info[i].UDID, "[0-9]+:[0-9]+:([0-9]+):[0-9]+:[0-9]+");
            if (m.Success && m.Groups.Count == 2) info[i].VendorID = Convert.ToInt32(m.Groups[1].Value, 10);
        }

        return status;
    }

    public static AdlStatus ADL_Adapter_ID_Get(int adapterIndex,
        out int adapterId)
    {
        try
        {
            return AdlAdapterIdGet(adapterIndex, out adapterId);
        }
        catch (EntryPointNotFoundException e1)
        {
            Logging.LogError(e1, "Unable to get entry point for ADL_Adapter_ID_Get");
            try
            {
                return AdlDisplayAdapterIdGet(adapterIndex, out adapterId);
            }
            catch (EntryPointNotFoundException e2)
            {
                Logging.LogError(e2, "Unable to get entry point for ADL_Display_AdapterID_Get");
                adapterId = 1;
                return AdlStatus.OK;
            }
        }
    }

    private delegate IntPtr AdlMainMemoryAllocDelegate(int size);

    // create a Main_Memory_Alloc delegate and keep it alive
    private static AdlMainMemoryAllocDelegate _mainMemoryAlloc =
        delegate(int size) { return Marshal.AllocHGlobal(size); };

    private static void Main_Memory_Free(IntPtr buffer)
    {
        if (IntPtr.Zero != buffer)
            Marshal.FreeHGlobal(buffer);
    }
}
