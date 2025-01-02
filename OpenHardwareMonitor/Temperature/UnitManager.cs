using OpenHardwareMonitor.Settings;

namespace OpenHardwareMonitor.Temperature;

public class UnitManager
{
    private readonly PersistentSettings _settings;
    private TemperatureUnit _temperatureUnit;

    public UnitManager(PersistentSettings settings)
    {
        _settings = settings;
        _temperatureUnit = (TemperatureUnit)settings.GetValue("TemperatureUnit",
            (int)TemperatureUnit.Celsius);
    }

    public TemperatureUnit TemperatureUnit
    {
        get => _temperatureUnit;
        set
        {
            _temperatureUnit = value;
            _settings.SetValue("TemperatureUnit", (int)_temperatureUnit);
        }
    }

    public static double? CelsiusToFahrenheit(double? valueInCelsius)
    {
        return valueInCelsius * 1.8 + 32;
    }
}
