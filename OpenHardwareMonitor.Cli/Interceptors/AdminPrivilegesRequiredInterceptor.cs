using System;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenHardwareMonitor.Cli;

public class AdminPrivilegesRequiredInterceptor : ICommandInterceptor
{
    public Task<int> InterceptAsync(CommandContext context, Func<CommandContext, Task<int>> next)
    {
        if (!Utility.IsUserAdministrator())
        {
            AnsiConsole.Markup("[underline red]WARNING:[/] [yellow]you need to run this application with administrator permission[/]");
            AnsiConsole.WriteLine();
            return Task.FromResult(ErrorCodes.AdminPrivilegesRequired);
        }

        return next(context);
    }
}
