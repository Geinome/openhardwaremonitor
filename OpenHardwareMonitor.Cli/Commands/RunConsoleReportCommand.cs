using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli.Commands;

public class RunConsoleReportCommand : AsyncCommand<RunConsoleReportCommand.Settings>
{
    public const string Verb = "ReportToConsole";

    private readonly ILogger<RunConsoleReportCommand> _logger;
    private readonly ComputerHardware _computerHardware;

    public RunConsoleReportCommand(ILogger<RunConsoleReportCommand> logger, ComputerHardware computerHardware)
    {
        _logger = logger;
        _computerHardware = computerHardware;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        _logger.LogInformation("Running console report");

        try
        {
            await WriteReportToConsole(settings);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write report to console");
            return -1;
        }
    }

    private async Task WriteReportToConsole(Settings settings)
    {
        var computer = _computerHardware.ComputerDiagnostics(settings);
        var result = computer.GetReport();
        computer.Close();
    }

    public sealed class Settings : CommandSettingsBase
    {
    }
}
