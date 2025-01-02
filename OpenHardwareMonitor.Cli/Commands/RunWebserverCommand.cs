using System;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenHardwareMonitor.Cli.Helpers;
using OpenHardwareMonitor.Hardware;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli.Commands;

public class RunWebserverCommand : AsyncCommand<RunWebserverCommand.Settings>
{
    public const string Verb = "RunWebserver";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ILogger<RunWebserverCommand> _logger;
    private readonly ComputerHardware _computerHardware;
    private readonly PeriodicTimer _periodicTimer;

    public RunWebserverCommand(ILogger<RunWebserverCommand> logger, ComputerHardware computerHardware)
    {
        _logger = logger;
        _computerHardware = computerHardware;
        _periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            Computer computer = _computerHardware.ComputerDiagnostics(settings);

            GrapevineServer server =
                new GrapevineServer(_computerHardware.Root, computer, settings.Port,
                    true);

            if (server.PlatformNotSupported)
            {
                _logger.LogCritical("Platform not supported");
                return ErrorCodes.PlatformNotSupported;
            }

            if (!server.Start())
            {
                _logger.LogError("Failed to start HTTP webserver");
                return ErrorCodes.FailedToStartWebServer;
            }

            // enable refresh timer
            while (await _periodicTimer.WaitForNextTickAsync(_cancellationTokenSource.Token))
            {
                _computerHardware.RefreshData();
            }

            // output connection details to console and logfile
            _logger.LogInformation("HTTP webserver started at port {Port}", settings.Port);
            _logger.LogInformation("It is available at these addresses:");
            foreach (IPAddress ip in NetworkHelper.LocalIpAddresses())
            {
                _logger.LogInformation("http://{Ip}:{Port}/", ip, settings.Port);
            }

            _logger.LogInformation("Press <Enter> to stop the webserver . . .");

            // shutdown the webserver
            server.Stop();

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while running the webserver");
            return ErrorCodes.UnknownError;
        }
    }

    public sealed class Settings : CommandSettingsBase
    {
        [Description("TCP port for the webserver (defaults to 8086)")]
        [CommandOption("-p|--port")]
        public int Port { get; set; } = 8086;

        [Description("The refresh interval for all data in ms (defaults to 1000 ms)")]
        [CommandOption("-i|--interval")]
        public int Interval { get; set; } = 1000;
    }
}
