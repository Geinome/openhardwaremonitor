/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System.Collections.Generic;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware.HDD;

[NamePrefix("INTEL SSD")]
[RequireSmart(0xE1)]
[RequireSmart(0xE8)]
[RequireSmart(0xE9)]
internal class SsdIntel : AtaStorage
{
    private static readonly IEnumerable<SmartAttribute> SmartAttributes =
        new List<SmartAttribute>
        {
            new(0x01, SmartNames.ReadErrorRate),
            new(0x03, SmartNames.SpinUpTime),
            new(0x04, SmartNames.StartStopCount, RawToValue),
            new(0x05, SmartNames.ReallocatedSectorsCount),
            new(0x09, SmartNames.PowerOnHours, RawToValue),
            new(0x0C, SmartNames.PowerCycleCount, RawToValue),
            new(0xAA, SmartNames.AvailableReservedSpace),
            new(0xAB, SmartNames.ProgramFailCount),
            new(0xAC, SmartNames.EraseFailCount),
            new(0xAE, SmartNames.UnexpectedPowerLossCount, RawToValue),
            new(0xB7, SmartNames.SataDownshiftErrorCount, RawToValue),
            new(0xBB, SmartNames.UncorrectableErrorCount, RawToValue),
            new(0xB8, SmartNames.EndToEndError),
            new(0xBE, SmartNames.Temperature,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return SignedRawToValue(r, 1) + (p == null ? 0 : p[0].Value);
                }, // Only 1 byte seem to contain the temperature, the upper bytes contain something else
                SensorType.Temperature, 0, SmartNames.Temperature, false,
                new[]
                {
                    new ParameterDescription("Offset [°C]",
                        "Temperature offset of the thermal sensor.\n" +
                        "Temperature = Value + Offset.", 0)
                }),
            new(0xC0, SmartNames.UnsafeShutdownCount),
            new(0xC7, SmartNames.CrcErrorCount, RawToValue),
            new(0xE1, SmartNames.HostWrites,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return RawToValue(r, v, p) / 0x20;
                },
                SensorType.Data, 0, SmartNames.HostWrites),
            new(0xE8, SmartNames.RemainingLife,
                null, SensorType.Level, 0, SmartNames.RemainingLife),
            new(0xE9, SmartNames.MediaWearOutIndicator),
            new(0xF1, SmartNames.HostWrites,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return RawToValue(r, v, p) / 0x20;
                },
                SensorType.Data, 0, SmartNames.HostWrites),
            new(0xF2, SmartNames.HostReads,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return RawToValue(r, v, p) / 0x20;
                },
                SensorType.Data, 1, SmartNames.HostReads)
        };

    public SsdIntel(ISmart smart, string name, string firmwareRevision,
        int index, ISettings settings)
        : base(smart, name, firmwareRevision, "ssd", index, SmartAttributes, settings)
    {
    }
}
