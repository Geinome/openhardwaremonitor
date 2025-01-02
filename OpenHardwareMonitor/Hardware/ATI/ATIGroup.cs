/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor;
using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Hardware.ATI;

internal class AtiGroup : IGroup
{
    private readonly List<Atigpu> _hardware = new();
    private readonly StringBuilder _report = new();

    private IntPtr _context = IntPtr.Zero;

    private ILogger _logger;

    public AtiGroup(ISettings settings)
    {
        _logger = this.GetCurrentClassLogger();
        try
        {
            var adlStatus = Adl.ADL_Main_Control_Create(1);
            var adl2Status = Adl.ADL2_Main_Control_Create(1, out _context);

            _report.AppendLine("AMD Display Library");
            _report.AppendLine();
            _report.Append("ADL Status: ");
            _report.AppendLine(adlStatus.ToString());
            _report.Append("ADL2 Status: ");
            _report.AppendLine(adl2Status.ToString());
            _report.AppendLine();

            _report.AppendLine("Graphics Versions");
            _report.AppendLine();
            try
            {
                var status = Adl.AdlGraphicsVersionsGet(out var versionInfo);
                _report.Append(" Status: ");
                _report.AppendLine(status.ToString());
                _report.Append(" DriverVersion: ");
                _report.AppendLine(versionInfo.DriverVersion);
                _report.Append(" CatalystVersion: ");
                _report.AppendLine(versionInfo.CatalystVersion);
                _report.Append(" CatalystWebLink: ");
                _report.AppendLine(versionInfo.CatalystWebLink);
            }
            catch (DllNotFoundException x)
            {
                _logger.LogError($"Unable to open atiadlxx.dll: {x.Message}");
                _report.AppendLine(" Status: DLL not found");
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unexpected error calling ADL_Graphics_Versions_Get: {e.Message}");
                _report.AppendLine(" Status: " + e.Message);
            }

            _report.AppendLine();

            if (adlStatus == AdlStatus.OK)
            {
                var numberOfAdapters = 0;
                Adl.AdlAdapterNumberOfAdaptersGet(ref numberOfAdapters);

                _report.Append("Number of adapters: ");
                _report.AppendLine(numberOfAdapters.ToString(CultureInfo.InvariantCulture));
                _report.AppendLine();

                if (numberOfAdapters > 0)
                {
                    var adapterInfo = new AdlAdapterInfo[numberOfAdapters];
                    if (Adl.ADL_Adapter_AdapterInfo_Get(adapterInfo) == AdlStatus.OK)
                        for (var i = 0; i < numberOfAdapters; i++)
                        {
                            int isActive;
                            Adl.AdlAdapterActiveGet(adapterInfo[i].AdapterIndex,
                                out isActive);
                            int adapterId;
                            Adl.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex,
                                out adapterId);

                            _report.Append("AdapterIndex: ");
                            _report.AppendLine(i.ToString(CultureInfo.InvariantCulture));
                            _report.Append("isActive: ");
                            _report.AppendLine(isActive.ToString(CultureInfo.InvariantCulture));
                            _report.Append("AdapterName: ");
                            _report.AppendLine(adapterInfo[i].AdapterName);
                            _report.Append("UDID: ");
                            _report.AppendLine(adapterInfo[i].UDID);
                            _report.Append("Present: ");
                            _report.AppendLine(adapterInfo[i].Present.ToString(
                                CultureInfo.InvariantCulture));
                            _report.Append("VendorID: 0x");
                            _report.AppendLine(adapterInfo[i].VendorID.ToString("X",
                                CultureInfo.InvariantCulture));
                            _report.Append("BusNumber: ");
                            _report.AppendLine(adapterInfo[i].BusNumber.ToString(
                                CultureInfo.InvariantCulture));
                            _report.Append("DeviceNumber: ");
                            _report.AppendLine(adapterInfo[i].DeviceNumber.ToString(
                                CultureInfo.InvariantCulture));
                            _report.Append("FunctionNumber: ");
                            _report.AppendLine(adapterInfo[i].FunctionNumber.ToString(
                                CultureInfo.InvariantCulture));
                            _report.Append("AdapterID: 0x");
                            _report.AppendLine(adapterId.ToString("X",
                                CultureInfo.InvariantCulture));

                            if (!string.IsNullOrEmpty(adapterInfo[i].UDID) &&
                                adapterInfo[i].VendorID == Adl.AtiVendorId)
                            {
                                var found = false;
                                foreach (var gpu in _hardware)
                                    if (gpu.BusNumber == adapterInfo[i].BusNumber &&
                                        gpu.DeviceNumber == adapterInfo[i].DeviceNumber)
                                    {
                                        found = true;
                                        break;
                                    }

                                if (!found)
                                {
                                    var nameBuilder = new StringBuilder(adapterInfo[i].AdapterName);
                                    nameBuilder.Replace("(TM)", " ");
                                    for (var j = 0; j < 10; j++) nameBuilder.Replace("  ", " ");
                                    var name = nameBuilder.ToString().Trim();

                                    _hardware.Add(new Atigpu(name,
                                        adapterInfo[i].AdapterIndex,
                                        adapterInfo[i].BusNumber,
                                        adapterInfo[i].DeviceNumber, _context, settings));
                                }
                            }

                            _report.AppendLine();
                        }
                }
            }
        }
        catch (DllNotFoundException x)
        {
            _logger.LogError(x, $"Could not locate a library: {x.Message}");
        }
        catch (EntryPointNotFoundException e)
        {
            _logger.LogError(e, $"Could not locate entry point: {e.TypeName}.{e.TargetSite?.Name} ({e.Message})");
            _report.AppendLine();
            _report.AppendLine(e.ToString());
            _report.AppendLine();
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware.ToArray();

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        try
        {
            foreach (var gpu in _hardware)
                gpu.Dispose();

            if (_context != IntPtr.Zero)
            {
                Adl.Adl2MainControlDestroy(_context);
                _context = IntPtr.Zero;
            }

            Adl.AdlMainControlDestroy();
        }
        catch (Exception)
        {
        }
    }
}
