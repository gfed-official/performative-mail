using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim;

public sealed class SimWorld
{
    public uint CurrentTick { get; private set; }

    public PlayerTable Players { get; } = new PlayerTable();

    public WorldAtlas? Atlas { get; }

    public InventorySystem? Inventory { get; }

    public MailRegistry? Mail { get; }

    public MailSpawner? MailSpawner { get; }

    public ContainerId Intake { get; }

    public SimWorld()
    {
    }

    public SimWorld(IStackCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        Inventory = new InventorySystem(catalog);
    }

    public SimWorld(WorldAtlas atlas, IStackCatalog catalog, int seed, int jitterSeconds = MailSpawnConstants.BatchJitterSeconds)
    {
        Atlas = atlas;
        Inventory = new InventorySystem(catalog);
        Mail = new MailRegistry();
        Intake = Inventory.CreateContainer(ContainerSpec.Intake);
        MailSpawner = new MailSpawner(atlas, Mail, Inventory, Intake, seed, jitterSeconds);
    }

    public PlayerBody SpawnPlayer() => Players.SpawnOnRing(SpawnRing.CentreOf(Atlas));

    public void Tick(uint tick)
    {
        CurrentTick = tick;
        MailSpawner?.Step(tick);
    }

    public void ApplyInput(EntityId sender, in InputCmd cmd)
    {
        if (!Players.TryGet(sender, out var body))
            return;

        body.Apply(in cmd);
    }

    public void ApplyRequest(byte[] payload)
    {
    }
}
