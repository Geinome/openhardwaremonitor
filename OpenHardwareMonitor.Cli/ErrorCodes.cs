namespace OpenHardwareMonitor.Cli;

public static class ErrorCodes
{
    public const int Success = 0;
    public const int UnknownError = -1;
    public const int PlatformNotSupported = -2;
    public const int AdminPrivilegesRequired = -3;
    public const int FailedToStartWebServer = -4;
}
