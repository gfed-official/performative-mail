using PerformativeMail.Sim;
using PerformativeMail.Sim.Combat;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Combat;

public sealed class EnemyTableTests
{
    private const int TileCm = 200;
    private static readonly TileCoord Camp = new(0, 0);
    private static readonly TileCoord Pad = new(9, 0);

    private static RoutingGraph LineGraph() => new(
        new[]
        {
            new RouteNodeRecord(0, Camp),
            new RouteNodeRecord(1, new(3, 0)),
            new RouteNodeRecord(2, new(6, 0)),
            new RouteNodeRecord(3, Pad)
        },
        new[]
        {
            new RouteEdgeRecord(0, 1, 3, 0),
            new RouteEdgeRecord(1, 2, 3, 0),
            new RouteEdgeRecord(2, 3, 3, 0)
        });

    private static PostOfficeRecord PostOffice() => new(Pad, new TileCoord(2, 2), Pad, Pad, Facing.South);

    [Fact]
    public void Spawn_Barbarian_HasStatsIdClassAndRouteEndingAtPad()
    {
        var enemies = new EnemyTable(LineGraph(), PostOffice(), TileCm);
        var barb = enemies.Spawn(EnemyKind.Barbarian, Camp);

        Assert.Equal(EnemyKind.Barbarian, barb.Kind);
        Assert.Equal(60, barb.Hp);
        Assert.Equal(4.5, barb.SpeedMetresPerSecond);
        Assert.Equal(EntityClass.Enemy, barb.Id.Class);
        Assert.Equal(EnemyPhase.Spawned, barb.Phase);
        Assert.Equal(Pad, barb.Path.Tiles[barb.Path.Tiles.Count - 1]);
        Assert.Equal(PlayerPose.FromMeters(1.0, 1.0, 0, 16384), barb.Pose);
        Assert.NotEqual(barb.Id, enemies.Spawn(EnemyKind.Barbarian, Camp).Id);
    }

    [Fact]
    public void Step_ThirtyTicks_MarchesFourPointFiveMetresAlongRoute()
    {
        var enemies = new EnemyTable(LineGraph(), PostOffice(), TileCm);
        var barb = enemies.Spawn(EnemyKind.Barbarian, Camp);

        Step(enemies, TickClock.TickHz);

        Assert.Equal(EnemyPhase.Marching, barb.Phase);
        Assert.InRange(barb.MetresAlong, 4.35, 4.65);
        Assert.InRange(barb.Pose.Xcm, 535, 565);
        Assert.Equal(100, barb.Pose.Ycm);
    }

    [Fact]
    public void SimWorldTick_BoundTable_AdvancesAgent()
    {
        var world = new SimWorld();
        world.Enemies = new EnemyTable(LineGraph(), PostOffice(), TileCm);
        var barb = world.Enemies.Spawn(EnemyKind.Barbarian, Camp);

        for (uint t = 1; t <= (uint)TickClock.TickHz; t++)
            world.Tick(t, spawnMail: false);

        Assert.InRange(barb.MetresAlong, 4.35, 4.65);
    }

    [Fact]
    public void Step_PastRouteEnd_ArrivesAtPadAndHolds()
    {
        var enemies = new EnemyTable(LineGraph(), PostOffice(), TileCm);
        var barb = enemies.Spawn(EnemyKind.Barbarian, Camp);

        Step(enemies, 130);

        Assert.Equal(EnemyPhase.Arrived, barb.Phase);
        Assert.Equal(PlayerPose.FromMeters(19.0, 1.0, 0, 16384), barb.Pose);
        Assert.Equal(18, barb.MetresAlong);

        var pose = barb.Pose;
        var along = barb.MetresAlong;
        enemies.Step((float)TickClock.TickDurationSeconds);

        Assert.Equal(EnemyPhase.Arrived, barb.Phase);
        Assert.Equal(pose, barb.Pose);
        Assert.Equal(along, barb.MetresAlong);
    }

    [Fact]
    public void Spawn_OffGraphTile_SnapsToNearestNode()
    {
        var enemies = new EnemyTable(LineGraph(), PostOffice(), TileCm);
        var barb = enemies.Spawn(EnemyKind.Barbarian, new TileCoord(1, 1));

        Assert.Equal(Camp, barb.Path.Tiles[0]);
        Assert.Equal(PlayerPose.FromMeters(1.0, 1.0, 0, 16384), barb.Pose);
    }

    private static void Step(EnemyTable enemies, int ticks)
    {
        float dt = (float)TickClock.TickDurationSeconds;
        for (int i = 0; i < ticks; i++)
            enemies.Step(dt);
    }
}
