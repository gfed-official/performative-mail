using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public abstract record SessionRole
{
    private SessionRole()
    {
    }

    public sealed record Listening(HostAdvertisement Advertisement) : SessionRole;

    public sealed record Guest(JoinTarget Target) : SessionRole;
}

public abstract record PlaySession
{
    private PlaySession()
    {
    }

    public sealed record Menu : PlaySession
    {
        public static Menu Instance { get; } = new();
    }

    public sealed record Connecting(SessionRole Role, TimeSpan? Deadline) : PlaySession
    {
        public string Describe()
        {
            switch (Role)
            {
                case SessionRole.Listening listening:
                    return $"Hosting on {listening.Advertisement}…";
                case SessionRole.Guest guest:
                    return $"Connecting to {guest.Target}…";
                default:
                    throw new ArgumentOutOfRangeException(nameof(Role), Role, null);
            }
        }
    }

    public sealed record Playing : PlaySession
    {
        public Playing(
            SessionRole role,
            EntityId localPlayer,
            IReadOnlyList<PawnView> pawns,
            in HudSnapshot hud,
            WorldTables? world,
            OverlayReplica? overlay,
            IReadOnlyList<ResourceNodeView>? resources = null,
            ConstructFrame? constructs = null,
            IReadOnlyList<VehicleView>? vehicles = null,
            IReadOnlyList<MapPing>? pings = null)
        {
            Role = role;
            LocalPlayer = localPlayer;
            var copy = new PawnView[pawns.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = pawns[i];
            Pawns = copy;
            Hud = hud;
            World = world;
            Overlay = overlay;
            if (resources is null || resources.Count == 0)
            {
                Resources = Array.Empty<ResourceNodeView>();
            }
            else
            {
                var harvested = new ResourceNodeView[resources.Count];
                for (int i = 0; i < harvested.Length; i++)
                    harvested[i] = resources[i];
                Resources = harvested;
            }
            Constructs = constructs ?? ConstructFrame.Empty;
            if (vehicles is null || vehicles.Count == 0)
            {
                Vehicles = Array.Empty<VehicleView>();
            }
            else
            {
                var spawned = new VehicleView[vehicles.Count];
                for (int i = 0; i < spawned.Length; i++)
                    spawned[i] = vehicles[i];
                Vehicles = spawned;
            }
            if (pings is null || pings.Count == 0)
            {
                Pings = Array.Empty<MapPing>();
            }
            else
            {
                var marks = new MapPing[pings.Count];
                for (int i = 0; i < marks.Length; i++)
                    marks[i] = pings[i];
                Pings = marks;
            }
        }

        public SessionRole Role { get; }

        public EntityId LocalPlayer { get; }

        public IReadOnlyList<PawnView> Pawns { get; }

        public HudSnapshot Hud { get; }

        public WorldTables? World { get; }

        public OverlayReplica? Overlay { get; }

        public IReadOnlyList<ResourceNodeView> Resources { get; }

        public ConstructFrame Constructs { get; }

        public IReadOnlyList<VehicleView> Vehicles { get; }

        public IReadOnlyList<MapPing> Pings { get; }
    }

    public sealed record Failed(FailReason Reason) : PlaySession;
}
