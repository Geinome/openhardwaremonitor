using CommandLine;

namespace OpenHardwareMonitor.Cli.CommandLineOptions;

[Verb("RunWebserver", HelpText = "Run a webserver with REST api")]
public class RunWebserver : OptionsBase
{
    [Option('p', "port", Default = 8086, HelpText = "TCP port for the webserver (defaults to 8086)")]
    public int Port { get; set; }

    [Option('i', "interval", Default = 1000,
        HelpText = "The refresh interval for all data in ms (defaults to 1000 ms)")]
    public int Interval { get; set; }
}