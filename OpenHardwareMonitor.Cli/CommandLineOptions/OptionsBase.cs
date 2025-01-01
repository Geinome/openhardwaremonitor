using CommandLine;

namespace OpenHardwareMonitor.Cli.CommandLineOptions;

public class OptionsBase
{
    [Option('t', "TemperatureUnit", HelpText = "Temperature values in Fahrenheit oder Celsius (defaults to Celsius)")]
    public GUI.TemperatureUnit TemperatureUnit { get; set; }

    [Option('v', "Verbose", HelpText = "Verbose mode")]
    public bool VerboseMode { get; set; }

    [Option("WaitOnExitForSeconds", HelpText = "On application exit, wait for X seconds")]
    public int WaitOnExitForSeconds { get; set; }

    [Option("WaitOnExitForEnterKey", HelpText = "On application exit, wait for <Enter> key")]
    public bool WaitOnExitForEnterKey { get; set; }

    [Option("LogFile", HelpText = "Log messages and errors to a file")]
    public string LogFile { get; set; }

    [Option("IgnoreMonitorCPU", HelpText = "Disable monitoring of CPU")]
    public bool IgnoreMonitorCPU { get; set; }

    [Option("IgnoreMonitorFanController", HelpText = "Disable monitoring of fan controller")]
    public bool IgnoreMonitorFanController { get; set; }

    [Option("IgnoreMonitorGPU", HelpText = "Disable monitoring of GPU")]
    public bool IgnoreMonitorGPU { get; set; }

    [Option("IgnoreMonitorHDD", HelpText = "Disable monitoring of HDDs/SSDs")]
    public bool IgnoreMonitorHDD { get; set; }

    [Option("IgnoreMonitorMainboard", HelpText = "Disable monitoring of mainboard")]
    public bool IgnoreMonitorMainboard { get; set; }

    [Option("IgnoreMonitorRAM", HelpText = "Disable monitoring of RAM")]
    public bool IgnoreMonitorRAM { get; set; }

    [Option("IgnoreMonitorNetwork", HelpText = "Disable network monitoring")]
    public bool IgnoreMonitorNetwork { get; set; }
}
