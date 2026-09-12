using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;
using InventoryAccepted = PerformativeMail.Sim.Inventory.Accepted;

namespace PerformativeMail.Sim.Vehicles;

public enum NpcHireReject : byte
{
    InsufficientFunds,
    AlreadyHired,
    NotParked,
    CannotOperate,
}

public abstract record NpcHireResult;

public sealed record NpcHired(NpcDriver Driver, Cents Paid) : NpcHireResult;

public sealed record NpcHireRejected(NpcHireReject Reason) : NpcHireResult;

public enum NpcRoutePhase : byte
{
    Hired,
    Driving,
    Delivering,
    Fleeing,
    Done,
}

public sealed class NpcDriver
{
    public const int HireCents = 150;
    public const float SpeedRatio = 0.6f;
    public const double FleeRadiusMetres = 20;

    public static int InsertPeriodTicks => TickClock.TickHz / 2;

    private readonly IRouteConsole _site;
    private readonly VehicleBody _vehicle;
    private readonly int _tileCm;
    private PathHop[] _hops = Array.Empty<PathHop>();
    private TileCoord[] _routeTiles = Array.Empty<TileCoord>();
    private RouteEnemy[] _enemies = Array.Empty<RouteEnemy>();
    private RoutingGraph? _graph;
    private int _hop;
    private double _metresAlong;
    private int _insertWait;
    private InventorySystem? _inventory;
    private Destinations? _destinations;
    private Dictionary<AddressId, DestinationId>? _mailboxes;
    private Wallet? _wallet;
    private ComplaintMeter? _complaint;
    private byte _shift = 1;

    private NpcDriver(IRouteConsole site, VehicleBody vehicle, int tileCm)
    {
        _site = site;
        _vehicle = vehicle;
        _tileCm = tileCm;
        Phase = NpcRoutePhase.Hired;
    }

    public static bool CanOperate(VehicleKind kind)
    {
        switch (kind)
        {
            case VehicleKind.Bike:
            case VehicleKind.MailTruck:
            case VehicleKind.Motorboat:
                return true;
            case VehicleKind.Rowboat:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public EntityId Depot => _site.Id;

    public EntityId Vehicle => _vehicle.Id;

    public NpcRoutePhase Phase { get; private set; }

    public static NpcHireResult TryHire(Wallet wallet, IRouteConsole site, VehicleBody vehicle, int tileCm)
    {
        if (wallet is null) throw new ArgumentNullException(nameof(wallet));
        if (site is null) throw new ArgumentNullException(nameof(site));
        if (vehicle is null) throw new ArgumentNullException(nameof(vehicle));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);

        if (!CanOperate(vehicle.Kind))
            return new NpcHireRejected(NpcHireReject.CannotOperate);
        if (site.Driver is not null)
            return new NpcHireRejected(NpcHireReject.AlreadyHired);
        if (!vehicle.IsParked || !vehicle.Tile(tileCm).Equals(site.ParkingZone))
            return new NpcHireRejected(NpcHireReject.NotParked);
        if (wallet.Balance.Value < HireCents)
            return new NpcHireRejected(NpcHireReject.InsufficientFunds);

        var paid = new Cents(HireCents);
        if (!wallet.TryDebit(paid))
            return new NpcHireRejected(NpcHireReject.InsufficientFunds);

        var driver = new NpcDriver(site, vehicle, tileCm);
        vehicle.SeatNpcDriver();
        site.BindDriver(driver);
        return new NpcHired(driver, paid);
    }

    public void BindDelivery(
        InventorySystem inventory,
        Destinations destinations,
        IReadOnlyDictionary<AddressId, DestinationId> mailboxes,
        Wallet wallet,
        ComplaintMeter? complaint = null,
        byte shift = 1)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        if (mailboxes is null) throw new ArgumentNullException(nameof(mailboxes));
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _complaint = complaint;
        if (shift < 1) throw new ArgumentOutOfRangeException(nameof(shift), shift, null);
        _shift = shift;
        _mailboxes = new Dictionary<AddressId, DestinationId>(mailboxes.Count);
        foreach (var pair in mailboxes)
            _mailboxes[pair.Key] = pair.Value;
    }

    public bool TryBegin(RoutingGraph graph, RouteAnchors anchors)
    {
        if (graph is null) throw new ArgumentNullException(nameof(graph));
        if (anchors is null) throw new ArgumentNullException(nameof(anchors));
        if (Phase != NpcRoutePhase.Hired && Phase != NpcRoutePhase.Done)
            return false;
        if (!_site.Route.TryRoundTrip(graph, _site.ParkingZone, anchors, out var trip))
            return false;

        var hops = new PathHop[trip.Waypoints.Count - 1];
        for (int i = 0; i < hops.Length; i++)
        {
            if (!graph.TryPath(trip.Waypoints[i], trip.Waypoints[i + 1], out var path))
                return false;
            bool delivers = i < trip.Stops.Count;
            hops[i] = new PathHop(
                CopyTiles(path.Tiles),
                MetresOf(path.Tiles, _tileCm),
                delivers ? trip.Stops[i] : (RouteStop?)null,
                _tileCm);
        }

        _graph = graph;
        _hops = hops;
        _routeTiles = CollectTiles(hops);
        _hop = 0;
        _metresAlong = 0;
        _insertWait = 0;
        Phase = hops.Length == 0 ? NpcRoutePhase.Done : NpcRoutePhase.Driving;
        if (Phase == NpcRoutePhase.Driving)
            SnapTo(_hops[0].Tiles[0], yaw: 0, speed: 0f);
        MaybeFlee();
        return true;
    }

    public void NoticeEnemies(IReadOnlyList<RouteEnemy> enemies)
    {
        if (enemies is null) throw new ArgumentNullException(nameof(enemies));
        var copy = new RouteEnemy[enemies.Count];
        for (int i = 0; i < enemies.Count; i++)
            copy[i] = enemies[i];
        _enemies = copy;
        MaybeFlee();
    }

    public void Step()
    {
        if (_vehicle.NpcInPassengerSeat)
            return;

        MaybeFlee();
        switch (Phase)
        {
            case NpcRoutePhase.Driving:
            case NpcRoutePhase.Fleeing:
                Drive();
                return;
            case NpcRoutePhase.Delivering:
                Deliver();
                return;
            case NpcRoutePhase.Hired:
            case NpcRoutePhase.Done:
                return;
            default:
            {
                NpcRoutePhase unseen = Phase;
                throw new ArgumentOutOfRangeException(nameof(Phase), unseen, null);
            }
        }
    }

    private void MaybeFlee()
    {
        if (Phase != NpcRoutePhase.Driving && Phase != NpcRoutePhase.Delivering)
            return;
        if (!VehicleRoute.EnemyWithin(_routeTiles, _tileCm, _enemies, FleeRadiusMetres))
            return;
        BeginFlee();
    }

    private void BeginFlee()
    {
        _insertWait = 0;
        var from = _vehicle.Tile(_tileCm);
        if (_graph is not null && _graph.TryPath(from, _site.ParkingZone, out var path))
        {
            _hops = new[]
            {
                new PathHop(CopyTiles(path.Tiles), MetresOf(path.Tiles, _tileCm), null, _tileCm)
            };
            _hop = 0;
            _metresAlong = 0;
            Phase = NpcRoutePhase.Fleeing;
            return;
        }

        Park();
        Phase = NpcRoutePhase.Done;
    }

    private void Drive()
    {
        if (_hop >= _hops.Length)
        {
            Park();
            Phase = NpcRoutePhase.Done;
            return;
        }

        var hop = _hops[_hop];
        if (hop.LengthMetres <= 0)
        {
            Arrive();
            return;
        }

        double speed = VehicleContext.ForKind(_vehicle.Kind).AtRatio(SpeedRatio).MaxSpeedMetersPerSecond;
        double step = speed * TickClock.TickDurationSeconds;
        double next = _metresAlong + step;
        if (next >= hop.LengthMetres)
        {
            Arrive();
            return;
        }

        _metresAlong = next;
        _vehicle.SetKinematics(PoseAt(hop, _metresAlong), (float)speed);
    }

    private void Arrive()
    {
        var hop = _hops[_hop];
        var end = hop.Tiles[hop.Tiles.Length - 1];
        SnapTo(end, YawAt(hop, hop.LengthMetres), 0f);
        _metresAlong = hop.LengthMetres;
        if (hop.Stop is RouteStop)
        {
            Phase = NpcRoutePhase.Delivering;
            _insertWait = 0;
            Deliver();
            return;
        }

        AdvanceHop();
    }

    private void Deliver()
    {
        if (_insertWait > 0)
        {
            _insertWait--;
            return;
        }

        if (!TryDeliverOne())
        {
            AdvanceHop();
            return;
        }

        if (!HasMatching())
        {
            AdvanceHop();
            return;
        }

        _insertWait = InsertPeriodTicks;
    }

    private void AdvanceHop()
    {
        _hop++;
        _metresAlong = 0;
        if (_hop >= _hops.Length)
        {
            Park();
            Phase = NpcRoutePhase.Done;
            return;
        }

        Phase = NpcRoutePhase.Driving;
    }

    private void Park()
    {
        SnapTo(_site.ParkingZone, yaw: 0, speed: 0f);
    }

    private void SnapTo(TileCoord tile, ushort yaw, float speed)
    {
        _vehicle.SetKinematics(TileCenter(tile, _tileCm, yaw), speed);
    }

    private bool TryDeliverOne()
    {
        if (_hops[_hop].Stop is not RouteStop stop)
            return false;
        if (_inventory is null || _destinations is null || _mailboxes is null || _wallet is null)
            return false;
        if (_vehicle.Cargo is not ContainerId cargo)
            return false;
        if (!_inventory.TryGetContainer(cargo, out var grid))
            return false;

        if (!TryFindMatching(grid, stop, out var entry, out var mailId, out var address))
            return false;
        if (!_mailboxes.TryGetValue(address, out var destination))
            return false;

        if (_inventory.Apply(Actor.System, new Withdraw(cargo, entry, Amount.Of(1))) is not InventoryAccepted accepted
            || accepted.Withdrawn is not MailStack taken)
            return false;

        var result = _destinations.TryDeliver(mailId, destination, _shift, _wallet, _complaint);
        if (result is Delivered)
            return true;
        if (result is Misdelivered)
            return false;

        _inventory.Apply(Actor.System, new Deposit(cargo, taken));
        return false;
    }

    private bool HasMatching()
    {
        if (_hops[_hop].Stop is not RouteStop stop)
            return false;
        if (_inventory is null || _vehicle.Cargo is not ContainerId cargo)
            return false;
        if (!_inventory.TryGetContainer(cargo, out var grid))
            return false;
        return TryFindMatching(grid, stop, out _, out _, out _);
    }

    private static bool TryFindMatching(
        GridContainer grid,
        RouteStop stop,
        out EntryId entry,
        out MailId mailId,
        out AddressId address)
    {
        entry = default;
        mailId = default;
        address = default;
        uint best = uint.MaxValue;
        bool found = false;
        foreach (var row in grid.Entries)
        {
            if (row.Stack is not MailStack mail) continue;
            if (!stop.Accepts(mail.Address)) continue;
            for (int i = 0; i < mail.Ids.Count; i++)
            {
                uint id = mail.Ids[i].Value;
                if (found && id >= best) continue;
                best = id;
                entry = row.Id;
                mailId = mail.Ids[i];
                address = mail.Address;
                found = true;
            }
        }

        return found;
    }

    private static PlayerPose PoseAt(in PathHop hop, double metres)
    {
        Locate(hop, metres, out var a, out var b, out double t, out ushort yaw);
        return PlayerPose.FromMeters(
            Lerp(a.X, b.X, t),
            Lerp(a.Y, b.Y, t),
            0,
            yaw);
    }

    private static ushort YawAt(in PathHop hop, double metres)
    {
        Locate(hop, metres, out _, out _, out _, out ushort yaw);
        return yaw;
    }

    private static void Locate(
        in PathHop hop,
        double metres,
        out WorldPoint a,
        out WorldPoint b,
        out double t,
        out ushort yaw)
    {
        var tiles = hop.Tiles;
        double tileM = hop.TileMetres;
        if (tiles.Length == 1 || hop.LengthMetres <= 0)
        {
            a = Center(tiles[0], tileM);
            b = a;
            t = 0;
            yaw = 0;
            return;
        }

        double at = 0;
        for (int i = 0; i < tiles.Length - 1; i++)
        {
            a = Center(tiles[i], tileM);
            b = Center(tiles[i + 1], tileM);
            double seg = Manhattan(tiles[i], tiles[i + 1]) * tileM;
            if (seg <= 0) continue;
            if (metres <= at + seg || i == tiles.Length - 2)
            {
                t = seg <= 0 ? 0 : (metres - at) / seg;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                yaw = YawToward(a, b);
                return;
            }

            at += seg;
        }

        a = Center(tiles[tiles.Length - 1], tileM);
        b = a;
        t = 0;
        yaw = 0;
    }

    private static WorldPoint Center(TileCoord tile, double tileM) =>
        new((tile.X + 0.5) * tileM, (tile.Y + 0.5) * tileM);

    private static PlayerPose TileCenter(TileCoord tile, int tileCm, ushort yaw)
    {
        double tileM = tileCm / 100.0;
        var at = Center(tile, tileM);
        return PlayerPose.FromMeters(at.X, at.Y, 0, yaw);
    }

    private static double MetresOf(IReadOnlyList<TileCoord> tiles, int tileCm)
    {
        if (tiles.Count < 2) return 0;
        double tileM = tileCm / 100.0;
        double metres = 0;
        for (int i = 0; i < tiles.Count - 1; i++)
            metres += Manhattan(tiles[i], tiles[i + 1]) * tileM;
        return metres;
    }

    private static TileCoord[] CollectTiles(PathHop[] hops)
    {
        int n = 0;
        for (int i = 0; i < hops.Length; i++)
            n += hops[i].Tiles.Length;
        var tiles = new TileCoord[n];
        int w = 0;
        for (int i = 0; i < hops.Length; i++)
        {
            var hop = hops[i].Tiles;
            for (int t = 0; t < hop.Length; t++)
                tiles[w++] = hop[t];
        }

        return tiles;
    }

    private static TileCoord[] CopyTiles(IReadOnlyList<TileCoord> tiles)
    {
        var copy = new TileCoord[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
            copy[i] = tiles[i];
        return copy;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static ushort YawToward(in WorldPoint from, in WorldPoint to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return 0;
        double turns = Math.Atan2(dx, dy) / (Math.PI * 2.0);
        if (turns < 0) turns += 1;
        return (ushort)((int)Math.Round(turns * 65536.0, MidpointRounding.AwayFromZero) & 65535);
    }

    private static int Manhattan(TileCoord a, TileCoord b)
    {
        int dx = a.X - b.X;
        if (dx < 0) dx = -dx;
        int dy = a.Y - b.Y;
        if (dy < 0) dy = -dy;
        return dx + dy;
    }

    private readonly record struct WorldPoint(double X, double Y);

    private readonly record struct PathHop(TileCoord[] Tiles, double LengthMetres, RouteStop? Stop, int TileCm)
    {
        public double TileMetres => TileCm / 100.0;
    }
}
