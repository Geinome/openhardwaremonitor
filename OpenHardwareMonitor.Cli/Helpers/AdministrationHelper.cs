using System;
using System.Security.Principal;

namespace OpenHardwareMonitor.Cli.Helpers;

internal static class AdministrationHelper
{
    public static bool IsUserAdministrator()
    {
        bool isAdmin;
        try
        {
            WindowsIdentity user = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(user);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (UnauthorizedAccessException)
        {
            isAdmin = false;
        }
        catch (Exception)
        {
            isAdmin = false;
        }

        return isAdmin;
    }
}
