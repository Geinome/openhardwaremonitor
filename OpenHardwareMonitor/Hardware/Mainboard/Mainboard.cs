/*

  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.

  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>

*/

using System;
using System.Text;
using OpenHardwareMonitor.Hardware.LPC;

namespace OpenHardwareMonitor.Hardware.Mainboard;

internal class Mainboard : IHardware
{
    private readonly Smbios _smbios;
    private readonly string _name;
    private string _customName;
    private readonly ISettings _settings;
    private readonly Lpcio _lpcio;
    private readonly LmSensors _lmSensors;
    private readonly Hardware[] _superIoHardware;

    public Mainboard(Smbios smbios, ISettings settings)
    {
        this._settings = settings;
        this._smbios = smbios;

        var manufacturer = smbios.Board == null
            ? Manufacturer.Unknown
            : Identification.GetManufacturer(smbios.Board.ManufacturerName);

        var model = smbios.Board == null ? Model.Unknown : Identification.GetModel(smbios.Board.ProductName);

        if (smbios.Board != null)
        {
            if (!string.IsNullOrEmpty(smbios.Board.ProductName))
            {
                if (manufacturer == Manufacturer.Unknown)
                    _name = smbios.Board.ProductName;
                else
                    _name = manufacturer + " " +
                           smbios.Board.ProductName;
            }
            else
            {
                _name = manufacturer.ToString();
            }
        }
        else
        {
            _name = Manufacturer.Unknown.ToString();
        }

        _customName = settings.GetValue(
            new Identifier(Identifier, "name").ToString(), _name);

        ISuperIo[] superIo;
        if (OperatingSystem.IsUnix)
        {
            _lmSensors = new LmSensors();
            superIo = _lmSensors.SuperIo;
        }
        else
        {
            _lpcio = new Lpcio();
            superIo = _lpcio.SuperIo;
        }

        _superIoHardware = new Hardware[superIo.Length];
        for (var i = 0; i < superIo.Length; i++)
            _superIoHardware[i] = new SuperIoHardware(this, superIo[i],
                manufacturer, model, settings);
    }

    public string Name
    {
        get => _customName;
        set
        {
            if (!string.IsNullOrEmpty(value))
                _customName = value;
            else
                _customName = _name;
            _settings.SetValue(new Identifier(Identifier, "name").ToString(),
                _customName);
        }
    }

    public Identifier Identifier => new("mainboard");

    public HardwareType HardwareType => HardwareType.Mainboard;

    public virtual IHardware Parent => null;

    public string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("Mainboard");
        r.AppendLine();
        r.Append(_smbios.GetReport());

        if (_lpcio != null)
            r.Append(_lpcio.GetReport());

        var table =
            FirmwareTable.GetTable(FirmwareTable.Provider.ACPI, "TAMG");
        if (table != null)
        {
            var tamg = new GigabyteTamg(table);
            r.Append(tamg.GetReport());
        }

        return r.ToString();
    }

    public void Update()
    {
    }

    public void Close()
    {
        if (_lmSensors != null)
            _lmSensors.Close();
        foreach (var hardware in _superIoHardware)
            hardware.Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Close();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IHardware[] SubHardware => _superIoHardware;

    public ISensor[] Sensors => new ISensor[0];

#pragma warning disable 67
    public event SensorEventHandler SensorAdded;
    public event SensorEventHandler SensorRemoved;
#pragma warning restore 67

    public void Accept(IVisitor visitor)
    {
        if (visitor == null)
            throw new ArgumentNullException("visitor");
        visitor.VisitHardware(this);
    }

    public void Traverse(IVisitor visitor)
    {
        foreach (IHardware hardware in _superIoHardware)
            hardware.Accept(visitor);
    }
}
