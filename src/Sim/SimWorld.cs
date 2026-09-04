using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim;

public sealed class SimWorld
{
    public uint CurrentTick { get; private set; }

    public PlayerTable Players { get; } = new PlayerTable();

    public VehicleTable Vehicles { get; } = new VehicleTable();

    public WorldAtlas? Atlas { get; }

    public InventorySystem? Inventory { get; }

    public MailRegistry? Mail { get; }

    public MailSpawner? MailSpawner { get; }

    public ComplaintMeter Complaint { get; } = new();

    public Wallet Wallet { get; } = new();

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
        MailSpawner = new MailSpawner(atlas, Mail, Inventory, Intake, seed, jitterSeconds, Complaint);
    }

    public PlayerBody SpawnPlayer() => Players.SpawnOnRing(SpawnRing.CentreOf(Atlas));

    public void Tick(uint tick, bool spawnMail = true)
    {
        CurrentTick = tick;
        if (spawnMail)
            MailSpawner?.Step(tick);
    }

    public bool TryMount(EntityId player, EntityId vehicle)
    {
        if (!Players.TryGet(player, out var body))
            return false;
        if (!Vehicles.TryGet(vehicle, out var bike))
            return false;
        if (bike.Driver.Value != 0 && bike.Driver != player)
            return false;

        if (body.VehicleId.Value != 0 && body.VehicleId != vehicle)
            ReleaseDriver(body.VehicleId, player);

        body.Mount(vehicle);
        bike.SetDriver(player);
        bike.SetPose(body.Pose);
        return true;
    }

    public bool TryDismount(EntityId player)
    {
        if (!Players.TryGet(player, out var body))
            return false;
        if (body.VehicleId.Value == 0)
            return false;

        ReleaseDriver(body.VehicleId, player);
        body.Dismount();
        return true;
    }

    private void ReleaseDriver(EntityId vehicle, EntityId player)
    {
        if (Vehicles.TryGet(vehicle, out var bike) && bike.Driver == player)
            bike.ClearDriver();
    }

    public void ApplyInput(EntityId sender, in InputCmd cmd)
    {
        if (!Players.TryGet(sender, out var body))
            return;

        if (body.VehicleId.Value != 0 && Vehicles.TryGet(body.VehicleId, out var bike))
        {
            bike.Apply(in cmd, VehicleContext.BikeOnRoad);
            body.SetPose(bike.Pose);
            body.RecordInput(in cmd);
            return;
        }

        body.Apply(in cmd);
    }

    public void ApplyRequest(byte[] payload)
    {
    }
}
