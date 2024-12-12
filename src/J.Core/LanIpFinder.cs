using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace J.Core;

public static class LanIpFinder
{
    public static string GetLanIpOrEmptyString()
    {
        try
        {
            var ips =
                from n in NetworkInterface.GetAllNetworkInterfaces()
                where
                    n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                from a in n.GetIPProperties().UnicastAddresses
                where
                    a.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(a.Address)
                    && IsPrivateIp(a.Address)
                select a.Address.ToString();

            return ips.FirstOrDefault() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool IsPrivateIp(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
