using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace OpenHardwareMonitor.Cli.Services;

public class AdministratorPrivilegesService
{
    private readonly ILogger<AdministratorPrivilegesService> _logger;

    public AdministratorPrivilegesService(ILogger<AdministratorPrivilegesService> logger)
    {
        _logger = logger;
    }

    public bool IsCurrentProcessElevated()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // https://github.com/dotnet/sdk/blob/v6.0.100/src/Cli/dotnet/Installer/Windows/WindowsUtils.cs#L38
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            // https://github.com/dotnet/maintenance-packages/blob/62823150914410d43a3fd9de246d882f2a21d5ef/src/Common/tests/TestUtilities/System/PlatformDetection.Unix.cs#L58
            // 0 is the ID of the root user
            return geteuid() == 0;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to check if the current process is elevated");
            return false;
        }
    }

    [DllImport("libc")]
    private static extern uint geteuid();
}
