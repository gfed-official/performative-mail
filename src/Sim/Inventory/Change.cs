using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Inventory;

public readonly record struct Entry(EntryId Id, Stack Stack, Placement At);

public abstract record Change;

public sealed record Upsert(Entry Entry) : Change;

public sealed record Remove(EntryId Id) : Change;

public sealed record Reset(ContainerSpec Spec, IReadOnlyList<Entry> Entries) : Change;

public sealed record ContainerDelta(
    ContainerId Container,
    ContainerVersion Version,
    ulong Hash,
    IReadOnlyList<Change> Changes);
