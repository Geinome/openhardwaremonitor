using System.ComponentModel;
using OpenHardwareMonitor.Temperature;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli.Commands;

public class CommandSettingsBase : CommandSettings, IComputerHardwareSettings
{
    [Description( "Temperature values in Fahrenheit oder Celsius (defaults to Celsius)")]
    [CommandOption("-t|--TemperatureUnit")]
    public TemperatureUnit TemperatureUnit { get; set; }

    [Description("Verbose mode")]
    [CommandOption("-v|--Verbose")]
    public bool VerboseMode { get; set; }

    [Description( "On application exit, wait for X seconds")]
    [CommandOption("--WaitOnExitForSeconds")]
    public int WaitOnExitForSeconds { get; set; }

    [Description( "On application exit, wait for <Enter> key")]
    [CommandOption("--WaitOnExitForEnterKey")]
    public bool WaitOnExitForEnterKey { get; set; }

    [Description( "Log messages and errors to a file")]
    [CommandOption("--LogFile")]
    public string LogFile { get; set; }

    [Description( "Disable monitoring of CPU")]
    [CommandOption("--IgnoreMonitorCPU")]
    public bool IgnoreMonitorCPU { get; set; }

    [Description( "Disable monitoring of fan controller")]
    [CommandOption("--IgnoreMonitorFanController")]
    public bool IgnoreMonitorFanController { get; set; }

    [Description( "Disable monitoring of GPU")]
    [CommandOption("--IgnoreMonitorGPU")]
    public bool IgnoreMonitorGPU { get; set; }

    [Description( "Disable monitoring of HDDs/SSDs")]
    [CommandOption("--IgnoreMonitorHDD")]
    public bool IgnoreMonitorHDD { get; set; }

    [Description( "Disable monitoring of mainboard")]
    [CommandOption("--IgnoreMonitorMainboard")]
    public bool IgnoreMonitorMainboard { get; set; }

    [Description( "Disable monitoring of RAM")]
    [CommandOption("--IgnoreMonitorRAM")]
    public bool IgnoreMonitorRAM { get; set; }

    [Description( "Disable network monitoring")]
    [CommandOption("--IgnoreMonitorNetwork")]
    public bool IgnoreMonitorNetwork { get; set; }
}
