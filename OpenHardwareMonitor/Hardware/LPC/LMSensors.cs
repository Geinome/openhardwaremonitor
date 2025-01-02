/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenHardwareMonitor.Hardware.LPC;

internal class LmSensors
{
    private readonly List<LmChip> _lmChips = new();

    public LmSensors()
    {
        var basePaths = Directory.GetDirectories("/sys/class/hwmon/");
        foreach (var basePath in basePaths)
        foreach (var devicePath in new[] { "/device", "" })
        {
            var path = basePath + devicePath;

            string name = null;
            try
            {
                using (var reader = new StreamReader(path + "/name"))
                {
                    name = reader.ReadLine();
                }
            }
            catch (IOException)
            {
            }

            switch (name)
            {
                case "atk0110":
                    _lmChips.Add(new LmChip(Chip.ATK0110, path));
                    break;

                case "f71858fg":
                    _lmChips.Add(new LmChip(Chip.F71858, path));
                    break;
                case "f71862fg":
                    _lmChips.Add(new LmChip(Chip.F71862, path));
                    break;
                case "f71869":
                    _lmChips.Add(new LmChip(Chip.F71869, path));
                    break;
                case "f71869a":
                    _lmChips.Add(new LmChip(Chip.F71869A, path));
                    break;
                case "f71882fg":
                    _lmChips.Add(new LmChip(Chip.F71882, path));
                    break;
                case "f71889a":
                    _lmChips.Add(new LmChip(Chip.F71889Ad, path));
                    break;
                case "f71878ad":
                    _lmChips.Add(new LmChip(Chip.F71878Ad, path));
                    break;
                case "f71889ed":
                    _lmChips.Add(new LmChip(Chip.F71889Ed, path));
                    break;
                case "f71889fg":
                    _lmChips.Add(new LmChip(Chip.F71889F, path));
                    break;
                case "f71808e":
                    _lmChips.Add(new LmChip(Chip.F71808E, path));
                    break;

                case "it8705":
                    _lmChips.Add(new LmChip(Chip.It8705F, path));
                    break;
                case "it8712":
                    _lmChips.Add(new LmChip(Chip.It8712F, path));
                    break;
                case "it8716":
                    _lmChips.Add(new LmChip(Chip.It8716F, path));
                    break;
                case "it8718":
                    _lmChips.Add(new LmChip(Chip.It8718F, path));
                    break;
                case "it8720":
                    _lmChips.Add(new LmChip(Chip.It8720F, path));
                    break;

                case "nct6775":
                    _lmChips.Add(new LmChip(Chip.Nct6771F, path));
                    break;
                case "nct6776":
                    _lmChips.Add(new LmChip(Chip.Nct6776F, path));
                    break;
                case "nct6779":
                    _lmChips.Add(new LmChip(Chip.Nct6779D, path));
                    break;
                case "nct6791":
                    _lmChips.Add(new LmChip(Chip.Nct6791D, path));
                    break;
                case "nct6792":
                    _lmChips.Add(new LmChip(Chip.Nct6792D, path));
                    break;
                case "nct6793":
                    _lmChips.Add(new LmChip(Chip.Nct6793D, path));
                    break;
                case "nct6795":
                    _lmChips.Add(new LmChip(Chip.Nct6795D, path));
                    break;
                case "nct6796":
                    _lmChips.Add(new LmChip(Chip.Nct6796D, path));
                    break;
                case "nct6797":
                    _lmChips.Add(new LmChip(Chip.Nct6797D, path));
                    break;
                case "nct6798":
                    _lmChips.Add(new LmChip(Chip.Nct6798D, path));
                    break;

                case "w83627ehf":
                    _lmChips.Add(new LmChip(Chip.W83627Ehf, path));
                    break;
                case "w83627dhg":
                    _lmChips.Add(new LmChip(Chip.W83627Dhg, path));
                    break;
                case "w83667hg":
                    _lmChips.Add(new LmChip(Chip.W83667Hg, path));
                    break;
                case "w83627hf":
                    _lmChips.Add(new LmChip(Chip.W83627Hf, path));
                    break;
                case "w83627thf":
                    _lmChips.Add(new LmChip(Chip.W83627Thf, path));
                    break;
                case "w83687thf":
                    _lmChips.Add(new LmChip(Chip.W83687Thf, path));
                    break;
            }
        }
    }

    public void Close()
    {
        foreach (var lmChip in _lmChips)
            lmChip.Close();
    }

    public ISuperIo[] SuperIo => _lmChips.ToArray();

    private class LmChip : ISuperIo
    {
        private string _path;
        private readonly Chip _chip;

        private readonly double?[] _voltages;
        private readonly double?[] _temperatures;
        private readonly double?[] _fans;
        private readonly double?[] _controls;

        private readonly FileStream[] _voltageStreams;
        private readonly FileStream[] _temperatureStreams;
        private readonly FileStream[] _fanStreams;

        public Chip Chip => _chip;
        public double?[] Voltages => _voltages;
        public double?[] Temperatures => _temperatures;
        public double?[] Fans => _fans;
        public double?[] Controls => _controls;

        public LmChip(Chip chip, string path)
        {
            this._path = path;
            this._chip = chip;

            var voltagePaths = Directory.GetFiles(path, "in*_input");
            _voltages = new double?[voltagePaths.Length];
            _voltageStreams = new FileStream[voltagePaths.Length];
            for (var i = 0; i < voltagePaths.Length; i++)
                _voltageStreams[i] = new FileStream(voltagePaths[i],
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var temperaturePaths = Directory.GetFiles(path, "temp*_input");
            _temperatures = new double?[temperaturePaths.Length];
            _temperatureStreams = new FileStream[temperaturePaths.Length];
            for (var i = 0; i < temperaturePaths.Length; i++)
                _temperatureStreams[i] = new FileStream(temperaturePaths[i],
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var fanPaths = Directory.GetFiles(path, "fan*_input");
            _fans = new double?[fanPaths.Length];
            _fanStreams = new FileStream[fanPaths.Length];
            for (var i = 0; i < fanPaths.Length; i++)
                _fanStreams[i] = new FileStream(fanPaths[i],
                    FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            _controls = new double?[0];
        }

        public byte? ReadGpio(int index)
        {
            return null;
        }

        public void WriteGpio(int index, byte value)
        {
        }

        public string GetReport()
        {
            return null;
        }

        public void SetControl(int index, byte? value)
        {
        }

        private string ReadFirstLine(Stream stream)
        {
            var sb = new StringBuilder();
            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                var b = stream.ReadByte();
                while (b != -1 && b != 10)
                {
                    sb.Append((char)b);
                    b = stream.ReadByte();
                }
            }
            catch
            {
            }

            return sb.ToString();
        }

        public void Update()
        {
            for (var i = 0; i < _voltages.Length; i++)
            {
                var s = ReadFirstLine(_voltageStreams[i]);
                try
                {
                    _voltages[i] = 0.001 *
                                  long.Parse(s, CultureInfo.InvariantCulture);
                }
                catch
                {
                    _voltages[i] = null;
                }
            }

            for (var i = 0; i < _temperatures.Length; i++)
            {
                var s = ReadFirstLine(_temperatureStreams[i]);
                try
                {
                    _temperatures[i] = 0.001 *
                                      long.Parse(s, CultureInfo.InvariantCulture);
                }
                catch
                {
                    _temperatures[i] = null;
                }
            }

            for (var i = 0; i < _fans.Length; i++)
            {
                var s = ReadFirstLine(_fanStreams[i]);
                try
                {
                    _fans[i] = long.Parse(s, CultureInfo.InvariantCulture);
                }
                catch
                {
                    _fans[i] = null;
                }
            }
        }

        public void Close()
        {
            foreach (var stream in _voltageStreams)
                stream.Close();
            foreach (var stream in _temperatureStreams)
                stream.Close();
            foreach (var stream in _fanStreams)
                stream.Close();
        }
    }
}
