# Open hardware Monitor CLI tool
This is a simple command line tool that reads the hardware sensors from the Open Hardware Monitor and prints them to the console.

## Usage
```sh
OpenHardwareMonitor.Cli ReportToConsole
```

## Building

## Cross-platform

While the Open Hardware Monitor is Windows-only, this tool is cross-platform and can be built on Windows, Linux, and macOS.

### Linux Development

Compatibility with Linux is still being worked on.
The following needs to happen:
- [ ] Implement Linux support in the OpenHardwareMonitorLib
- [ ] Implement Linux support in the OpenHardwareMonitor.Cli

To accomplish this, the following needs to be done:
- [ ] Check if OS is linux and has sensors. Can call `uname -r`. Will need to verify not on WSL by checking for `/proc/version` or `/proc/sys/fs/binfmt_misc/WSLInterop`.
- [ ] Check if `lm-sensors` is installed. Can call `sensors -v`. If not, fallback to https://docs.kernel.org/hwmon/hwmon-kernel-api.html
