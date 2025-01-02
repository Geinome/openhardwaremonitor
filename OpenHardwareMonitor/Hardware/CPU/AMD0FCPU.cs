/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2010 Paul Werelds <paul@werelds.net>

*/

using System.Globalization;
using System.Text;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.CPU
{
    internal sealed class Amd0Fcpu : Amdcpu
    {
        private readonly Sensor[] _coreTemperatures;
        private readonly Sensor[] _coreClocks;
        private readonly Sensor _busClock;

        private const uint FidvidStatus = 0xC0010042;

        private const byte MiscellaneousControlFunction = 3;
        private const ushort MiscellaneousControlDeviceId = 0x1103;
        private const uint ThermtripStatusRegister = 0xE4;

        private readonly byte _thermSenseCoreSelCpu0;
        private readonly byte _thermSenseCoreSelCpu1;
        private readonly uint _miscellaneousControlAddress;

        public Amd0Fcpu(int processorIndex, Cpuid[][] cpuid, ISettings settings)
            : base(processorIndex, cpuid, settings)
        {
            float offset = -49.0f;

            // AM2+ 65nm +21 offset
            uint model = cpuid[0][0].Model;
            if (model >= 0x69 && model != 0xc1 && model != 0x6c && model != 0x7c)
                offset += 21;

            if (model < 40)
            {
                // AMD Athlon 64 Processors
                _thermSenseCoreSelCpu0 = 0x0;
                _thermSenseCoreSelCpu1 = 0x4;
            }
            else
            {
                // AMD NPT Family 0Fh Revision F, G have the core selection swapped
                _thermSenseCoreSelCpu0 = 0x4;
                _thermSenseCoreSelCpu1 = 0x0;
            }

            // check if processor supports a digital thermal sensor
            if (cpuid[0][0].ExtData.GetLength(0) > 7 &&
                (cpuid[0][0].ExtData[7, 3] & 1) != 0)
            {
                _coreTemperatures = new Sensor[CoreCount];
                for (int i = 0; i < CoreCount; i++)
                {
                    _coreTemperatures[i] =
                        new Sensor("Core #" + (i + 1), i, SensorType.Temperature,
                            this, new[]
                            {
                                new ParameterDescription("Offset [°C]",
                                    "Temperature offset of the thermal sensor.\n" +
                                    "Temperature = Value + Offset.", offset)
                            }, settings);
                }
            }
            else
            {
                _coreTemperatures = new Sensor[0];
            }

            _miscellaneousControlAddress = GetPciAddress(
                MiscellaneousControlFunction, MiscellaneousControlDeviceId);

            _busClock = new Sensor("Bus Speed", 0, SensorType.Clock, this, settings);
            _coreClocks = new Sensor[CoreCount];
            for (int i = 0; i < _coreClocks.Length; i++)
            {
                _coreClocks[i] = new Sensor(CoreString(i), i + 1, SensorType.Clock,
                    this, settings);
                if (HasTimeStampCounter)
                    ActivateSensor(_coreClocks[i]);
            }

            Update();
        }

        protected override uint[] GetMsRs()
        {
            return new[] { FidvidStatus };
        }

        public override string GetReport()
        {
            StringBuilder r = new StringBuilder();
            r.Append(base.GetReport());

            r.Append("Miscellaneous Control Address: 0x");
            r.AppendLine((_miscellaneousControlAddress).ToString("X",
                CultureInfo.InvariantCulture));
            r.AppendLine();

            return r.ToString();
        }

        public override void Update()
        {
            base.Update();

            if (Ring0.WaitPciBusMutex(10))
            {
                if (_miscellaneousControlAddress != Ring0.InvalidPciAddress)
                {
                    for (uint i = 0; i < _coreTemperatures.Length; i++)
                    {
                        if (Ring0.WritePciConfig(
                                _miscellaneousControlAddress, ThermtripStatusRegister,
                                i > 0 ? _thermSenseCoreSelCpu1 : _thermSenseCoreSelCpu0))
                        {
                            uint value;
                            if (Ring0.ReadPciConfig(
                                    _miscellaneousControlAddress, ThermtripStatusRegister,
                                    out value))
                            {
                                _coreTemperatures[i].Value = ((value >> 16) & 0xFF) +
                                                            _coreTemperatures[i].Parameters[0].Value;
                                ActivateSensor(_coreTemperatures[i]);
                            }
                            else
                            {
                                DeactivateSensor(_coreTemperatures[i]);
                            }
                        }
                    }
                }

                Ring0.ReleasePciBusMutex();
            }

            if (HasTimeStampCounter)
            {
                double newBusClock = 0;

                for (int i = 0; i < _coreClocks.Length; i++)
                {
                    Thread.Sleep(1);

                    uint eax, edx;
                    if (Ring0.RdmsrTx(FidvidStatus, out eax, out edx,
                            Cpuid[i][0].Affinity))
                    {
                        // CurrFID can be found in eax bits 0-5, MaxFID in 16-21
                        // 8-13 hold StartFID, we don't use that here.
                        double curMp = 0.5 * ((eax & 0x3F) + 8);
                        double maxMp = 0.5 * ((eax >> 16 & 0x3F) + 8);
                        _coreClocks[i].Value =
                            (curMp * TimeStampCounterFrequency / maxMp);
                        newBusClock = (TimeStampCounterFrequency / maxMp);
                    }
                    else
                    {
                        // Fail-safe value - if the code above fails, we'll use this instead
                        _coreClocks[i].Value = TimeStampCounterFrequency;
                    }
                }

                if (newBusClock > 0)
                {
                    this._busClock.Value = newBusClock;
                    ActivateSensor(this._busClock);
                }
            }
        }
    }
}
