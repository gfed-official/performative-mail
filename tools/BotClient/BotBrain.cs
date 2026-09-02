using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.BotClient;

public static class BotBrain
{
    public static (BotCommand Command, BotState Next) Step(in BotView view, in BotState state, BotTuning tuning)
    {
        switch (state.Phase)
        {
            case BotPhase.Idle:
                return StepIdle(in view);
            case BotPhase.Fetching:
                return StepFetching(in view, in state, tuning);
            case BotPhase.Carrying:
                return StepCarrying(in view, in state, tuning);
            case BotPhase.Delivering:
                return StepDelivering(in view, in state, tuning);
            case BotPhase.Stuck:
                return (new BotCommand.Idle(), new BotState(BotPhase.Stuck, state.Target, state.SinceTick));
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "Unhandled BotPhase.");
        }
    }

    private static (BotCommand Command, BotState Next) StepIdle(in BotView view)
    {
        if (view.Held is { } held)
        {
            foreach (var mailbox in view.Mailboxes)
            {
                if (mailbox.Address == held.Address)
                    return (new BotCommand.Idle(), new BotState(BotPhase.Carrying, mailbox.Dest, default));
            }

            return (new BotCommand.Idle(), new BotState(BotPhase.Stuck, default, default));
        }

        if (view.Intake is { FirstMailEntry: not null })
            return (new BotCommand.Idle(), new BotState(BotPhase.Fetching, default, default));

        return (new BotCommand.Idle(), new BotState(BotPhase.Idle, default, default));
    }

    private static (BotCommand Command, BotState Next) StepFetching(in BotView view, in BotState state, BotTuning tuning)
    {
        var intake = view.Intake;
        if (intake is null || intake.FirstMailEntry is not { } entry)
            return (new BotCommand.Idle(), state);

        if (Distance(view.At, intake.At) <= tuning.ReachMetres)
            return (new BotCommand.TakeFromIntake(intake.Container, entry), new BotState(BotPhase.Idle, default, default));

        return (Toward(view.At, intake.At), new BotState(BotPhase.Fetching, state.Target, state.SinceTick));
    }

    private static (BotCommand Command, BotState Next) StepCarrying(in BotView view, in BotState state, BotTuning tuning)
    {
        if (!TryMailbox(view.Mailboxes, state.Target, out var mailbox))
            return (new BotCommand.Idle(), state);

        if (Distance(view.At, mailbox.At) <= tuning.ReachMetres)
            return (new BotCommand.Interact(mailbox.Dest), new BotState(BotPhase.Delivering, mailbox.Dest, view.Tick));

        return (Toward(view.At, mailbox.At), new BotState(BotPhase.Carrying, state.Target, state.SinceTick));
    }

    private static (BotCommand Command, BotState Next) StepDelivering(in BotView view, in BotState state, BotTuning tuning)
    {
        if (view.Held is null)
            return (new BotCommand.Idle(), new BotState(BotPhase.Idle, default, default));

        var elapsed = view.Tick - state.SinceTick;
        if (elapsed < (uint)tuning.HoldTicks)
            return (new BotCommand.Interact(state.Target), new BotState(BotPhase.Delivering, state.Target, state.SinceTick));
        if (elapsed >= (uint)tuning.StuckAfterTicks)
            return (new BotCommand.Idle(), new BotState(BotPhase.Stuck, default, default));

        return (new BotCommand.Interact(state.Target), new BotState(BotPhase.Delivering, state.Target, state.SinceTick));
    }

    private static bool TryMailbox(IReadOnlyList<Mailbox> mailboxes, EntityId dest, out Mailbox mailbox)
    {
        foreach (var candidate in mailboxes)
        {
            if (candidate.Dest == dest)
            {
                mailbox = candidate;
                return true;
            }
        }

        mailbox = default!;
        return false;
    }

    private static float Distance(BotPos a, BotPos b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static BotCommand.Move Toward(BotPos from, BotPos to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        return new BotCommand.Move(dx / length, dy / length);
    }
}
