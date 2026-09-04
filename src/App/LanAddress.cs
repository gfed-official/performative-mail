using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PerformativeMail.App;

public static class LanAddress
{
    private static readonly IPEndPoint DefaultRouteProbe = new(IPAddress.Parse("8.8.8.8"), 53);

    private static readonly string[] VirtualHints =
    {
        "VMware",
        "VMnet",
        "VirtualBox",
        "Hyper-V",
        "vEthernet",
        "Docker",
        "WSL",
    };

    public static string FirstNonLoopbackIPv4() =>
        Pick(ProbeDefaultRouteIPv4(), EnumerateUnicast());

    internal static string Pick(string? defaultRouteIPv4, IEnumerable<LanUnicast> candidates)
    {
        if (TryAdvertisable(defaultRouteIPv4, out var routed))
            return routed;

        string? firstVirtual = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.Up || !TryAdvertisable(candidate.Address, out var address))
                continue;

            if (LooksVirtual(candidate.AdapterName, candidate.Description))
            {
                firstVirtual ??= address;
                continue;
            }

            return address;
        }

        return firstVirtual ?? "127.0.0.1";
    }

    internal static bool IsAdvertisableIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
            return false;
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any))
            return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] != 169 || bytes[1] != 254;
    }

    internal static bool LooksVirtual(string adapterName, string description) =>
        ContainsVirtualHint(adapterName) || ContainsVirtualHint(description);

    internal static string? ProbeDefaultRouteIPv4()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Connect does not send; the kernel assigns the default-route source address.
            socket.Connect(DefaultRouteProbe);
            if (socket.LocalEndPoint is IPEndPoint local)
                return local.Address.ToString();
        }
        catch (SocketException)
        {
        }

        return null;
    }

    private static bool TryAdvertisable(string? text, out string address)
    {
        address = "";
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!IPAddress.TryParse(text, out var parsed) || !IsAdvertisableIPv4(parsed))
            return false;

        address = parsed.ToString();
        return true;
    }

    private static bool ContainsVirtualHint(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var hint in VirtualHints)
        {
            if (text.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<LanUnicast> EnumerateUnicast()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            bool up = nic.OperationalStatus == OperationalStatus.Up;
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                yield return new LanUnicast(
                    addr.Address.ToString(),
                    nic.Name,
                    nic.Description,
                    up);
            }
        }
    }
}

internal readonly record struct LanUnicast(
    string Address,
    string AdapterName,
    string Description,
    bool Up);
