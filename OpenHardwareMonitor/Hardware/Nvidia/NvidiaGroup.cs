/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OpenHardwareMonitor.Hardware.Nvidia;

internal class NvidiaGroup : IGroup
{
    private readonly List<Hardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public NvidiaGroup(ISettings settings)
    {
        if (!Nvapi.IsAvailable)
            return;

        _report.AppendLine("NVAPI");
        _report.AppendLine();

        string version;
        if (Nvapi.NvAPI_GetInterfaceVersionString(out version) == NvStatus.OK)
        {
            _report.Append(" Version: ");
            _report.AppendLine(version);
        }

        var handles =
            new NvPhysicalGpuHandle[Nvapi.MaxPhysicalGpus];
        int count;
        if (Nvapi.NvApiEnumPhysicalGpUs == null)
        {
            _report.AppendLine(" Error: NvAPI_EnumPhysicalGPUs not available");
            _report.AppendLine();
            return;
        }
        else
        {
            var status = Nvapi.NvApiEnumPhysicalGpUs(handles, out count);
            if (status != NvStatus.OK)
            {
                _report.AppendLine(" Status: " + status);
                _report.AppendLine();
                return;
            }
        }

        var result = Nvml.NvmlInit();

        _report.AppendLine();
        _report.AppendLine("NVML");
        _report.AppendLine();
        _report.AppendLine(" Status: " + result);
        _report.AppendLine();

        IDictionary<NvPhysicalGpuHandle, NvDisplayHandle> displayHandles =
            new Dictionary<NvPhysicalGpuHandle, NvDisplayHandle>();

        if (Nvapi.NvApiEnumNvidiaDisplayHandle != null &&
            Nvapi.NvApiGetPhysicalGpUsFromDisplay != null)
        {
            var status = NvStatus.OK;
            var i = 0;
            while (status == NvStatus.OK)
            {
                var displayHandle = new NvDisplayHandle();
                status = Nvapi.NvApiEnumNvidiaDisplayHandle(i, ref displayHandle);
                i++;

                if (status == NvStatus.OK)
                {
                    var handlesFromDisplay =
                        new NvPhysicalGpuHandle[Nvapi.MaxPhysicalGpus];
                    uint countFromDisplay;
                    if (Nvapi.NvApiGetPhysicalGpUsFromDisplay(displayHandle,
                            handlesFromDisplay, out countFromDisplay) == NvStatus.OK)
                        for (var j = 0; j < countFromDisplay; j++)
                            if (!displayHandles.ContainsKey(handlesFromDisplay[j]))
                                displayHandles.Add(handlesFromDisplay[j], displayHandle);
                }
            }
        }

        _report.Append("Number of GPUs: ");
        _report.AppendLine(count.ToString(CultureInfo.InvariantCulture));

        for (var i = 0; i < count; i++)
        {
            NvDisplayHandle displayHandle;
            displayHandles.TryGetValue(handles[i], out displayHandle);
            _hardware.Add(new NvidiaGpu(i, handles[i], displayHandle, settings));
        }

        _report.AppendLine();
    }

    public IReadOnlyList<IHardware> Hardware => _hardware.ToArray();

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        foreach (var gpu in _hardware)
            gpu.Dispose();

        if (Nvml.IsInitialized) Nvml.NvmlShutdown();
    }
}
