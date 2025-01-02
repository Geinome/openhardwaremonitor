using Microsoft.Extensions.DependencyInjection;
using OpenHardwareMonitor.Cli;
using OpenHardwareMonitor.Cli.Commands;
using OpenHardwareMonitor.Cli.Interceptors;
using OpenHardwareMonitor.Cli.Services;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddSingleton<ComputerHardware>();
services.AddSingleton<AdministratorPrivilegesService>();

using var registrar = new DependencyInjectionRegistrar(services);

var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<RunWebserverCommand>(RunWebserverCommand.Verb)
        .WithDescription("Expose hardware information via a REST Api");

    config.AddCommand<RunConsoleReportCommand>(RunConsoleReportCommand.Verb)
        .WithDescription("Report hardware information to the console");

    config.AddCommand<RunFileReportCommand>(RunFileReportCommand.Verb)
        .WithDescription("Report hardware information to a file");

    config.SetInterceptor(new AdminPrivilegesRequiredInterceptor());

    config.PropagateExceptions();
    config.ValidateExamples();
    config.CaseSensitivity(CaseSensitivity.None);
});

return await app.RunAsync(args);
