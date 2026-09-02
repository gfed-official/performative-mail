using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.BotClient;

public readonly record struct BotPos(float X, float Y);

public record Mailbox(EntityId Dest, AddressId Address, BotPos At);

public record IntakeView(ContainerId Container, BotPos At, EntryId? FirstMailEntry);

public record BotView(
    uint Tick,
    EntityId Self,
    BotPos At,
    MailStack? Held,
    ContainerId Hotbar,
    IntakeView? Intake,
    IReadOnlyList<Mailbox> Mailboxes,
    int WalletCents);
