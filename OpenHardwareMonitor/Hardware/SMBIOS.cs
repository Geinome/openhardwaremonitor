/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;

namespace OpenHardwareMonitor.Hardware;

internal class Smbios
{
    private readonly byte[] _raw;
    private readonly Structure[] _table;

    private readonly Version _version;
    private readonly BiosInformation _biosInformation;
    private readonly SystemInformation _systemInformation;
    private readonly BaseBoardInformation _baseBoardInformation;
    private readonly ProcessorInformation _processorInformation;
    private readonly MemoryDevice[] _memoryDevices;

    private static string ReadSysFs(string path)
    {
        try
        {
            if (File.Exists(path))
                using (var reader = new StreamReader(path))
                {
                    return reader.ReadLine();
                }
            else
                return null;
        }
        catch
        {
            return null;
        }
    }

    public Smbios()
    {
        if (OperatingSystem.IsUnix)
        {
            _raw = null;
            _table = null;

            var boardVendor = ReadSysFs("/sys/class/dmi/id/board_vendor");
            var boardName = ReadSysFs("/sys/class/dmi/id/board_name");
            var boardVersion = ReadSysFs("/sys/class/dmi/id/board_version");
            _baseBoardInformation = new BaseBoardInformation(
                boardVendor, boardName, boardVersion, null);

            var systemVendor = ReadSysFs("/sys/class/dmi/id/sys_vendor");
            var productName = ReadSysFs("/sys/class/dmi/id/product_name");
            var productVersion = ReadSysFs("/sys/class/dmi/id/product_version");
            _systemInformation = new SystemInformation(systemVendor,
                productName, productVersion, null, null);

            var biosVendor = ReadSysFs("/sys/class/dmi/id/bios_vendor");
            var biosVersion = ReadSysFs("/sys/class/dmi/id/bios_version");
            _biosInformation = new BiosInformation(biosVendor, biosVersion);

            _memoryDevices = new MemoryDevice[0];
        }
        else
        {
            var structureList = new List<Structure>();
            var memoryDeviceList = new List<MemoryDevice>();

            _raw = null;
            byte majorVersion = 0;
            byte minorVersion = 0;
            try
            {
                ManagementObjectCollection collection;
                using (var searcher =
                       new ManagementObjectSearcher("root\\WMI",
                           "SELECT * FROM MSSMBios_RawSMBiosTables"))
                {
                    collection = searcher.Get();
                }

                foreach (ManagementObject mo in collection)
                {
                    _raw = (byte[])mo["SMBiosData"];
                    majorVersion = (byte)mo["SmbiosMajorVersion"];
                    minorVersion = (byte)mo["SmbiosMinorVersion"];
                    break;
                }
            }
            catch
            {
            }

            if (majorVersion > 0 || minorVersion > 0)
                _version = new Version(majorVersion, minorVersion);

            if (_raw != null && _raw.Length > 0)
            {
                var offset = 0;
                var type = _raw[offset];
                while (offset + 4 < _raw.Length && type != 127)
                {
                    type = _raw[offset];
                    int length = _raw[offset + 1];
                    var handle = (ushort)((_raw[offset + 2] << 8) | _raw[offset + 3]);

                    if (offset + length > _raw.Length)
                        break;
                    var data = new byte[length];
                    Array.Copy(_raw, offset, data, 0, length);
                    offset += length;

                    var stringsList = new List<string>();
                    if (offset < _raw.Length && _raw[offset] == 0)
                        offset++;

                    while (offset < _raw.Length && _raw[offset] != 0)
                    {
                        var sb = new StringBuilder();
                        while (offset < _raw.Length && _raw[offset] != 0)
                        {
                            sb.Append((char)_raw[offset]);
                            offset++;
                        }

                        offset++;
                        stringsList.Add(sb.ToString());
                    }

                    offset++;
                    switch (type)
                    {
                        case 0x00:
                            _biosInformation = new BiosInformation(
                                type, handle, data, stringsList.ToArray());
                            structureList.Add(_biosInformation);
                            break;
                        case 0x01:
                            _systemInformation = new SystemInformation(
                                type, handle, data, stringsList.ToArray());
                            structureList.Add(_systemInformation);
                            break;
                        case 0x02:
                            _baseBoardInformation = new BaseBoardInformation(
                                type, handle, data, stringsList.ToArray());
                            structureList.Add(_baseBoardInformation);
                            break;
                        case 0x04:
                            _processorInformation = new ProcessorInformation(
                                type, handle, data, stringsList.ToArray());
                            structureList.Add(_processorInformation);
                            break;
                        case 0x11:
                            var m = new MemoryDevice(
                                type, handle, data, stringsList.ToArray());
                            memoryDeviceList.Add(m);
                            structureList.Add(m);
                            break;
                        default:
                            structureList.Add(new Structure(
                                type, handle, data, stringsList.ToArray()));
                            break;
                    }
                }
            }

            _memoryDevices = memoryDeviceList.ToArray();
            _table = structureList.ToArray();
        }
    }

    public string GetReport()
    {
        var r = new StringBuilder();

        if (_version != null)
        {
            r.Append("SMBIOS Version: ");
            r.AppendLine(_version.ToString(2));
            r.AppendLine();
        }

        if (Bios != null)
        {
            r.Append("BIOS Vendor: ");
            r.AppendLine(Bios.Vendor);
            r.Append("BIOS Version: ");
            r.AppendLine(Bios.Version);
            r.AppendLine();
        }

        if (System != null)
        {
            r.Append("System Manufacturer: ");
            r.AppendLine(System.ManufacturerName);
            r.Append("System Name: ");
            r.AppendLine(System.ProductName);
            r.Append("System Version: ");
            r.AppendLine(System.Version);
            r.AppendLine();
        }

        if (Board != null)
        {
            r.Append("Mainboard Manufacturer: ");
            r.AppendLine(Board.ManufacturerName);
            r.Append("Mainboard Name: ");
            r.AppendLine(Board.ProductName);
            r.Append("Mainboard Version: ");
            r.AppendLine(Board.Version);
            r.AppendLine();
        }

        if (Processor != null)
        {
            r.Append("Processor Manufacturer: ");
            r.AppendLine(Processor.ManufacturerName);
            r.Append("Processor Version: ");
            r.AppendLine(Processor.Version);
            r.Append("Processor Core Count: ");
            r.AppendLine(Processor.CoreCount.ToString());
            r.Append("Processor Core Enabled: ");
            r.AppendLine(Processor.CoreEnabled.ToString());
            r.Append("Processor Thread Count: ");
            r.AppendLine(Processor.ThreadCount.ToString());
            r.Append("Processor External Clock: ");
            r.Append(Processor.ExternalClock);
            r.AppendLine(" Mhz");
            r.AppendLine();
        }

        for (var i = 0; i < MemoryDevices.Length; i++)
        {
            r.Append("Memory Device [" + i + "] Manufacturer: ");
            r.AppendLine(MemoryDevices[i].ManufacturerName);
            r.Append("Memory Device [" + i + "] Part Number: ");
            r.AppendLine(MemoryDevices[i].PartNumber);
            r.Append("Memory Device [" + i + "] Device Locator: ");
            r.AppendLine(MemoryDevices[i].DeviceLocator);
            r.Append("Memory Device [" + i + "] Bank Locator: ");
            r.AppendLine(MemoryDevices[i].BankLocator);
            r.Append("Memory Device [" + i + "] Speed: ");
            r.Append(MemoryDevices[i].Speed);
            r.AppendLine(" MHz");
            r.AppendLine();
        }

        if (_raw != null)
        {
            var base64 = Convert.ToBase64String(_raw);
            r.AppendLine("SMBIOS Table");
            r.AppendLine();

            for (var i = 0; i < Math.Ceiling(base64.Length / 64.0); i++)
            {
                r.Append(" ");
                for (var j = 0; j < 0x40; j++)
                {
                    var index = (i << 6) | j;
                    if (index < base64.Length) r.Append(base64[index]);
                }

                r.AppendLine();
            }

            r.AppendLine();
        }

        return r.ToString();
    }

    public BiosInformation Bios => _biosInformation;

    public SystemInformation System => _systemInformation;

    public BaseBoardInformation Board => _baseBoardInformation;


    public ProcessorInformation Processor => _processorInformation;

    public MemoryDevice[] MemoryDevices => _memoryDevices;

    public class Structure
    {
        private readonly byte _type;
        private readonly ushort _handle;

        private readonly byte[] _data;
        private readonly string[] _strings;

        protected int GetByte(int offset)
        {
            if (offset < _data.Length && offset >= 0)
                return _data[offset];
            else
                return 0;
        }

        protected int GetWord(int offset)
        {
            if (offset + 1 < _data.Length && offset >= 0)
                return (_data[offset + 1] << 8) | _data[offset];
            else
                return 0;
        }

        protected string GetString(int offset)
        {
            if (offset < _data.Length && _data[offset] > 0 &&
                _data[offset] <= _strings.Length)
                return _strings[_data[offset] - 1];
            else
                return "";
        }

        public Structure(byte type, ushort handle, byte[] data, string[] strings)
        {
            _type = type;
            _handle = handle;
            _data = data;
            _strings = strings;
        }

        public byte Type => _type;

        public ushort Handle => _handle;
    }

    public class BiosInformation : Structure
    {
        private readonly string _vendor;
        private readonly string _version;

        public BiosInformation(string vendor, string version)
            : base(0x00, 0, null, null)
        {
            _vendor = vendor;
            _version = version;
        }

        public BiosInformation(byte type, ushort handle, byte[] data,
            string[] strings)
            : base(type, handle, data, strings)
        {
            _vendor = GetString(0x04);
            _version = GetString(0x05);
        }

        public string Vendor => _vendor;

        public string Version => _version;
    }

    public class SystemInformation : Structure
    {
        private readonly string _manufacturerName;
        private readonly string _productName;
        private readonly string _version;
        private readonly string _serialNumber;
        private readonly string _family;

        public SystemInformation(string manufacturerName, string productName,
            string version, string serialNumber, string family)
            : base(0x01, 0, null, null)
        {
            _manufacturerName = manufacturerName;
            _productName = productName;
            _version = version;
            _serialNumber = serialNumber;
            _family = family;
        }

        public SystemInformation(byte type, ushort handle, byte[] data,
            string[] strings)
            : base(type, handle, data, strings)
        {
            _manufacturerName = GetString(0x04);
            _productName = GetString(0x05);
            _version = GetString(0x06);
            _serialNumber = GetString(0x07);
            _family = GetString(0x1A);
        }

        public string ManufacturerName => _manufacturerName;

        public string ProductName => _productName;

        public string Version => _version;

        public string SerialNumber => _serialNumber;

        public string Family => _family;
    }

    public class BaseBoardInformation : Structure
    {
        private readonly string _manufacturerName;
        private readonly string _productName;
        private readonly string _version;
        private readonly string _serialNumber;

        public BaseBoardInformation(string manufacturerName, string productName,
            string version, string serialNumber)
            : base(0x02, 0, null, null)
        {
            _manufacturerName = manufacturerName;
            _productName = productName;
            _version = version;
            _serialNumber = serialNumber;
        }

        public BaseBoardInformation(byte type, ushort handle, byte[] data,
            string[] strings)
            : base(type, handle, data, strings)
        {
            _manufacturerName = GetString(0x04).Trim();
            _productName = GetString(0x05).Trim();
            _version = GetString(0x06).Trim();
            _serialNumber = GetString(0x07).Trim();
        }

        public string ManufacturerName => _manufacturerName;

        public string ProductName => _productName;

        public string Version => _version;

        public string SerialNumber => _serialNumber;
    }

    public class ProcessorInformation : Structure
    {
        public ProcessorInformation(byte type, ushort handle, byte[] data,
            string[] strings)
            : base(type, handle, data, strings)
        {
            ManufacturerName = GetString(0x07).Trim();
            Version = GetString(0x10).Trim();
            CoreCount = GetByte(0x23);
            CoreEnabled = GetByte(0x24);
            ThreadCount = GetByte(0x25);
            ExternalClock = GetWord(0x12);
        }

        public string ManufacturerName { get; private set; }

        public string Version { get; private set; }

        public int CoreCount { get; private set; }

        public int CoreEnabled { get; private set; }

        public int ThreadCount { get; private set; }

        public int ExternalClock { get; private set; }
    }

    public class MemoryDevice : Structure
    {
        private readonly string _deviceLocator;
        private readonly string _bankLocator;
        private readonly string _manufacturerName;
        private readonly string _serialNumber;
        private readonly string _partNumber;
        private readonly int _speed;

        public MemoryDevice(byte type, ushort handle, byte[] data,
            string[] strings)
            : base(type, handle, data, strings)
        {
            _deviceLocator = GetString(0x10).Trim();
            _bankLocator = GetString(0x11).Trim();
            _manufacturerName = GetString(0x17).Trim();
            _serialNumber = GetString(0x18).Trim();
            _partNumber = GetString(0x1A).Trim();
            _speed = GetWord(0x15);
        }

        public string DeviceLocator => _deviceLocator;

        public string BankLocator => _bankLocator;

        public string ManufacturerName => _manufacturerName;

        public string SerialNumber => _serialNumber;

        public string PartNumber => _partNumber;

        public int Speed => _speed;
    }
}
