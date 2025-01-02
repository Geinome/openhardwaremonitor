/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.Nvidia;

internal class Nvml
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlInitDelegate();

    private static readonly NvmlInitDelegate NvmlInitEx;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NvmlReturn NvmlInitV2Delegate();

    private static readonly NvmlInitV2Delegate NvmlInitV2;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvmlReturn NvmlShutdownDelegate();

    public static readonly NvmlShutdownDelegate NvmlShutdown;

    public delegate NvmlReturn NvmlDeviceGetHandleByPciBusIdDelegate(
        string pciBusId, out NvmlDevice device);

    public static readonly NvmlDeviceGetHandleByPciBusIdDelegate
        NvmlDeviceGetHandleByPciBusId;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvmlReturn NvmlDeviceGetPowerUsageDelegate(
        NvmlDevice device, out int power);

    public static readonly NvmlDeviceGetPowerUsageDelegate
        NvmlDeviceGetPowerUsage;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate NvmlReturn NvmlDeviceGetPcieThroughputDelegate(
        NvmlDevice devicee, NvmlPcieUtilCounter counter, out uint value);

    public static readonly NvmlDeviceGetPcieThroughputDelegate
        NvmlDeviceGetPcieThroughput;


    public static NvmlReturn NvmlInit()
    {
        try
        {
            var result = NvmlInitV2();
            _initialized = result == NvmlReturn.Success;
            return result;
        }
        catch
        {
            try
            {
                var result = NvmlInitEx();
                _initialized = result == NvmlReturn.Success;
                return result;
            }
            catch
            {
                return NvmlReturn.ErrorLibraryNotFound;
            }
        }
    }

    private static string GetDllName()
    {
        if (OperatingSystem.IsUnix)
            return "libnvidia-ml.so";
        else
            return "nvml.dll";
    }

    private static T CreateDelegate<T>(string entryPoint) where T : Delegate
    {
        var attribute = new DllImportAttribute(GetDllName())
        {
            CallingConvention = CallingConvention.Cdecl,
            PreserveSig = true,
            EntryPoint = entryPoint
        };
        PInvokeDelegateFactory.CreateDelegate(attribute, out T newDelegate);
        return newDelegate;
    }

    static Nvml()
    {
        NvmlInitEx =
            CreateDelegate<NvmlInitDelegate>(
                "nvmlInit");
        NvmlInitV2 =
            CreateDelegate<NvmlInitV2Delegate>(
                "nvmlInit_v2");
        NvmlShutdown =
            CreateDelegate<NvmlShutdownDelegate>(
                "nvmlShutdown");
        NvmlDeviceGetHandleByPciBusId =
            CreateDelegate<NvmlDeviceGetHandleByPciBusIdDelegate>(
                "nvmlDeviceGetHandleByPciBusId_v2");
        NvmlDeviceGetPowerUsage =
            CreateDelegate<NvmlDeviceGetPowerUsageDelegate>(
                "nvmlDeviceGetPowerUsage");
        NvmlDeviceGetPcieThroughput =
            CreateDelegate<NvmlDeviceGetPcieThroughputDelegate>(
                "nvmlDeviceGetPcieThroughput");
    }

    private static bool _initialized;
    public static bool IsInitialized => _initialized;

    [StructLayout(LayoutKind.Sequential)]
    internal struct NvmlDevice
    {
        private readonly IntPtr ptr;
    }

    internal enum NvmlPcieUtilCounter
    {
        TxBytes = 0,
        RxBytes = 1
    }

    internal enum NvmlReturn
    {
        /// <summary>
        /// The operation was successful
        /// </summary>
        Success = 0,

        /// <summary>
        /// NVML was not first initialized with nvmlInit()
        /// </summary>
        ErrorUninitialized = 1,

        /// <summary>
        /// A supplied argument is invalid
        /// </summary>
        ErrorInvalidArgument = 2,

        /// <summary>
        /// The requested operation is not available on target device
        /// </summary>
        ErrorNotSupported = 3,

        /// <summary>
        /// The current user does not have permission for operation
        /// </summary>
        ErrorNoPermission = 4,

        /// <summary>
        /// Deprecated: Multiple initializations are now allowed through ref
        /// counting
        /// </summary>
        ErrorAlreadyInitialized = 5,

        /// <summary>
        /// A query to find an object was unsuccessful
        /// </summary>
        ErrorNotFound = 6,

        /// <summary>
        /// An input argument is not large enough
        /// </summary>
        ErrorInsufficientSize = 7,

        /// <summary>
        /// A device's external power cables are not properly attached
        /// </summary>
        ErrorInsufficientPower = 8,

        /// <summary>
        /// NVIDIA driver is not loaded
        /// </summary>
        ErrorDriverNotLoaded = 9,

        /// <summary>
        /// User provided timeout passed
        /// </summary>
        ErrorTimeout = 10,

        /// <summary>
        /// NVIDIA Kernel detected an interrupt issue with a GPU
        /// </summary>
        ErrorIrqIssue = 11,

        /// <summary>
        /// NVML Shared Library couldn't be found or loaded
        /// </summary>
        ErrorLibraryNotFound = 12,

        /// <summary>
        /// Local version of NVML doesn't implement this function
        /// </summary>
        ErrorFunctionNotFound = 13,

        /// <summary>
        /// infoROM is corruptedinfoROM is corrupted
        /// </summary>
        ErrorCorruptedInforom = 14,

        /// <summary>
        /// The GPU has fallen off the bus or has otherwise become inaccessible
        /// </summary>
        ErrorGpuIsLost = 15,

        /// <summary>
        /// The GPU requires a reset before it can be used again
        /// </summary>
        ErrorResetRequired = 16,

        /// <summary>
        /// The GPU control device has been blocked by the operating
        /// system/cgroups
        /// </summary>
        ErrorOperatingSystem = 17,

        /// <summary>
        /// RM detects a driver/library version mismatch
        /// </summary>
        ErrorLibRmVersionMismatch = 18,

        /// <summary>
        /// An operation cannot be performed because the GPU is currently in use
        /// </summary>
        ErrorInUse = 19,

        /// <summary>
        /// Insufficient memory
        /// </summary>
        ErrorMemory = 20,

        /// <summary>
        /// No data
        /// </summary>
        ErrorNoData = 21,

        /// <summary>
        /// The requested vgpu operation is not available on target device,
        /// becasue ECC is enabled
        /// </summary>
        ErrorvGpuEccNotSupported = 22,

        /// <summary>
        /// An internal driver error occurred
        /// </summary>
        ErrorUnknown = 999
    };
}
