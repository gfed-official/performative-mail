using PerformativeMail.BotClient;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Bot.Tests;

public sealed class BotBrainTests
{
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);
    private static readonly EntityId Self = new(1);
    private static readonly EntityId DestOak = new(10);
    private static readonly ContainerId Hotbar = new(1);
    private static readonly ContainerId IntakeId = new(2);
    private static readonly EntryId FirstMail = new(5);
    private static readonly BotTuning Tuning = new();
    private static readonly BotPos Origin = new(0, 0);
    private static readonly BotPos Far = new(10, 0);
    private static readonly BotPos Near = new(2, 0);

    [Fact]
    public void Idle_HeldMatchesMailbox_GoesCarrying()
    {
        var view = View(held: Letter(Oak), mailboxes: new[] { OakBox(Far) });
        var state = Idle();

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Carrying, DestOak, default), next);
    }

    [Fact]
    public void Idle_HeldWithNoMatchingMailbox_GoesStuck()
    {
        var view = View(held: Letter(Oak), mailboxes: new[] { new Mailbox(new EntityId(11), Elm, Far) });
        var state = Idle();

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Stuck, default, default), next);
    }

    [Fact]
    public void Idle_IntakeHasFirstMailEntry_GoesFetching()
    {
        var view = View(intake: new IntakeView(IntakeId, Far, FirstMail));
        var leftover = new BotState(BotPhase.Idle, DestOak, 7);

        var (command, next) = BotBrain.Step(in view, in leftover, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Fetching, default, default), next);
    }

    [Fact]
    public void Idle_Nothing_StaysIdle()
    {
        var view = View();
        var state = Idle();

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Idle, default, default), next);
    }

    [Fact]
    public void Fetching_Far_MovesTowardIntake()
    {
        var view = View(intake: new IntakeView(IntakeId, Far, FirstMail));
        var state = new BotState(BotPhase.Fetching, default, default);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Move(1f, 0f), command);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Fetching_WithinReach_TakesFromIntake()
    {
        var view = View(at: Near, intake: new IntakeView(IntakeId, Origin, FirstMail));
        var state = new BotState(BotPhase.Fetching, default, default);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.TakeFromIntake(IntakeId, FirstMail), command);
        Assert.Equal(new BotState(BotPhase.Idle, default, default), next);
    }

    [Fact]
    public void Carrying_Far_MovesTowardDest()
    {
        var view = View(held: Letter(Oak), mailboxes: new[] { OakBox(Far) });
        var state = new BotState(BotPhase.Carrying, DestOak, default);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Move(1f, 0f), command);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Carrying_WithinReach_InteractsAndDelivers()
    {
        var view = View(tick: 40, at: Near, held: Letter(Oak), mailboxes: new[] { OakBox(Origin) });
        var state = new BotState(BotPhase.Carrying, DestOak, default);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Interact(DestOak), command);
        Assert.Equal(new BotState(BotPhase.Delivering, DestOak, 40), next);
    }

    [Fact]
    public void Delivering_HeldNull_GoesIdle()
    {
        var view = View(tick: 50);
        var state = new BotState(BotPhase.Delivering, DestOak, 40);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Idle, default, default), next);
    }

    [Fact]
    public void Delivering_ElapsedBelowHoldTicks_KeepsInteract()
    {
        var view = View(tick: 45, held: Letter(Oak), mailboxes: new[] { OakBox(Origin) });
        var state = new BotState(BotPhase.Delivering, DestOak, 40);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Interact(DestOak), command);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Delivering_ElapsedAtStuckAfterTicks_GoesStuck()
    {
        var view = View(tick: 940, held: Letter(Oak), mailboxes: new[] { OakBox(Origin) });
        var state = new BotState(BotPhase.Delivering, DestOak, 40);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(new BotState(BotPhase.Stuck, default, default), next);
    }

    [Fact]
    public void Stuck_StaysStuck()
    {
        var view = View(held: Letter(Oak), mailboxes: new[] { OakBox(Origin) });
        var state = new BotState(BotPhase.Stuck, DestOak, 3);

        var (command, next) = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(new BotCommand.Idle(), command);
        Assert.Equal(state, next);
    }

    [Fact]
    public void Step_IsDeterministic()
    {
        var view = View(at: Origin, intake: new IntakeView(IntakeId, new BotPos(3f, 4f), FirstMail));
        var state = new BotState(BotPhase.Fetching, default, default);

        var first = BotBrain.Step(in view, in state, Tuning);
        var second = BotBrain.Step(in view, in state, Tuning);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Move_IsUnitVector()
    {
        var view = View(at: Origin, intake: new IntakeView(IntakeId, new BotPos(3f, 4f), FirstMail));
        var state = new BotState(BotPhase.Fetching, default, default);

        var move = Assert.IsType<BotCommand.Move>(BotBrain.Step(in view, in state, Tuning).Command);
        var length = MathF.Sqrt(move.DirX * move.DirX + move.DirY * move.DirY);

        Assert.InRange(length, 1f - 1e-4f, 1f + 1e-4f);
    }

    private static BotState Idle() => new(BotPhase.Idle, default, default);

    private static MailStack Letter(AddressId address) =>
        MailStack.Single(new MailKindId(1), address, new MailId(1));

    private static Mailbox OakBox(BotPos at) => new(DestOak, Oak, at);

    private static BotView View(
        uint tick = 0,
        BotPos? at = null,
        MailStack? held = null,
        IntakeView? intake = null,
        IReadOnlyList<Mailbox>? mailboxes = null)
    {
        return new BotView(
            tick,
            Self,
            at ?? Origin,
            held,
            Hotbar,
            intake,
            mailboxes ?? Array.Empty<Mailbox>(),
            0);
    }
}
