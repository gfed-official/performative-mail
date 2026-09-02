# Candidate 1: entries-first grid, ops that plan into Changes, one reducer on both ends

## Problem

M0 U1+U2 need the Sim-side grid inventory and the InventoryOp request/validate/apply path that the server calls, with per-container versioning, and they must pass the 10 000-op concurrent chest fuzz (never duplicate or lose an item) and the 8-client soak (server and client container hashes equal at every version). The Sim assembly is netstandard2.1 with no Godot references, and the current scaffold is thin. It has `SimWorld.ApplyRequest(byte[])` with no sender, an empty `SimEvent`, `EntityId` in Core, and an empty `Inventory/` folder. Three facts from the spec shape the answer. The server is a single writer (one 30 Hz tick loop drains requests in arrival order), so "concurrent" means two players' requests built on stale views racing in a queue, not threads. Conflicts are semantic per chapter 03 §3.3 ("cell now occupied" rejects), so a version mismatch alone must not reject. And the desync criterion compares server and client hashes per version, so the client must be able to reproduce the server's container state exactly from what it receives.

## Usage (caller's view)

The consumer imports `PerformativeMail.Sim.Inventory` and talks to one object, `InventorySystem`. Ops name entries (stable stack ids), never cells. One call, `Apply(actor, op)`, authorizes, plans, commits, versions, hashes, and returns either `Accepted(deltas)` or `Rejected(reason)`. Nothing is ever half-applied. The same class is the client's replica (`ApplyDelta`) and its prediction shadow (`Fork()` then `Apply`).

### Call site 1: the request path in SimWorld (what the server calls)

```csharp
// src/Sim/SimWorld.cs
public sealed class SimWorld
{
    public InventorySystem Inventory { get; }
    private readonly List<InventoryResultEvent> _pendingEvents = new();

    public SimWorld(IStackCatalog catalog) => Inventory = new InventorySystem(catalog);

    // Sender comes from the connection, never from the payload (scaffold change: add the parameter).
    public void ApplyRequest(EntityId sender, ReadOnlySpan<byte> payload)
    {
        if (!InventoryCodec.TryParseRequest(payload, out var request)) return;   // Sim/Net boundary: bytes -> domain
        switch (request)
        {
            case InventoryRequest r:
                var result = Inventory.Apply(Actor.Player(sender), r.Op);
                if (result is Accepted { Withdrawn: { } dropped })
                    /* TODO(U6) World.SpawnItem(sender, dropped) */ ;
                _pendingEvents.Add(new InventoryResultEvent(sender, r.ReqId, result));
                break;
            case OpenContainerRequest o:
                // TODO(U6) range <= 2.5 m via Players before opening; Inventory does not know positions.
                _pendingEvents.Add(new InventoryResultEvent(sender, o.ReqId, Inventory.Open(sender, o.Container)));
                break;
            case CloseContainerRequest c:
                Inventory.Close(sender, c.Container);
                break;
        }
    }
}
```

```csharp
// src/Server/ServerRuntime.cs, at Events.Flush
foreach (var ev in World.DrainInventoryEvents())
{
    switch (ev.Result)
    {
        case Accepted a:
            foreach (var delta in a.Deltas)
                foreach (var viewer in World.Inventory.ViewersOf(delta.Container))
                    Send(viewer, InventoryCodec.EncodeEvent(delta, reqId: viewer.Equals(ev.Sender) ? ev.ReqId : null));
            break;
        case Rejected r:
            Send(ev.Sender, InventoryCodec.EncodeRejected(ev.ReqId, r.Reason));
            break;
    }
}
```

### Call site 2: mail spawn into Intake (a Sim system as actor)

```csharp
// src/Sim/Mail/MailSpawner.cs (U6)
foreach (var item in batch)
{
    var stack = MailStack.Single(item.Kind, item.Address, item.Id);
    if (_inventory.Apply(Actor.System, new Deposit(_intake, stack)) is Rejected)
        _backlog.Enqueue(item);   // Intake full; the spec's backlog. Deposit is all-or-nothing.
}
```

### Call site 3: the M0 acceptance fuzz (the lever that proves criterion 3 and criterion 1 in-process)

```csharp
// tests/Sim.Tests/Inventory/ConcurrentChestFuzz.cs
[Fact]
public void TwoPlayers_OneChest_10kOps_NeverDuplicateOrLose()
{
    var server = new InventorySystem(TestCatalog.Default);
    var a = new EntityId(0x01000001); var b = new EntityId(0x01000002);
    var chest = server.CreateContainer(ContainerSpec.Chest);
    var invA  = server.CreateContainer(ContainerSpec.BaseInventory, owner: a);
    var invB  = server.CreateContainer(ContainerSpec.BaseInventory, owner: b);
    Seed.Letters(server, chest, addresses: 6, count: 40);          // Deposit as Actor.System
    Seed.Packages(server, invA, invB);
    Assert.IsType<Accepted>(server.Open(a, chest));
    Assert.IsType<Accepted>(server.Open(b, chest));

    var ledger  = MailLedger.Of(server);                            // multiset of MailIds + item counts
    var replica = new InventorySystem(TestCatalog.Default);         // a client that only sees deltas
    foreach (var c in server.Containers) replica.ApplyDelta(server.Snapshot(c.Id));

    var rng = new Random(seed: 7);
    var stale = server.Fork();                                       // what a lagging client believes
    for (int i = 0; i < 10_000; i++)
    {
        if (i % 5 == 0) stale = server.Fork();                       // refresh the lagging view now and then
        var actor = rng.Next(2) == 0 ? a : b;
        var op = OpGen.Random(rng, view: stale, actor, mine: actor.Equals(a) ? invA : invB, shared: chest);
        var result = server.Apply(Actor.Player(actor), op);          // arrival order, single writer, no locks

        if (result is Accepted acc)
            foreach (var d in acc.Deltas)
                Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(d));   // same reducer, same hash

        Assert.Equal(ledger, MailLedger.Of(server));                 // conservation: no dup, no loss
        Assert.Null(server.CheckInvariants());                       // cells == projection of entries; no overlap
    }
    foreach (var c in server.Containers)
        Assert.Equal(c.Hash, replica[c.Id].Hash);                    // criterion 1 without a network
}
```

### Call site 4: client prediction and rollback (Client's concern; shown to prove the API carries it)

```csharp
// src/Client/ClientInventory.cs
void OnServerEvent(ContainerDelta delta, uint? ackedReqId)
{
    if (_authoritative.ApplyDelta(delta) != ReplicaResult.Applied) RequestResync(delta.Container);
    if (ackedReqId is { } id) _pending.RemoveAll(p => p.ReqId == id);
    Repredict();
}
void OnServerRejected(uint reqId) { _pending.RemoveAll(p => p.ReqId == reqId); Repredict(); Clunk(); }
void Repredict()
{
    _predicted = _authoritative.Fork();                              // rollback is re-derivation, nothing to undo
    foreach (var p in _pending) _predicted.Apply(Actor.System, p.Op);
}
```

## Shape

**Data structures.** Every id is a branded `readonly record struct` in `Core/Ids.cs` beside `EntityId` (`MailId`, `AddressId`, `MailKindId`, `ItemDefId`, `ContainerId`, `EntryId`, `ContainerVersion`), per type-system-discipline. A `Stack` is a sum type `MailStack(kind, address, ids)` | `ItemStack(item, count)` with `Count >= 1` enforced at construction; its `StackKey` (kind and address for mail, def for items) is the merge-compatibility key, built only via factories so an item key can never carry an address. An `Entry` is `(EntryId, Stack, Placement)`; `Placement` is origin plus a rotation flag and the covered rectangle is derived from the catalog footprint, so width and height are never stored twice. `Placement.For` normalizes `Rotated` to false for square footprints so equal layouts hash equal. `ContainerShape` is a grid, or `Slot` (the cursor, a 1x1 grid whose one entry may have any footprint), so "the cursor holds at most one entry" falls out of the cell count instead of a special case.

`GridContainer` holds `Dictionary<EntryId, Entry>` as the truth and a private `uint[] cells` occupancy projection. Every dominant access pattern traces through those two. `EntryAt(cell)` is one array read. A placement check is one read per covered cell (≤ 40). First-fit and "merges first" are one row-major scan of the same array, so no stack-key index is needed. Full state, hash, sort, and weight enumerate entries (≤ 320). Nothing is deferred to a later index.

**The mutation language.** `Change` is `Upsert(Entry)` | `Remove(EntryId)` | `Reset(spec, entries)`. `GridContainer.Apply(Change)` is the only mutator and is the reducer both the server and the client run, per model-the-domain (a reducer instead of ad hoc mutations). `Upsert` vacates the entry's old cells if present and occupies the new ones; it covers place, move, split, restack. `Reset` covers sort results, open (full state), and resync, so full state is just a delta.

**The request path.** `InventoryOp` is a sum type `Move` | `QuickMove` | `Sort` | `Withdraw` | `Deposit`. Each op has `internal abstract TryPlan(inv, out Plan, out RejectReason)`, a read-only walk of current state producing the ordered `Change`s. Putting planning on the op is how C# gets exhaustiveness here (an op without planning logic does not compile) since it cannot close a record hierarchy. `Plan` has an internal constructor, so `Commit` only ever receives validated change lists and cannot fail. That is the structural fix for the actual dup/loss hazard, a two-container move that mutates the source then fails on the destination, per encode-lessons-in-structure (unrepresentable rather than checked). `Commit` groups steps by container in first-touch order, applies, bumps the version once per container, recomputes the hash incrementally, and returns one `ContainerDelta(container, version, hash, changes)` per touched container. A plan with zero steps yields zero deltas and no bump, so moving an entry onto its own footprint is idempotent, per make-operations-idempotent.

`move`, `split`, and `merge` on the wire are one `Move`. `Count < source count` splits, a single compatible stack under the target cells merges up to `maxStack` with the excess staying in the source, free cells place, and anything else rejects `Occupied`. `QuickMove` and `Deposit` share `GridContainer.PlanFit` (merges first, then unrotated, then rotated, row-major); `QuickMove` moves what fits and treats a same-container request as an idempotent no-op, `Deposit` is all-or-nothing so the spawner gets a clean backlog signal. `Withdraw` is how items leave the system (drop and, in U3, deliver via `Destinations.TryDeliver`); the stack returns in `Accepted.Withdrawn`. `Sort` merges compatible stacks, packs first-fit-decreasing grouped by the key onto an empty shape, and emits one `Reset`; if FFD cannot fit what previously fit, it rejects `CannotRepack` and changes nothing.

**Concurrency.** The chest is a genuine single canonical object, so per separate-before-serializing-shared-state the serialization is structural. One writer (the tick thread) applies requests in arrival order. There is no lock and no version-CAS. Requests identify entries by `EntryId`, so a request built on a stale view still means "that stack" after the other player has moved things; only real conflicts (`Occupied`, `UnknownEntry`, `StackFull`, `BadCount`) reject. Versions sequence deltas per container and the incremental hash rides on every delta, so a replica detects a gap (`VersionGap`) or divergence (`HashMismatch`) on the spot and resyncs with a `Reset`.

**Authorization and viewing.** `Actor` is `Player(EntityId)` | `System`. `InventorySystem` owns who has what open through an owners map (player containers are always open to their owner) and `_external`, a `Dictionary<EntityId, ContainerId>` with a single slot per player, so "at most one external container, opening another closes the first" is a property of the type. `Authorize` requires every container the op touches to be open to a Player actor and forbids `Deposit` from players; `System` bypasses. `ViewersOf` scans ≤ 8 players; that scan is the index. Range checks stay with the caller who knows positions (SimWorld/Players) at open time, per boundary-discipline.

**Boundaries.** `Sim/Net/InventoryCodec` parses bytes into `InventoryRequest(reqId, op)` and never produces `Deposit`; `reqId` is correlation, so it stays in the codec and SimWorld's event, never inside `InventorySystem`. Wire and domain types never mix on the public surface.

**Interface depth.** Public surface: `Apply`, `Open`, `Close`, `ViewersOf`, `Snapshot`, `CreateContainer`, `DestroyContainer`, an indexer plus `TryGetContainer`, `ApplyDelta`, `Fork`, `CheckInvariants`. Behind `Apply` sit rotation, footprint checks, stacking by address, split remainder handling, first-fit with merge passes, FFD repacking, atomic two-container commits, versioning, hashing, and event shaping. Exposed to callers: op construction from entry ids, the reject reason enum, and deltas to route. Deliberately not done: no undo log (rollback is re-derivation), no reqId dedup in Sim (reliable-ordered transport; belongs in the server session if ever needed), no positions in Inventory, no wire types in Sim.Inventory.

## Synthesis decision

*Filled in by arena.*

## Tradeoffs accepted

- We accept a derived occupancy array inside `GridContainer` (a second representation) in exchange for O(1) cell queries and O(w·h) placement checks; it is private, the only writers are `Apply(Change)`, and `CheckInvariants` rebuilds and compares it in tests.
- We accept that C# cannot close the `InventoryOp` and `Change` hierarchies; ops get compile-time exhaustiveness through the abstract `TryPlan`, and the reducer's switch over `Change` has a throwing default.
- We accept `Fork()` deep copies for client prediction and for stale views in the fuzz (≤ 5 containers, ≤ 400 entries per fork) in exchange for having no stored preview state that can drift.
- We accept that predicted `EntryId`s for splits are provisional and will differ from the server's; the shadow is discarded on every authoritative delta.
- We accept one version bump per container per op, not per change, so a client applies a delta atomically.
- We accept an XOR-fold incremental hash (splitmix over each entry, entries have unique ids so nothing cancels); it detects desync, it is not a MAC.
- We accept the `Accepted.Withdrawn` nullable (non-null only for `Withdraw`) rather than a second result hierarchy.
- We accept `TryPlan` bumping the entry-id allocator during planning; ids are never reused, so a rejected plan burns an id harmlessly.
- We accept `Open`/`Close` as methods rather than ops; they mutate the viewing registry, a different body of knowledge from container contents, and giving them a `Plan` would turn `Plan` into a bag of optional fields.
- We accept a 1x7 Sim hotbar; the reserved "hands" slot is a Client rendering concern, which avoids blocked-cell masks in the shape.

## Alternatives considered

- **Cells-primary grid plus a mutex.** Each cell holds an item reference; a 2x2 item is four references; ops mutate directly; a lock "for safety." The lock solves a problem that does not exist (single writer) and misses the one that does (partial application across two containers). Identity is implicit, so requests must name cells and go stale the moment anyone else moves something. Hash equality would rest on the client re-implementing every mutation identically by hand. Exposes cell bookkeeping and hides nothing.
- **Version-CAS optimistic concurrency.** The request carries the container version it was built against; mismatch rejects. Simple to reason about, but on a shared chest nearly every op from the second player fails, which contradicts §3.3 (only "cell now occupied" rejects) and makes the client coordinate on versions. Hides little, exposes a coordination burden.
- **Immutable container plus pure `Apply(state, op) -> state'`.** Thread safety we do not need, per-op allocation, and no persistent collections in netstandard2.1 without a package. Fork-and-replay gives the client the same derived-preview story with mutable containers and explicit cloning.
- **Command objects with `Execute`/`Undo` for client rollback.** Stored undo stacks must stay in sync with authoritative updates that interleave with them. A derived preview has nothing to keep in sync.
- **Event-sourced containers (the op log is the truth).** Replay cost on join and resync when the spec already mandates a full-state send on open; deltas plus `Reset` is the practical middle.

## Open questions and risks

- Should `SimWorld.ApplyRequest` gain the `EntityId sender` parameter (from the connection), as sketched, rather than trusting a sender field in the payload?
- Is partial success the right contract for `QuickMove` (move what fits, `Accepted` with deltas) while `Deposit` stays all-or-nothing, or should both be all-or-nothing?
- When FFD cannot repack a layout that previously fit, is `CannotRepack` (no change) acceptable, or should `Sort` fall back to a best-effort partial packing?
- Should `ViewersOf` be evaluated at flush time (as sketched) or captured into the result at apply time, given a `Close` can land between the two within a tick?
- Is the chapter 03 §3.2 weight penalty per item (20 letters = 20 points) or per stack? `WeightPoints` is sketched per item; this affects Players, not the fuzz.
- Is a 1x7 Sim hotbar acceptable, or does the wire schema require a 1x8 grid with a blocked cell?
- `record` types on netstandard2.1 need the `IsExternalInit` polyfill. It is included at the end of `types.cs`, and the sketch was compiled both with it (clean) and without it (fails) to confirm.
- Predicted `EntryId`s for splits differ from the server's, so the client UI should track a dragged stack by (container, cell) while a split is pending.

## Next implementation step

Write `Core/Ids.cs`, `GridContainer` with `Apply(Change)`, the incremental hash, and `CheckInvariants`, then `Move.TryPlan` and `InventorySystem.Apply`/`Commit`, and land the fuzz harness skeleton (`OpGen`, `MailLedger`) against them before any other op.
