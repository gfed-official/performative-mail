using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using Xunit;

namespace PerformativeMail.Sim.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void TickClock_TickHz_IsThirty()
    {
        Assert.Equal(30, TickClock.TickHz);
        Assert.Equal(1800, TickClock.TicksFromSeconds(60));
    }

    [Fact]
    public void SimWorld_Tick_UpdatesCurrentTick()
    {
        var world = new SimWorld();
        world.Tick(7);
        Assert.Equal(7u, world.CurrentTick);
    }

    [Fact]
    public void EntityId_PacksClassAndCounter()
    {
        var id = EntityId.FromClassAndCounter(3, 42);
        Assert.Equal(3, id.Class);
        Assert.Equal(42u, id.Counter);
    }
}
