using System.Net;
using System.Net.Sockets;
using PerformativeMail.App;

namespace PerformativeMail.Net.Tests;

public sealed class LanAddressTests
{
    [Fact]
    public void Pick_PrefersDefaultRouteOverEarlierVMnet()
    {
        Assert.Equal("10.7.147.169", LanAddress.Pick("10.7.147.169", EvanPcNics()));
    }

    [Fact]
    public void Pick_WithoutDefaultRoute_SkipsVirtualAdapters()
    {
        Assert.Equal("10.7.147.169", LanAddress.Pick(null, EvanPcNics()));
    }

    [Fact]
    public void Pick_RejectsLoopbackAndApipaCandidates()
    {
        var nics = new[]
        {
            new LanUnicast("127.0.0.1", "Loopback Pseudo-Interface 1", "Loopback", true),
            new LanUnicast("169.254.10.20", "Ethernet", "Realtek", true),
            new LanUnicast("10.7.147.169", "Wi-Fi", "Intel(R) Wi-Fi", true),
        };

        Assert.Equal("10.7.147.169", LanAddress.Pick(null, nics));
    }

    [Fact]
    public void Pick_RejectsApipaDefaultRoute()
    {
        Assert.Equal("10.7.147.169", LanAddress.Pick("169.254.1.1", EvanPcNics()));
    }

    [Fact]
    public void Pick_UsesVirtualWhenOnlyVirtualIsUp()
    {
        var nics = new[]
        {
            new LanUnicast(
                "192.168.149.1",
                "VMware Network Adapter VMnet1",
                "VMware Virtual Ethernet Adapter",
                true),
            new LanUnicast("10.7.147.169", "Wi-Fi", "Intel(R) Wi-Fi", false),
        };

        Assert.Equal("192.168.149.1", LanAddress.Pick(null, nics));
    }

    [Fact]
    public void Pick_ReturnsLoopbackWhenNothingUsable()
    {
        Assert.Equal("127.0.0.1", LanAddress.Pick(null, Array.Empty<LanUnicast>()));
    }

    [Fact]
    public void IsAdvertisableIPv4_RejectsLoopbackApipaAndAny()
    {
        Assert.False(LanAddress.IsAdvertisableIPv4(IPAddress.Loopback));
        Assert.False(LanAddress.IsAdvertisableIPv4(IPAddress.Any));
        Assert.False(LanAddress.IsAdvertisableIPv4(IPAddress.Parse("169.254.12.34")));
        Assert.True(LanAddress.IsAdvertisableIPv4(IPAddress.Parse("10.7.147.169")));
        Assert.True(LanAddress.IsAdvertisableIPv4(IPAddress.Parse("192.168.149.1")));
    }

    [Fact]
    public void LooksVirtual_MatchesVMwareAndHypervisorNames()
    {
        Assert.True(LanAddress.LooksVirtual("VMware Network Adapter VMnet1", "VMware Virtual Ethernet Adapter"));
        Assert.True(LanAddress.LooksVirtual("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter"));
        Assert.False(LanAddress.LooksVirtual("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz"));
    }

    [Fact]
    public void FirstNonLoopbackIPv4_IsAdvertisableOrLoopbackFallback()
    {
        var advertised = LanAddress.FirstNonLoopbackIPv4();
        Assert.True(IPAddress.TryParse(advertised, out var ip));
        Assert.Equal(AddressFamily.InterNetwork, ip.AddressFamily);
        if (advertised != "127.0.0.1")
            Assert.True(LanAddress.IsAdvertisableIPv4(ip));
    }

    [Fact]
    public void ProbeDefaultRouteIPv4_WhenPresent_IsAdvertisable()
    {
        var probed = LanAddress.ProbeDefaultRouteIPv4();
        if (probed is null)
            return;

        Assert.True(IPAddress.TryParse(probed, out var ip));
        Assert.True(LanAddress.IsAdvertisableIPv4(ip));
        Assert.Equal(probed, LanAddress.FirstNonLoopbackIPv4());
    }

    private static LanUnicast[] EvanPcNics() =>
        new[]
        {
            new LanUnicast("192.168.149.1", "VMware Network Adapter VMnet1", "VMware Virtual Ethernet Adapter", true),
            new LanUnicast("192.168.138.1", "VMware Network Adapter VMnet8", "VMware Virtual Ethernet Adapter", true),
            new LanUnicast("10.7.147.169", "Wi-Fi", "Intel(R) Wi-Fi", true),
        };
}
