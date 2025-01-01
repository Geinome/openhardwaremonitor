using System;
using CommandLine;
using OpenHardwareMonitor.Hardware;
using OpenHardwareMonitor.Cli.CommandLineOptions;

namespace OpenHardwareMonitor.Cli;

internal class Program
{
    private static readonly ComputerHardware ComputerHardware = new();
    private static System.Timers.Timer _runConsoleHttpServerTimer;
    private static bool _verboseMode;
    private static bool _waitOnExitForEnterKey;
    private static int _waitOnExitForSeconds;

    /// <summary>
    /// Run the OpenHardwareMonitor application as a console application
    /// </summary>
    /// <param name="args"></param>
    /// <returns>0 in case of success, 1 if arguments mismatch, 2 if required application files are missing, 3 in case of unexpected errors, 9 if administrative privileges are missing, or higher values for task specific errors</returns>
    private static int Main(string[] args)
    {
        int exitCode = 0;
        try
        {
            if (!AllRequiredFilesAvailable())
                return 2;

            Parser caseInsensitiveParser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = Parser.Default.Settings.HelpWriter;
                settings.EnableDashDash = Parser.Default.Settings.EnableDashDash;
                settings.CaseInsensitiveEnumValues = Parser.Default.Settings.CaseInsensitiveEnumValues;
                settings.IgnoreUnknownArguments = Parser.Default.Settings.IgnoreUnknownArguments;
                settings.MaximumDisplayWidth = Parser.Default.Settings.MaximumDisplayWidth;
                settings.ParsingCulture = Parser.Default.Settings.ParsingCulture;
            });

            var result = caseInsensitiveParser
                .ParseArguments<RunWebserver, ReportToConsole,
                    ReportToFile>(args);
            exitCode = result.MapResult(
                (RunWebserver opts) => RunConsoleHttpServerAndReturnExitCode(opts),
                (ReportToConsole opts) => RunConsoleReportAndReturnExitCode(opts),
                (ReportToFile opts) => RunFileReportAndReturnExitCode(opts),
                errs => 1);
        }
        catch (Exception ex)
        {
            if (_verboseMode)
            {
                Utility.WriteLogMessage("ERROR: " + ex, null, true);
            }
            else
            {
                Utility.WriteLogMessage("ERROR: " + ex.Message, null, true);
            }

            exitCode = 3;
        }

        if ((exitCode == 1) && (!Utility.IsUserAdministrator()))
        {
            Console.Out.WriteLine("WARNING: you need to run this application with administrator permission");
            Console.Out.WriteLine();
        }

        if (_waitOnExitForSeconds > 0)
        {
            Console.Out.WriteLine("Waiting " + _waitOnExitForSeconds + " seconds before exiting . . .");
            System.Threading.Thread.Sleep(_waitOnExitForSeconds * 1000);
        }

        if (_waitOnExitForEnterKey)
        {
            Console.Out.WriteLine("Press <Enter> to exit . . .");
            Console.ReadLine();
        }

        return exitCode;
    }

    private static int InitBaseOptionsAndEnforceAdminPrivileges(OptionsBase options)
    {
        _waitOnExitForSeconds = options.WaitOnExitForSeconds;
        _waitOnExitForEnterKey = options.WaitOnExitForEnterKey;
        _verboseMode = options.VerboseMode;

        if (!string.IsNullOrEmpty(options.LogFile) && System.IO.File.Exists(options.LogFile))
        {
            Utility.WriteLogMessage(
                "--------------------------------------------------------------------------------------------",
                options, false);
        }

        if (!string.IsNullOrEmpty(options.LogFile))
        {
            Utility.WriteLogMessage("Application started on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                options, false);
        }

        if (!Utility.IsUserAdministrator())
        {
            Utility.WriteLogMessage(
                "Application requires administration privileges, restart of process as administrator required",
                options, false);
        }

        int exitCodeOfAdminMode = Utility.EnforceAppIsRunAsAdmin();
        if (exitCodeOfAdminMode == -2)
        {
            Utility.WriteLogMessage(
                "ERROR: Administrator permission required (" + exitCodeOfAdminMode + ")", options, true);
            return 9;
        }
        else if (exitCodeOfAdminMode > 0)
        {
            //code already executed successfully in separate application process with successfully requested administration privileges
            return exitCodeOfAdminMode;
        }
        else if (exitCodeOfAdminMode == -1)
        {
            //regular code execution should take place in following steps - we are running with adminitration privileges
            if (!string.IsNullOrEmpty(options.LogFile))
            {
                Utility.WriteLogMessage("Application started with administration privileges", options, false);
            }

            return exitCodeOfAdminMode;
        }
        else if (exitCodeOfAdminMode == 0)
        {
            //Succcess! :-)
            return exitCodeOfAdminMode;
        }
        else
        {
            //window close button or system terminates (e.g. logoff event) can cause negative exit codes - just log it as warning
            Utility.WriteLogMessage(
                "WARNING: exit code of process with administration privileges was " +
                exitCodeOfAdminMode + ", possibly caused by a close event by system or user", null,
                false);
            return exitCodeOfAdminMode;
        }
    }

    private static bool IsFileAvailable(string fileName)
    {
        string path = AppDomain.CurrentDomain.BaseDirectory;

        if (!System.IO.File.Exists(System.IO.Path.Combine(path, fileName)))
        {
            Utility.WriteLogMessage("ERROR: The following file could not be found: " + fileName, null, true);
            Utility.WriteLogMessage("       Please extract all files from the archive.", null, true);
            return false;
        }

        return true;
    }

    private static bool AllRequiredFilesAvailable()
    {
        if (!IsFileAvailable("OpenHardwareMonitor.exe"))
            return false;
        if (!IsFileAvailable("Aga.Controls.dll"))
            return false;
        if (!IsFileAvailable("CommandLine.dll"))
            return false;
        if (!IsFileAvailable("OpenHardwareMonitorLib.dll"))
            return false;
        if (!IsFileAvailable("OxyPlot.dll"))
            return false;
        if (!IsFileAvailable("OxyPlot.WindowsForms.dll"))
            return false;

        return true;
    }

    private static int RunFileReportAndReturnExitCode(ReportToFile options)
    {
        int enforcedExitCode = InitBaseOptionsAndEnforceAdminPrivileges(options);
        if (enforcedExitCode != -1)
        {
            return enforcedExitCode;
        }

        try
        {
            System.IO.FileInfo outputFile = new System.IO.FileInfo(options.FilePath);
            System.IO.StreamWriter sw = outputFile.CreateText();
            try
            {
                sw.Write(GetPlainTextReport(options));
                sw.Flush();
                sw.Close();
                return 0;
            }
            catch (Exception ex)
            {
                Utility.WriteLogMessage(ex, options);
                return 21;
            }
        }
        catch (Exception ex)
        {
            Utility.WriteLogMessage(ex, options);
            return 20;
        }
    }

    private static string GetPlainTextReport(OptionsBase options)
    {
        Computer computer = ComputerHardware.ComputerDiagnostics(options);
        string result = computer.GetReport();
        computer.Close();
        return result;
    }

    private static int RunConsoleReportAndReturnExitCode(ReportToConsole options)
    {
        int enforcedExitCode = InitBaseOptionsAndEnforceAdminPrivileges(options);
        if (enforcedExitCode != -1)
        {
            return enforcedExitCode;
        }

        try
        {
            Console.Out.Write(GetPlainTextReport(options));
            return 0;
        }
        catch (Exception ex)
        {
            Utility.WriteLogMessage(ex, options);
            return 30;
        }
    }

    private static void RunConsoleHttpServerTimer_Elapsed(Object source, System.Timers.ElapsedEventArgs e)
    {
        ComputerHardware.RefreshData();
    }

    private static int RunConsoleHttpServerAndReturnExitCode(RunWebserver options)
    {
        int enforcedExitCode = InitBaseOptionsAndEnforceAdminPrivileges(options);
        if (enforcedExitCode != -1)
        {
            return enforcedExitCode;
        }

        try
        {
            //currently running with administrative privileges
            Computer computer = ComputerHardware.ComputerDiagnostics(options);

            Utilities.GrapevineServer server =
                new Utilities.GrapevineServer(ComputerHardware.Root, computer, options.Port,
                    true);
            if (server.PlatformNotSupported)
            {
                Utility.WriteLogMessage("ERROR: Platform not supported", options, true);
                return 11;
            }

            if (server.Start())
            {
                // enable refresh timer
                _runConsoleHttpServerTimer = new System.Timers.Timer(options.Interval);
                _runConsoleHttpServerTimer.Elapsed += RunConsoleHttpServerTimer_Elapsed;
                _runConsoleHttpServerTimer.AutoReset = true;
                _runConsoleHttpServerTimer.Enabled = true;

                // output connection details to console and logfile
                Utility.WriteLogMessage("HTTP webserver started at port " + options.Port, options, false,
                    true);
                Utility.WriteLogMessage("It is available at these addresses:", options, false, true);
                foreach (System.Net.IPAddress ip in Utility.LocalIpAddresses())
                {
                    Utility.WriteLogMessage("- http://" + ip + ":" + options.Port + "/",
                        options, false, true);
                }

                // wait for user to quit the application
                Console.Out.WriteLine("Press <Enter> to stop the webserver . . .");
                Console.ReadLine();

                // shutdown the webserver
                _runConsoleHttpServerTimer.Stop();
                _runConsoleHttpServerTimer.Dispose();
                server.Stop();

                return 0;
            }
            else
            {
                Utility.WriteLogMessage("ERROR: Failed to start HTTP webserver", options, true);
                // Utility.WriteLogMessage(server.StartHttpListenerException.ToString(), options, true);
                return 12;
            }
        }
        catch (Exception ex)
        {
            Utility.WriteLogMessage(ex, options);
            return 10;
        }
    }
}
