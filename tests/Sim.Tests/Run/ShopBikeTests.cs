using System.IO;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ShopBikeTests
{
    [Fact]
    public void TryBuy_Bike_DebitsWalletAndGrantsVehicle()
    {
        string path = Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir, "bike.json");
        var defs = ShopCatalog.Parse(File.ReadAllText(path), path);
        var wallet = new Wallet(new Cents(200));
        var shop = new ShopSession(defs, wallet, seed: 1);
        shop.RollOffers(1);

        var bought = Assert.IsType<ShopBought>(shop.TryBuy("bike"));

        Assert.Equal("bike", bought.Id);
        Assert.Equal(new Cents(120), bought.Paid);
        Assert.Equal("bike", bought.Vehicle);
        Assert.Null(bought.Item);
        Assert.Null(bought.Blueprint);
        Assert.Equal(new Cents(80), wallet.Balance);
    }

    [Fact]
    public void ShopCatalog_Hire_StillRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ShopCatalog.Parse(
            """
            {
              "id": "npc_driver",
              "name": "Hire Driver",
              "kind": "hire",
              "price": 150,
              "grants": { "vehicle": "bike" },
              "availability": { "fromShift": 1, "slot": "fixed" }
            }
            """,
            "hire"));
        Assert.Contains("hire", ex.Message);
    }

    [Fact]
    public void ShopCatalog_TruckGrant_Rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ShopCatalog.Parse(
            """
            {
              "id": "mail_truck",
              "name": "Mail Truck",
              "kind": "vehicle",
              "price": 900,
              "grants": { "vehicle": "mail_truck" },
              "availability": { "fromShift": 2, "slot": "fixed" }
            }
            """,
            "truck"));
        Assert.Contains("bike", ex.Message);
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
