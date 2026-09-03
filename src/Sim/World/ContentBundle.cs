using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.World;

public sealed class ContentBundle
{
    public ContentBundle(
        string[] streets,
        ArchetypeDef[] archetypes,
        BalanceTable balance,
        ItemDef[] items,
        ContainerDef[] containers,
        MailKindDef[] kinds,
        MailMixDef mix,
        DestinationTypeDef[] destinations,
        BuildingDef[] buildings,
        RecipeDef[] recipes,
        ShopItemDef[] shop,
        PerkDef[] perks,
        StampDef[] stamps,
        UnlockTable unlocks)
    {
        Streets = streets;
        Archetypes = archetypes;
        Balance = balance;
        Items = items;
        Containers = containers;
        Kinds = kinds;
        Mix = mix;
        Destinations = destinations;
        Buildings = buildings;
        Recipes = recipes;
        Shop = shop;
        Perks = perks;
        Stamps = stamps;
        Unlocks = unlocks;
    }

    public string[] Streets { get; }

    public ArchetypeDef[] Archetypes { get; }

    public BalanceTable Balance { get; }

    public ItemDef[] Items { get; }

    public ContainerDef[] Containers { get; }

    public MailKindDef[] Kinds { get; }

    public MailMixDef Mix { get; }

    public DestinationTypeDef[] Destinations { get; }

    public BuildingDef[] Buildings { get; }

    public RecipeDef[] Recipes { get; }

    public ShopItemDef[] Shop { get; }

    public PerkDef[] Perks { get; }

    public StampDef[] Stamps { get; }

    public UnlockTable Unlocks { get; }
}
