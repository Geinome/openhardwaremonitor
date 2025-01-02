using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            var report = GetReportData(settings);
            await sw.WriteAsync(report);
            return ErrorCodes.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write report to file");
            return ErrorCodes.UnknownError;
        }
    }

    private string GetReportData(Settings settings)
    {
        Computer computer = _computerHardware.ComputerDiagnostics(settings);

        try
        {
            return settings.Format switch
            {
                ReportFormat.Json => GenerateJsonReport(computer),
                ReportFormat.Text => GenerateTextReport(computer),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            computer.Close();
        }
    }

    private static string GenerateJsonReport(Computer computer)
    {
        var model = new
        {
            Version = typeof(Computer).Assembly.GetName().Version,
            OperatingSystem = Environment.OSVersion,
            Hardware = computer.Hardware.Select(h => new
            {
                h.Name,
                h.HardwareType,
                Identifier = h.Identifier.ToString(),
                Sensors = h.Sensors.Select(s => new
                {
                    s.Name,
                    Identifier = s.Identifier.ToString(),
                    s.SensorType,
                    s.Value,
                    s.Max,
                    s.Min
                }),
                SubHardware = h.SubHardware.Select(sh => new
                {
                    sh.Name,
                    sh.HardwareType,
                    Identifier = sh.Identifier.ToString(),
                    Sensors = sh.Sensors.Select(s => new
                    {
                        s.Name,
                        Identifier = s.Identifier.ToString(),
                        s.SensorType,
                        s.Value,
                        s.Max,
                        s.Min,
                    })
                })
            }),
            ProcessType = IntPtr.Size == 4 ? "32-Bit" : "64-Bit",
        };

        return JsonSerializer.Serialize(model, new JsonSerializerOptions()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            WriteIndented = true,
        });
    }

    private static string GenerateTextReport(Computer computer)
    {
        return computer.GetReport();
    }

    public sealed class Settings : CommandSettingsBase
    {
        [Description("File to report to")]
        [CommandArgument(0, "<File>")]
        public required string FilePath { get; init; }

        [Description("Format of the report, defaults to 'Text'")]
        [CommandOption("-f|--format")]
        public ReportFormat Format { get; init; } = ReportFormat.Text;
    }
}
