using System.IO;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ShopTruckTests
{
    [Fact]
    public void TryBuy_MailTruck_WithoutBlueprint_Rejected()
    {
        var wallet = new Wallet(new Cents(1400));
        var shop = new ShopSession(LoadTruckCatalog(), wallet, seed: 1);
        shop.RollOffers(2);

        var rejected = Assert.IsType<ShopRejected>(shop.TryBuy("mail_truck"));

        Assert.Equal(ShopReject.MissingBlueprint, rejected.Reason);
        Assert.Equal(new Cents(1400), wallet.Balance);
    }

    [Fact]
    public void TryBuy_BpTruckThenMailTruck_GrantsVehicle()
    {
        var wallet = new Wallet(new Cents(1400));
        var shop = new ShopSession(LoadTruckCatalog(), wallet, seed: 1);
        shop.RollOffers(2);

        var blueprint = Assert.IsType<ShopBought>(shop.TryBuy("bp_truck"));
        Assert.Equal("bp_truck", blueprint.Blueprint);

        var truck = Assert.IsType<ShopBought>(shop.TryBuy("mail_truck"));
        Assert.Equal("mail_truck", truck.Vehicle);
        Assert.Equal(new Cents(0), wallet.Balance);
    }

    private static ShopItemDef[] LoadTruckCatalog()
    {
        string root = Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir);
        string bpPath = Path.Combine(root, "bp_truck.json");
        string truckPath = Path.Combine(root, "mail_truck.json");
        var bp = ShopCatalog.Parse(File.ReadAllText(bpPath), bpPath);
        var truck = ShopCatalog.Parse(File.ReadAllText(truckPath), truckPath);
        var defs = new ShopItemDef[bp.Length + truck.Length];
        Array.Copy(bp, 0, defs, 0, bp.Length);
        Array.Copy(truck, 0, defs, bp.Length, truck.Length);
        return defs;
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }
}
