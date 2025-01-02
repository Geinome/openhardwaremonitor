using System;
using System.Threading.Tasks;
using OpenHardwareMonitor.Cli.Helpers;
using OpenHardwareMonitor.Cli.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli.Interceptors;

// TODO: Update to support elevating permissions: https://anthonysimmon.com/programmatically-elevate-dotnet-app-on-any-platform/
public class AdminPrivilegesRequiredInterceptor : ICommandInterceptor
{
    public Task<int> InterceptAsync(CommandContext context, Func<CommandContext, Task<int>> next)
    {
        if (!AdministrationHelper.IsUserAdministrator())
        {
            AnsiConsole.Markup("[underline red]WARNING:[/] [yellow]you need to run this application with administrator permission[/]");
            AnsiConsole.WriteLine();
            return Task.FromResult(ErrorCodes.AdminPrivilegesRequired);
        }

        return next(context);
    }
}
