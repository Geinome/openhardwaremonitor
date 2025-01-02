namespace OpenHardwareMonitor.Cli;

public interface IComputerHardwareSettings
{
    bool IgnoreMonitorCPU { get; }

    bool IgnoreMonitorFanController { get; }

    bool IgnoreMonitorGPU { get; }

    bool IgnoreMonitorHDD { get; }

    bool IgnoreMonitorMainboard { get; }

    bool IgnoreMonitorRAM { get; }

    bool IgnoreMonitorNetwork { get; }
}
