/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2012-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware.HDD;

[NamePrefix("")]
[RequireSmart(0xAB)]
[RequireSmart(0xAC)]
[RequireSmart(0xAD)]
[RequireSmart(0xAE)]
[RequireSmart(0xC4)]
[RequireSmart(0xCA)]
[RequireSmart(0xCE)]
internal class SsdMicron : AtaStorage
{
    private static readonly IEnumerable<SmartAttribute> SmartAttributes =
        new List<SmartAttribute>
        {
            new(0x01, SmartNames.ReadErrorRate, RawToValue),
            new(0x05, SmartNames.ReallocatedNandBlockCount, RawToValue),
            new(0x09, SmartNames.PowerOnHours, RawToValue),
            new(0x0C, SmartNames.PowerCycleCount, RawToValue),
            new(0xAA, SmartNames.NewFailingBlockCount, RawToValue),
            new(0xAB, SmartNames.ProgramFailCount, RawToValue),
            new(0xAC, SmartNames.EraseFailCount, RawToValue),
            new(0xAD, SmartNames.WearLevelingCount, RawToValue),
            new(0xAE, SmartNames.UnexpectedPowerLossCount, RawToValue),
            new(0xB4, SmartNames.UnusedReserveNandBlocks, RawToValue),
            new(0xB5, SmartNames.Non4KAlignedAccess,
                (byte[] raw, byte value, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return 6e4f * ((raw[5] << 8) | raw[4]);
                }),
            new(0xB7, SmartNames.SataDownshiftErrorCount, RawToValue),
            new(0xB8, SmartNames.ErrorCorrectionCount, RawToValue),
            new(0xBB, SmartNames.ReportedUncorrectableErrors, RawToValue),
            new(0xBC, SmartNames.CommandTimeout, RawToValue),
            new(0xBD, SmartNames.FactoryBadBlockCount, RawToValue),
            new(0xC2, SmartNames.Temperature, (raw, value, p) => SignedRawToValue(raw, 1)),
            new(0xC4, SmartNames.ReallocationEventCount, RawToValue),
            new(0xC5, SmartNames.CurrentPendingSectorCount),
            new(0xC6, SmartNames.OffLineUncorrectableErrorCount, RawToValue),
            new(0xC7, SmartNames.UltraDmaCrcErrorCount, RawToValue),
            new(0xCA, SmartNames.RemainingLife,
                (byte[] raw, byte value, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return 100 - RawToValue(raw, value, p);
                },
                SensorType.Level, 0, SmartNames.RemainingLife),
            new(0xCE, SmartNames.WriteErrorRate,
                (byte[] raw, byte value, IReadOnlyArray<IParameter> p)
                    =>
                {
                    return 6e4f * ((raw[1] << 8) | raw[0]);
                }),
            new(0xD2, SmartNames.SuccessfulRainRecoveryCount, RawToValue),
            new(0xF6, SmartNames.TotalLbasWritten,
                (byte[] r, byte v, IReadOnlyArray<IParameter> p) =>
                {
                    return (((long)r[5] << 40) | ((long)r[4] << 32) | ((long)r[3] << 24) |
                            ((long)r[2] << 16) | ((long)r[1] << 8) | r[0]) *
                           (512.0f / 1024 / 1024 / 1024);
                }, SensorType.Data, 0, "Total Bytes Written"),
            new(0xF7, SmartNames.HostProgramNandPagesCount, RawToValue),
            new(0xF8, SmartNames.FtlProgramNandPagesCount, RawToValue)
        };

    private Sensor _temperature;
    private Sensor _writeAmplification;

    public SsdMicron(ISmart smart, string name, string firmwareRevision,
        int index, ISettings settings)
        : base(smart, name, firmwareRevision, "ssd", index, SmartAttributes, settings)
    {
        _temperature = new Sensor("Temperature", 0, false,
            SensorType.Temperature, this,
            new[]
            {
                new ParameterDescription("Offset [°C]",
                    "Temperature offset of the thermal sensor.\n" +
                    "Temperature = Value + Offset.", 0)
            }, settings);
        _writeAmplification = new Sensor("Write Amplification", 0,
            SensorType.Factor, this, settings);
    }

    public override void UpdateAdditionalSensors(DriveAttributeValue[] values)
    {
        double? hostProgramPagesCount = null;
        double? ftlProgramPagesCount = null;
        foreach (var value in values)
        {
            if (value.Identifier == 0xF7)
                hostProgramPagesCount = RawToValue(value.RawValue, value.AttrValue, null);

            if (value.Identifier == 0xF8)
                ftlProgramPagesCount = RawToValue(value.RawValue, value.AttrValue, null);

            if (value.Identifier == 0xC2)
            {
                _temperature.Value =
                    value.RawValue[0] + _temperature.Parameters[0].Value;
                if (value.RawValue[0] != 0)
                    ActivateSensor(_temperature);
            }
        }

        if (hostProgramPagesCount.HasValue && ftlProgramPagesCount.HasValue)
        {
            if (hostProgramPagesCount.Value > 0)
                _writeAmplification.Value =
                    (hostProgramPagesCount.Value + ftlProgramPagesCount) /
                    hostProgramPagesCount.Value;
            else
                _writeAmplification.Value = 0;
            ActivateSensor(_writeAmplification);
        }
    }
}
