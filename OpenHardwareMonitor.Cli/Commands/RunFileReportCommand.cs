using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Hardware;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli.Commands;

public class RunFileReportCommand : AsyncCommand<RunFileReportCommand.Settings>
{
    public const string Verb = "ReportToFile";

    private readonly ILogger<RunFileReportCommand> _logger;
    private readonly ComputerHardware _computerHardware;

    public RunFileReportCommand(ILogger<RunFileReportCommand> logger, ComputerHardware computerHardware)
    {
        _logger = logger;
        _computerHardware = computerHardware;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _logger.LogInformation("Writing report to {FilePath}", settings.FilePath);

        try
        {
            await using var sw = new StreamWriter(settings.FilePath);
            await sw.WriteAsync(GetPlainTextReport(settings));
            return ErrorCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write report to file");
            return ErrorCodes.UnknownError;
        }
    }

    private string GetPlainTextReport(Settings settings)
    {
        var computer = _computerHardware.ComputerDiagnostics(settings);
        var result = computer.GetReport();
        computer.Close();
        return result;
    }

    public sealed class Settings : CommandSettingsBase
    {
        [Description("File to report to")]
        [CommandArgument(0, "<File>")]
        public required string FilePath { get; init; }

        [Description("Format of the report, defaults to 'Text'")]
        [CommandOption("-f|--format")]
        [TypeConverter(typeof(ReportFormat))]
        public ReportFormat Format { get; init; } = ReportFormat.Text;
    }
}
