using PerformativeMail.Sim.Net;
using Xunit.Abstractions;

namespace PerformativeMail.Net.Tests.Soak;

[Collection(SoakCollection.Name)]
public sealed class EightClientSoakTests
{
    private readonly ITestOutputHelper _output;

    public EightClientSoakTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void EightClientSoak_Criterion1_HashesMatchFor18000Ticks()
    {
        var session = SoakSession.Start(new SoakConfig { DurationTicks = SoakDuration.Criterion1Ticks });
        var report = session.Run();

        _output.WriteLine($"witnesses={report.Witnesses.Count}");
        if (report.Mismatches.Count > 0)
        {
            var first = report.Mismatches[0];
            _output.WriteLine(
                $"first mismatch tick={first.Tick} container={first.Container.Value} version={first.Version.Value} viewers={first.ViewerHashes.Count}");
        }

        Assert.Equal(18_000u, report.TicksRun);
        Assert.Equal(SoakRoster.SeatCount, report.ConnectedSeats);
        Assert.Empty(report.Mismatches);
        Assert.NotEmpty(report.Witnesses);
        Assert.Contains(report.Witnesses, w => w.Version.Value > 0);
        Assert.Contains(report.Witnesses, w => w.Container.Equals(session.Server.World.Intake));
        Assert.True(report.Criterion1);
        Assert.Equal(SoakRoster.SeatCount, session.Roster.Seats.Count);
        Assert.Equal(SoakRoster.RealCount, session.Roster.Seats.Count(s => s.Kind == SeatKind.Real));
        Assert.Equal(SoakRoster.BotCount, session.Roster.Seats.Count(s => s.Kind == SeatKind.Bot));
        for (int i = 0; i < session.Roster.Seats.Count; i++)
            Assert.True(session.Roster.Seats[i].Client.InventoryEventCount > 0);
    }
}
