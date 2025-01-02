using System;

namespace OpenHardwareMonitor.Cli;

internal class Utility
{
    public static int EnforceAppIsRunAsAdmin()
    {
        if (!IsUserAdministrator())
        {
            System.Diagnostics.ProcessStartInfo proc = new System.Diagnostics.ProcessStartInfo();
            proc.UseShellExecute = true;
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = System.Reflection.Assembly.GetEntryAssembly().Location;

            System.Collections.Generic.List<string> args =
                new System.Collections.Generic.List<string>(Environment.GetCommandLineArgs());
            args.RemoveAt(0);

            foreach (string arg in args.ToArray())
            {
                proc.Arguments += String.Format("\"{0}\" ", arg);
            }

            proc.Verb = "runas";

            try
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.Start(proc);
                p.WaitForExit();
                return p.ExitCode;
            }
            catch
            {
                Console.WriteLine("This application requires elevated credentials in order to operate correctly!");
                return -2;
            }
        }
        else
        {
            return -1;
        }
    }

    public static bool IsUserAdministrator()
    {
        bool isAdmin;
        try
        {
            System.Security.Principal.WindowsIdentity user = System.Security.Principal.WindowsIdentity.GetCurrent();
            System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(user);
            isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
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

    public static System.Net.IPAddress[] LocalIpAddresses()
    {
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            return null;
        }

        System.Net.IPHostEntry host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

        System.Collections.Generic.List<System.Net.IPAddress> result =
            new System.Collections.Generic.List<System.Net.IPAddress>();
        foreach (System.Net.IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                result.Add(ip);
            }
        }

        return result.ToArray();
    }
}
