/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.TBalancer;

internal class BalancerGroup : IGroup
{
    private readonly List<Balancer> _hardware = new();
    private readonly StringBuilder _report = new();

    public BalancerGroup(ISettings settings)
    {
        uint numDevices = 0;
        try
        {
            if (Ftd2Xx.FtCreateDeviceInfoList(out numDevices) != FtStatus.FT_OK)
            {
                _report.AppendLine("Status: FT_CreateDeviceInfoList failed");
                return;
            }
        }
        catch (DllNotFoundException)
        {
            return;
        }
        catch (EntryPointNotFoundException)
        {
            return;
        }
        catch (BadImageFormatException)
        {
            return;
        }

        for (var i = 0; i < numDevices; i++)
        {
            var serialNumberBuf = new byte[255];
            var descriptionBuf = new byte[255];
            FtHandle ftHandle = default;
            uint flags = 0;
            uint type = 0;
            uint id = 0;
            uint locId = 0;
            if (Ftd2Xx.FtGetDeviceInfoDetail(i, ref flags, ref type, ref id, ref locId, serialNumberBuf,
                    descriptionBuf,
                    out ftHandle) != FtStatus.FT_OK)
                continue;

            // For whatever reason, the returned strings contain lots of garbage past the first binary 0
            var serialNumber = Encoding.UTF8.GetString(serialNumberBuf);
            var description = Encoding.UTF8.GetString(descriptionBuf);

            TrimToSize(ref serialNumber);
            TrimToSize(ref description);

            var deviceType = (FtDevice)type;
            _report.Append("Device Index: ");
            _report.AppendLine(i.ToString(CultureInfo.InvariantCulture));
            _report.Append("Device Type: ");
            _report.AppendLine(deviceType.ToString());
            _report.Append("Description: ");
            _report.AppendLine(description);
            _report.Append("Serial number: ");
            _report.AppendLine(serialNumber);

            Ftd2Xx.FtClose(ftHandle);

            ftHandle = default;
            // the T-Balancer always uses an FT232BM
            if (deviceType != FtDevice.FtDevice232Bm)
            {
                _report.AppendLine("Status: Wrong device type");
                continue;
            }

            FtHandle handle;
            var status = Ftd2Xx.FtOpen(i, out handle);
            if (status != FtStatus.FT_OK)
            {
                _report.AppendLine("Open Status: " + status);
                continue;
            }

            Ftd2Xx.FtSetBaudRate(handle, 19200);
            Ftd2Xx.FtSetDataCharacteristics(handle, 8, 1, 0);
            Ftd2Xx.FtSetFlowControl(handle, FtFlowControl.FT_FLOW_RTS_CTS, 0x11,
                0x13);
            Ftd2Xx.FtSetTimeouts(handle, 1000, 1000);
            Ftd2Xx.FtPurge(handle, FtPurge.FT_PURGE_ALL);

            status = Ftd2Xx.Write(handle, new byte[] { 0x38 });
            if (status != FtStatus.FT_OK)
            {
                _report.AppendLine("Write Status: " + status);
                Ftd2Xx.FtClose(handle);
                continue;
            }

            var isValid = false;
            byte protocolVersion = 0;

            var j = 0;
            while (Ftd2Xx.BytesToRead(handle) == 0 && j < 2)
            {
                Thread.Sleep(100);
                j++;
            }

            if (Ftd2Xx.BytesToRead(handle) > 0)
            {
                if (Ftd2Xx.ReadByte(handle) == Balancer.Startflag)
                {
                    while (Ftd2Xx.BytesToRead(handle) < 284 && j < 5)
                    {
                        Thread.Sleep(100);
                        j++;
                    }

                    var length = Ftd2Xx.BytesToRead(handle);
                    if (length >= 284)
                    {
                        var data = new byte[285];
                        data[0] = Balancer.Startflag;
                        for (var k = 1; k < data.Length; k++)
                            data[k] = Ftd2Xx.ReadByte(handle);

                        // check protocol version 2X (protocols seen: 2C, 2A, 28)
                        isValid = (data[274] & 0xF0) == 0x20;
                        protocolVersion = data[274];
                        if (!isValid)
                        {
                            _report.Append("Status: Wrong Protocol Version: 0x");
                            _report.AppendLine(
                                protocolVersion.ToString("X", CultureInfo.InvariantCulture));
                        }
                    }
                    else
                    {
                        _report.AppendLine("Status: Wrong Message Length: " + length);
                    }
                }
                else
                {
                    _report.AppendLine("Status: Wrong Startflag");
                }
            }
            else
            {
                _report.AppendLine("Status: No Response");
            }

            Ftd2Xx.FtPurge(handle, FtPurge.FT_PURGE_ALL);
            Ftd2Xx.FtClose(handle);

            if (isValid)
            {
                _report.AppendLine("Status: OK");
                _hardware.Add(new Balancer(i, protocolVersion, settings));
            }

            if (i < numDevices - 1)
                _report.AppendLine();
        }
    }

    private void TrimToSize(ref string description)
    {
        var idx = description.IndexOf('\0');
        if (idx >= 0) description = description.Substring(0, idx);
    }

    public IReadOnlyList<IHardware> Hardware => _hardware.ToArray();

    public string GetReport()
    {
        if (_report.Length > 0)
        {
            var r = new StringBuilder();
            r.AppendLine("FTD2XX");
            r.AppendLine();
            r.Append(_report);
            r.AppendLine();
            return r.ToString();
        }
        else
        {
            return null;
        }
    }

    public void Close()
    {
        foreach (var tbalancer in _hardware)
            tbalancer.Dispose();
    }
}
