using System;

namespace OpenHardwareMonitor.Cli;

internal class Utility
{
    /// <summary>
    /// Check for running with administration privileges, if required restart the application with UAC request for administration
    /// </summary>
    /// <returns>-1 if the application is already run with adminstrative privileges, -2 if the application can't be restarted with administration privileges, or the exit code of the application being run as admin</returns>
    public static int EnforceAppIsRunAsAdmin()
    {
        if (!IsUserAdministrator())
        {
            System.Diagnostics.ProcessStartInfo proc = new System.Diagnostics.ProcessStartInfo();
            proc.UseShellExecute = true;
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = System.Reflection.Assembly.GetEntryAssembly().Location;

            System.Collections.Generic.List<string> args = new System.Collections.Generic.List<string>(Environment.GetCommandLineArgs());
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

    /// <summary>
    /// The IP addresses of the local machine
    /// </summary>
    /// <returns></returns>
    public static System.Net.IPAddress[] LocalIpAddresses()
    {
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            return null;
        }

        System.Net.IPHostEntry host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

        System.Collections.Generic.List<System.Net.IPAddress> result = new System.Collections.Generic.List<System.Net.IPAddress>();
        foreach (System.Net.IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                result.Add(ip);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Log a message to console output, console errors or a log file
    /// </summary>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="isError"></param>
    public static void WriteLogMessage(Exception exception, CommandLineOptions.OptionsBase options)
    {
        if ((options != null) && options.VerboseMode)
        {
            WriteLogMessage("ERROR: " + exception, options, true);
        }
        else
        {
            WriteLogMessage("ERROR: " + exception.Message, options, true);
        }
    }

    /// <summary>
    /// Log a message to console output, console errors or a log file
    /// </summary>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="isError"></param>
    public static void WriteLogMessage(string message, CommandLineOptions.OptionsBase options, bool isError)
    {
        WriteLogMessage(message, options, isError, false);
    }

    /// <summary>
    /// Log a message to console output, console errors or a log file
    /// </summary>
    /// <param name="message"></param>
    /// <param name="options"></param>
    /// <param name="isError"></param>
    /// <param name="logToConsoleAndFileIfAvailable"></param>
    public static void WriteLogMessage(string message, CommandLineOptions.OptionsBase options, bool isError, bool logToConsoleAndFileIfAvailable)
    {
        if (options == null)
        {
            //no options parsed successfully
            Console.Out.WriteLine(message);
        }
        else if (string.IsNullOrEmpty(options.LogFile))
        {
            //no log file
            if (isError)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.Out.WriteLine(message);
            }
        }
        else
        {
            //use log file
            try
            {
                System.IO.File.AppendAllText(options.LogFile, message + Environment.NewLine);
                if (logToConsoleAndFileIfAvailable)
                {
                    if (isError)
                    {
                        Console.Error.WriteLine(message);
                    }
                    else
                    {
                        Console.Out.WriteLine(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR WRITING TO LOG FILE: " + ex.Message);
            }
        }
    }
}
