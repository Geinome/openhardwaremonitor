/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2010-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Text;
using OpenHardwareMonitor;

namespace OpenHardwareMonitor.Hardware;

internal static class Ring0
{
    private static KernelDriver _driver;
    private static string _fileName;
    private static Mutex _isaBusMutex;
    private static Mutex _pciBusMutex;
    private static readonly StringBuilder Report = new();

    private const uint OlsType = 40000;

    private static IoControlCode
        _ioctlOlsGetRefcount = new(OlsType, 0x801,
            IoControlCode.Access.Any),
        _ioctlOlsGetDriverVersion = new(OlsType, 0x800,
            IoControlCode.Access.Any),
        _ioctlOlsReadMsr = new(OlsType, 0x821,
            IoControlCode.Access.Any),
        _ioctlOlsWriteMsr = new(OlsType, 0x822,
            IoControlCode.Access.Any),
        _ioctlOlsReadIoPortByte = new(OlsType, 0x833,
            IoControlCode.Access.Read),
        _ioctlOlsWriteIoPortByte = new(OlsType, 0x836,
            IoControlCode.Access.Write),
        _ioctlOlsReadPciConfig = new(OlsType, 0x851,
            IoControlCode.Access.Read),
        _ioctlOlsWritePciConfig = new(OlsType, 0x852,
            IoControlCode.Access.Write),
        _ioctlOlsReadMemory = new(OlsType, 0x841,
            IoControlCode.Access.Read);

    private static Assembly GetAssembly()
    {
        return typeof(Ring0).Assembly;
    }

    private static string GetTempFileName()
    {
        // try to create one in the application folder
        var location = GetAssembly().Location;
        if (!string.IsNullOrEmpty(location))
            try
            {
                var fileName = Path.ChangeExtension(location, ".sys");
                using (var stream = File.Create(fileName))
                {
                    return fileName;
                }
            }
            catch (Exception)
            {
            }

        // if this failed, try to get a file in the temporary folder
        try
        {
            return Path.GetTempFileName();
        }
        catch (IOException)
        {
            // some I/O exception
        }
        catch (UnauthorizedAccessException)
        {
            // we do not have the right to create a file in the temp folder
        }
        catch (NotSupportedException)
        {
            // invalid path format of the TMP system environment variable
        }

        return null;
    }

    private static bool ExtractDriver(string fileName)
    {
        var resourceName = "OpenHardwareMonitor.Ui.Hardware." +
                           (OperatingSystem.Is64BitOperatingSystem ? "WinRing0x64.sys" : "WinRing0.sys");

        var names = GetAssembly().GetManifestResourceNames();
        byte[] buffer = null;
        for (var i = 0; i < names.Length; i++)
            if (names[i].Replace('\\', '.') == resourceName)
                using (var stream = GetAssembly().GetManifestResourceStream(names[i]))
                {
                    buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                }

        if (buffer == null)
            return false;

        try
        {
            using (var target = new FileStream(fileName, FileMode.Create))
            {
                target.Write(buffer, 0, buffer.Length);
                target.Flush();
            }
        }
        catch (IOException)
        {
            // for example there is not enough space on the disk
            return false;
        }

        // make sure the file is actually writen to the file system
        for (var i = 0; i < 20; i++)
            try
            {
                if (File.Exists(fileName) &&
                    new FileInfo(fileName).Length == buffer.Length)
                    return true;
                Thread.Sleep(100);
            }
            catch (IOException)
            {
                Thread.Sleep(10);
            }

        // file still has not the right size, something is wrong
        return false;
    }

    public static void Open()
    {
        // no implementation for unix systems
        if (OperatingSystem.IsUnix)
            return;

        if (_driver != null)
            return;

        // clear the current report
        Report.Length = 0;

        _driver = new KernelDriver("WinRing0_1_2_0");
        _driver.Open();

        if (!_driver.IsOpen)
        {
            // driver is not loaded, try to install and open

            _fileName = GetTempFileName();
            if (_fileName != null && ExtractDriver(_fileName))
            {
                string installError;
                if (_driver.Install(_fileName, out installError))
                {
                    _driver.Open();

                    if (!_driver.IsOpen)
                    {
                        _driver.Delete();
                        Report.AppendLine("Status: Opening driver failed after install");
                    }
                }
                else
                {
                    var errorFirstInstall = installError;

                    // install failed, try to delete and reinstall
                    _driver.Delete();

                    // wait a short moment to give the OS a chance to remove the driver
                    Thread.Sleep(2000);

                    string errorSecondInstall;
                    if (_driver.Install(_fileName, out errorSecondInstall))
                    {
                        _driver.Open();

                        if (!_driver.IsOpen)
                        {
                            _driver.Delete();
                            Report.AppendLine(
                                "Status: Opening driver failed after reinstall");
                        }
                    }
                    else
                    {
                        Report.AppendLine("Status: Installing driver \"" +
                                          _fileName + "\" failed" +
                                          (File.Exists(_fileName) ? " and file exists" : ""));
                        Report.AppendLine("First Exception: " + errorFirstInstall);
                        Report.AppendLine("Second Exception: " + errorSecondInstall);
                    }
                }
            }
            else
            {
                Report.AppendLine("Status: Extracting driver failed");
            }

            try
            {
                // try to delte the driver file
                if (File.Exists(_fileName))
                    File.Delete(_fileName);
                _fileName = null;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (!_driver.IsOpen)
            _driver = null;

        var isaMutexName = "Global\\Access_ISABUS.HTP.Method";
        try
        {
            _isaBusMutex = new Mutex(false, isaMutexName);
        }
        catch (UnauthorizedAccessException)
        {
        }

        var pciMutexName = "Global\\Access_PCI";
        try
        {
            _pciBusMutex = new Mutex(false, pciMutexName);
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static bool IsOpen => _driver != null;

    public static void Close()
    {
        if (_driver == null)
            return;

        uint refCount = 0;
        _driver.DeviceIoControl(_ioctlOlsGetRefcount, null, ref refCount);

        _driver.Close();

        if (refCount <= 1)
            _driver.Delete();

        _driver = null;

        if (_isaBusMutex != null)
        {
            _isaBusMutex.Close();
            _isaBusMutex = null;
        }

        if (_pciBusMutex != null)
        {
            _pciBusMutex.Close();
            _pciBusMutex = null;
        }

        // try to delete temporary driver file again if failed during open
        if (_fileName != null && File.Exists(_fileName))
            try
            {
                File.Delete(_fileName);
                _fileName = null;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
    }

    public static string GetReport()
    {
        if (Report.Length > 0)
        {
            var r = new StringBuilder();
            r.AppendLine("Ring0");
            r.AppendLine();
            r.Append(Report);
            r.AppendLine();
            return r.ToString();
        }
        else
        {
            return null;
        }
    }

    public static bool WaitIsaBusMutex(int millisecondsTimeout)
    {
        if (_isaBusMutex == null)
            return true;
        try
        {
            return _isaBusMutex.WaitOne(millisecondsTimeout, false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void ReleaseIsaBusMutex()
    {
        if (_isaBusMutex == null)
            return;
        _isaBusMutex.ReleaseMutex();
    }

    public static bool WaitPciBusMutex(int millisecondsTimeout)
    {
        if (_pciBusMutex == null)
            return true;
        try
        {
            return _pciBusMutex.WaitOne(millisecondsTimeout, false);
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static void ReleasePciBusMutex()
    {
        if (_pciBusMutex == null)
            return;
        _pciBusMutex.ReleaseMutex();
    }

    public static bool Rdmsr(uint index, out uint eax, out uint edx)
    {
        if (_driver == null)
        {
            eax = 0;
            edx = 0;
            return false;
        }

        ulong buffer = 0;
        var result = _driver.DeviceIoControl(_ioctlOlsReadMsr, index,
            ref buffer);

        edx = (uint)((buffer >> 32) & 0xFFFFFFFF);
        eax = (uint)(buffer & 0xFFFFFFFF);
        return result;
    }

    public static bool RdmsrTx(uint index, out uint eax, out uint edx,
        GroupAffinity affinity)
    {
        var previousAffinity = ThreadAffinity.Set(affinity);

        var result = Rdmsr(index, out eax, out edx);

        ThreadAffinity.Set(previousAffinity);
        return result;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WrmsrInput
    {
        public uint Register;
        public ulong Value;
    }

    public static bool Wrmsr(uint index, uint eax, uint edx)
    {
        if (_driver == null)
            return false;

        var input = new WrmsrInput();
        input.Register = index;
        input.Value = ((ulong)edx << 32) | eax;

        return _driver.DeviceIoControl(_ioctlOlsWriteMsr, input);
    }

    public static byte ReadIoPort(uint port)
    {
        if (_driver == null)
            return 0;

        uint value = 0;
        _driver.DeviceIoControl(_ioctlOlsReadIoPortByte, port, ref value);

        return (byte)(value & 0xFF);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WriteIoPortInput
    {
        public uint PortNumber;
        public byte Value;
    }

    public static void WriteIoPort(uint port, byte value)
    {
        if (_driver == null)
            return;

        var input = new WriteIoPortInput();
        input.PortNumber = port;
        input.Value = value;

        _driver.DeviceIoControl(_ioctlOlsWriteIoPortByte, input);
    }

    public const uint InvalidPciAddress = 0xFFFFFFFF;

    public static uint GetPciAddress(byte bus, byte device, byte function)
    {
        return
            (uint)(((bus & 0xFF) << 8) | ((device & 0x1F) << 3) | (function & 7));
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadPciConfigInput
    {
        public uint PciAddress;
        public uint RegAddress;
    }

    public static bool ReadPciConfig(uint pciAddress, uint regAddress,
        out uint value)
    {
        if (_driver == null || (regAddress & 3) != 0)
        {
            value = 0;
            return false;
        }

        var input = new ReadPciConfigInput();
        input.PciAddress = pciAddress;
        input.RegAddress = regAddress;

        value = 0;
        return _driver.DeviceIoControl(_ioctlOlsReadPciConfig, input,
            ref value);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct WritePciConfigInput
    {
        public uint PciAddress;
        public uint RegAddress;
        public uint Value;
    }

    public static bool WritePciConfig(uint pciAddress, uint regAddress,
        uint value)
    {
        if (_driver == null || (regAddress & 3) != 0)
            return false;

        var input = new WritePciConfigInput();
        input.PciAddress = pciAddress;
        input.RegAddress = regAddress;
        input.Value = value;

        return _driver.DeviceIoControl(_ioctlOlsWritePciConfig, input);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadMemoryInput
    {
        public ulong address;
        public uint unitSize;
        public uint count;
    }

    public static bool ReadMemory<T>(ulong address, ref T buffer)
    {
        if (_driver == null) return false;

        var input = new ReadMemoryInput();
        input.address = address;
        input.unitSize = 1;
        input.count = (uint)Marshal.SizeOf(buffer);

        return _driver.DeviceIoControl(_ioctlOlsReadMemory, input,
            ref buffer);
    }
}
