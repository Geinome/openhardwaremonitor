using CommandLine;

namespace OpenHardwareMonitor.Cli.CommandLineOptions;

[Verb("ReportToFile", HelpText = "Report to a file")]
public class ReportToFile : OptionsBase
{
    [Option('f', "File", Required = true, HelpText = "File path")]
    public string FilePath { get; set; }
}