using PerformativeMail.Sim.Core;

namespace PerformativeMail.BotClient;

public abstract record BotCommand
{
    public sealed record Idle : BotCommand;

    public sealed record Move(float DirX, float DirY) : BotCommand;

    public sealed record TakeFromIntake(ContainerId Intake, EntryId Entry) : BotCommand;

    public sealed record Interact(EntityId Dest) : BotCommand;
}
