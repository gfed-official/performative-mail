using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.App;

public readonly record struct MoveIntent(sbyte AxisX, sbyte AxisY, ushort Yaw, InputButtons Buttons)
{
    public static MoveIntent Idle { get; } = new(0, 0, 0, InputButtons.None);
}

public readonly record struct JoinTarget(string Host, ushort Port)
{
    public static bool TryParse(string text, ushort defaultPort, out JoinTarget target)
    {
        target = default;
        if (string.IsNullOrWhiteSpace(text) || defaultPort == 0)
            return false;

        text = text.Trim();
        string host;
        ushort port = defaultPort;

        if (text[0] == '[')
        {
            int close = text.IndexOf(']');
            if (close < 2)
                return false;

            host = text.Substring(1, close - 1);
            if (close + 1 < text.Length)
            {
                if (text[close + 1] != ':')
                    return false;
                if (!TryPort(text.Substring(close + 2), out port))
                    return false;
            }
        }
        else
        {
            int firstColon = text.IndexOf(':');
            int lastColon = text.LastIndexOf(':');
            if (firstColon > 0 && firstColon == lastColon)
            {
                host = text.Substring(0, firstColon);
                if (!TryPort(text.Substring(firstColon + 1), out port))
                    return false;
            }
            else
            {
                host = text;
            }
        }

        if (string.IsNullOrWhiteSpace(host) || port == 0)
            return false;

        target = new JoinTarget(host, port);
        return true;
    }

    public override string ToString() =>
        Host.IndexOf(':') >= 0 ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

    private static bool TryPort(string text, out ushort port) =>
        ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port != 0;
}

public readonly record struct HostAdvertisement(string Address, ushort Port)
{
    public static HostAdvertisement For(ushort port) =>
        new(LanAddress.FirstNonLoopbackIPv4(), port);

    public override string ToString() => $"{Address}:{Port}";
}

public static class LanAddress
{
    public static string FirstNonLoopbackIPv4()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(addr.Address))
                    continue;

                var bytes = addr.Address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue;

                return addr.Address.ToString();
            }
        }

        return "127.0.0.1";
    }
}

public sealed record SessionOptions(
    ushort ListenPort,
    int MaxPlayers,
    TimeSpan ConnectDeadline,
    TimeSpan HandshakeDeadline)
{
    public const ushort DefaultPort = 7777;

    public static SessionOptions Default { get; } = new(
        DefaultPort,
        MaxPlayers: 8,
        ConnectDeadline: TimeSpan.FromSeconds(8),
        HandshakeDeadline: TimeSpan.FromSeconds(5));
}

public abstract record FailReason
{
    private FailReason()
    {
    }

    public sealed record PortInUse(ushort Port) : FailReason;

    public sealed record Unreachable(JoinTarget Target) : FailReason;

    public sealed record Refused(JoinTarget Target) : FailReason;

    public sealed record Rejected(HelloRejectReason Reason) : FailReason;

    public sealed record HandshakeTimeout(JoinTarget Target) : FailReason;

    public sealed record HostLost : FailReason;

    public string Message()
    {
        switch (this)
        {
            case PortInUse port:
                return $"Port {port.Port} is already in use.";
            case Unreachable unreachable:
                return $"Could not reach {unreachable.Target}. Check the address and that the host has UDP {unreachable.Target.Port} open.";
            case Refused refused:
                return $"The host at {refused.Target} closed the connection before join finished.";
            case Rejected:
                return "Protocol mismatch. You and the host are running different builds.";
            case HandshakeTimeout timeout:
                return $"Connected to {timeout.Target}, but the host never finished join.";
            case HostLost:
                return "Host lost.";
            default:
                throw new ArgumentOutOfRangeException(nameof(FailReason), this, null);
        }
    }
}

public sealed class PortUnavailableException : Exception
{
    public PortUnavailableException(ushort port)
        : base($"UDP port {port} is already in use.")
    {
        Port = port;
    }

    public ushort Port { get; }
}
