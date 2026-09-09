using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public static class MapBoot
{
    public const int Width = 24;
    public const int Height = 16;
    public const int TileCm = 200;
    public const string StreetA = "Oak Street";
    public const string StreetB = "Larch Lane";
    public static readonly AddressId HouseA = new(1, 1, 1, 0);
    public static readonly AddressId HouseB = new(2, 2, 2, 0);
    public static readonly TileCoord WoodTile = new(4, 3);
    public static readonly TileCoord PingTile = new(5, 6);

    public static WorldTables Tables()
    {
        int count = Width * Height;
        var heights = new short[count];
        var buildable = new bool[count];
        for (int i = 0; i < count; i++)
        {
            heights[i] = DebugWorld.LandHeightCm;
            buildable[i] = true;
        }

        var oak = new TileCoord[Width];
        var larch = new TileCoord[Width];
        for (int x = 0; x < Width; x++)
        {
            oak[x] = new TileCoord(x, 6);
            larch[x] = new TileCoord(x, 12);
        }

        var houses = new[]
        {
            new HouseRecord(HouseA, new TileCoord(0, 8), DebugWorld.LotSize, DebugWorld.House1Mailbox),
            new HouseRecord(HouseB, new TileCoord(0, 13), DebugWorld.LotSize, DebugWorld.House2Mailbox),
        };
        var lots = new[]
        {
            new LotRecord(1, 1, 1, new TileRect(0, 8, DebugWorld.LotSize.X, DebugWorld.LotSize.Y), true),
            new LotRecord(2, 2, 2, new TileRect(0, 13, DebugWorld.LotSize.X, DebugWorld.LotSize.Y), true),
        };

        return new WorldTables(
            Width,
            Height,
            TileCm,
            heights,
            houses,
            buildable,
            heightmapAttempts: 1,
            new PostOfficeRecord(
                DebugWorld.PostOfficeTile,
                DebugWorld.PostOfficeSize,
                DebugWorld.SpawnPadTile,
                DebugWorld.IntakeTile,
                Facing.East),
            new[]
            {
                new StreetRecord(1, StreetA, 1, oak),
                new StreetRecord(2, StreetB, 2, larch),
            },
            lots,
            new[] { new ResourceNodeRecord(ResourceKind.Wood, WoodTile) },
            Array.Empty<FerryLaneRecord>(),
            new[] { new RouteNodeRecord(1, new TileCoord(5, 6)), new RouteNodeRecord(2, new TileCoord(5, 12)) },
            new[] { new RouteEdgeRecord(1, 2, 6, 0) },
            Array.Empty<SpawnEdgeRecord>(),
            validationAttempts: 1);
    }

    public static OverlayReplica Overlay()
    {
        var catalog = LetterCatalog.Instance;
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbarId = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventoryId = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var mail = MailStack.Single(MailKinds.Letter, HouseA, new MailId(1));
        if (auth.Apply(Actor.System, new Deposit(hotbarId, mail)) is not Accepted)
            throw new InvalidOperationException("map boot overlay could not deposit mail.");

        var replica = new InventorySystem(catalog);
        Apply(replica, auth.Snapshot(hotbarId));
        Apply(replica, auth.Snapshot(inventoryId));
        return new OverlayReplica(
            replica[hotbarId],
            replica[inventoryId],
            null,
            null,
            OverlayReplica.NoPending);
    }

    public static MapFrame Placeholder() =>
        MapFrame.From(Tables(), Overlay(), MapFrame.DefaultLayers, MapFilter.None, Array.Empty<MapPing>());

    private static void Apply(InventorySystem replica, ContainerDelta delta)
    {
        if (replica.ApplyDelta(delta) != ReplicaResult.Applied)
            throw new InvalidOperationException("map boot overlay ApplyDelta failed.");
    }

    private sealed class LetterCatalog : IStackCatalog
    {
        public static readonly LetterCatalog Instance = new();

        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 20;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Mail;
    }
}
