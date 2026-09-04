using PerformativeMail.Sim;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Server;

public sealed class ArcadeBoot
{
    public ArcadeBoot(
        SimWorld world,
        WorldOffer offer,
        RunSettings settings,
        WorldTables tables,
        BalanceTable balance,
        Destinations destinations,
        ShiftClock clock)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Offer = offer;
        Settings = settings;
        Tables = tables ?? throw new ArgumentNullException(nameof(tables));
        Balance = balance ?? throw new ArgumentNullException(nameof(balance));
        Destinations = destinations ?? throw new ArgumentNullException(nameof(destinations));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public SimWorld World { get; }

    public WorldOffer Offer { get; }

    public RunSettings Settings { get; }

    public WorldTables Tables { get; }

    public BalanceTable Balance { get; }

    public Destinations Destinations { get; }

    public ShiftClock Clock { get; }
}
