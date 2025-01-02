/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2011-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
    Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>

*/

using System;
using System.Collections.Generic;
using System.Text;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware.HDD;

[NamePrefix("")]
internal class GenericHarddisk : AtaStorage
{
    private static readonly List<SmartAttribute> SmartAttributes =
        new()
        {
            new(0x01, SmartNames.ReadErrorRate),
            new(0x02, SmartNames.ThroughputPerformance),
            new(0x03, SmartNames.SpinUpTime),
            new(0x04, SmartNames.StartStopCount, RawToValue),
            new(0x05, SmartNames.ReallocatedSectorsCount),
            new(0x06, SmartNames.ReadChannelMargin),
            new(0x07, SmartNames.SeekErrorRate),
            new(0x08, SmartNames.SeekTimePerformance),
            new(0x09, SmartNames.PowerOnHours, RawToValue),
            new(0x0A, SmartNames.SpinRetryCount),
            new(0x0B, SmartNames.RecalibrationRetries),
            new(0x0C, SmartNames.PowerCycleCount, RawToValue),
            new(0x0D, SmartNames.SoftReadErrorRate),
            new(0xAA, SmartNames.Unknown),
            new(0xAB, SmartNames.Unknown),
            new(0xAC, SmartNames.Unknown),
            new(0xB7, SmartNames.SataDownshiftErrorCount, RawToValue),
            new(0xB8, SmartNames.EndToEndError),
            new(0xB9, SmartNames.HeadStability),
            new(0xBA, SmartNames.InducedOpVibrationDetection),
            new(0xBB, SmartNames.ReportedUncorrectableErrors, RawToValue),
            new(0xBC, SmartNames.CommandTimeout, RawToValue),
            new(0xBD, SmartNames.HighFlyWrites),
            new(0xBF, SmartNames.GSenseErrorRate),
            new(0xC0, SmartNames.EmergencyRetractCycleCount),
            new(0xC1, SmartNames.LoadCycleCount),
            new(0xC3, SmartNames.HardwareEccRecovered),
            new(0xC4, SmartNames.ReallocationEventCount),
            new(0xC5, SmartNames.CurrentPendingSectorCount),
            new(0xC6, SmartNames.UncorrectableSectorCount),
            new(0xC7, SmartNames.UltraDmaCrcErrorCount),
            new(0xC8, SmartNames.WriteErrorRate),
            new(0xCA, SmartNames.DataAddressMarkErrors),
            new(0xCB, SmartNames.RunOutCancel),
            new(0xCC, SmartNames.SoftEccCorrection),
            new(0xCD, SmartNames.ThermalAsperityRate),
            new(0xCE, SmartNames.FlyingHeight),
            new(0xCF, SmartNames.SpinHighCurrent),
            new(0xD0, SmartNames.SpinBuzz),
            new(0xD1, SmartNames.OfflineSeekPerformance),
            new(0xD3, SmartNames.VibrationDuringWrite),
            new(0xD4, SmartNames.ShockDuringWrite),
            new(0xDC, SmartNames.DiskShift),
            new(0xDD, SmartNames.AlternativeGSenseErrorRate),
            new(0xDE, SmartNames.LoadedHours),
            new(0xDF, SmartNames.LoadUnloadRetryCount),
            new(0xE0, SmartNames.LoadFriction),
            new(0xE1, SmartNames.LoadUnloadCycleCount),
            new(0xE2, SmartNames.LoadInTime),
            new(0xE3, SmartNames.TorqueAmplificationCount),
            new(0xE4, SmartNames.PowerOffRetractCycle),
            new(0xE6, SmartNames.GmrHeadAmplitude),
            new(0xE8, SmartNames.EnduranceRemaining),
            new(0xE9, SmartNames.PowerOnHours),
            new(0xF0, SmartNames.HeadFlyingHours),
            new(0xF1, SmartNames.TotalLbasWritten),
            new(0xF2, SmartNames.TotalLbasRead),
            new(0xFA, SmartNames.ReadErrorRetryRate),
            new(0xFE, SmartNames.FreeFallProtection),

            new(0xC2, SmartNames.Temperature,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return SignedRawToValue(r, 1) + (p == null ? 0 : p[0].Value);
                },
                SensorType.Temperature, 0, SmartNames.Temperature, false,
                new[]
                {
                    new ParameterDescription("Offset [°C]",
                        "Temperature offset of the thermal sensor.\n" +
                        "Temperature = Value + Offset.", 0)
                }),
            new(0xE7, SmartNames.Temperature,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return SignedRawToValue(r, 1) + (p == null ? 0 : p[0].Value);
                },
                SensorType.Temperature, 0, SmartNames.Temperature, false,
                new[]
                {
                    new ParameterDescription("Offset [°C]",
                        "Temperature offset of the thermal sensor.\n" +
                        "Temperature = Value + Offset.", 0)
                }),
            new(0xBE, SmartNames.TemperatureDifferenceFrom100,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return SignedRawToValue(r, 1) + (p == null ? 0 : p[0].Value);
                },
                SensorType.Temperature, 0, "Temperature", false,
                new[]
                {
                    new ParameterDescription("Offset [°C]",
                        "Temperature offset of the thermal sensor.\n" +
                        "Temperature = Value + Offset.", 0)
                })
        };

    public GenericHarddisk(ISmart smart, string name, string firmwareRevision,
        int index, ISettings settings)
        : base(smart, name, firmwareRevision, "hdd", index, SmartAttributes, settings)
    {
    }
}
