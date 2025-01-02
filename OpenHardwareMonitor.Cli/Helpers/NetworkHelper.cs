using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OpenHardwareMonitor.Cli.Helpers;

internal static class NetworkHelper
{
    public static IPAddress[] LocalIpAddresses()
    {
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            throw new Exception("No network available");
        }

        var host = Dns.GetHostEntry(Dns.GetHostName());

        return host.AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork)
            .ToArray();
    }
}
