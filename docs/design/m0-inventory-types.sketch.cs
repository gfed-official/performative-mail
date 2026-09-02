// Candidate 1: Performative Mail M0 (U1+U2) Sim-side inventory.
//
// Shape in one paragraph. Entries (identity-bearing stacks) are the truth and the cell grid is a
// private projection. Every InventoryOp plans itself into an ordered list of primitive Changes
// against current state; Commit applies those Changes and stamps one version and one incremental
// hash per touched container. The client applies the same Changes with the same reducer, so server
// and client hashes agree by construction. Concurrency is the single-writer tick loop applying
// requests in arrival order; conflicts are semantic (Occupied, UnknownEntry), never version-based.
//
// Section markers name the file each block lands in. Bodies are NotImplementedException or
// // TODO pseudocode. Compiles against netstandard2.1 (LangVersion latest) with the IsExternalInit
// polyfill at the end. Depends on the existing Core/EntityId.cs.
//
// Module map
//   src/Sim/Core/Ids.cs                 every branded id (beside EntityId)
//   src/Sim/Inventory/Geometry.cs       Footprint, Cell, CellRect, Placement, ContainerShape
//   src/Sim/Inventory/Stack.cs          StackKey, Stack, MailStack, ItemStack, IStackCatalog, ContainerSpec
//   src/Sim/Inventory/Change.cs         Entry, Change (Upsert | Remove | Reset), ContainerDelta
//   src/Sim/Inventory/GridContainer.cs  entries + occupancy projection + reducer + queries + hash
//   src/Sim/Inventory/InventoryOp.cs    Actor, InventoryOp (Move | QuickMove | Sort | Withdraw | Deposit), Plan, results
//   src/Sim/Inventory/InventorySystem.cs  aggregate root: Apply / Open / Close / ViewersOf / Snapshot / ApplyDelta / Fork
//   src/Sim/Net/InventoryCodec.cs       wire <-> domain; reqId lives here
//   src/Sim/SimWorld.cs                 dispatch seam (proposed signature change: sender parameter)
//   tests/Sim.Tests/Inventory/          fuzz harness helpers
//
// Reading order to trace a request: InventoryCodec.TryParseRequest -> SimWorld.ApplyRequest ->
// InventorySystem.Apply -> <op>.TryPlan -> InventorySystem.Commit -> GridContainer.Apply(Change).

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Net;

// ============================================================================================
// file: src/Sim/Core/Ids.cs   (U1)
// uint underneath, never interchangeable. Suggest making EntityId a readonly record struct too so
// it gets IEquatable and works as a fast dictionary key; the sketch below uses it as one.
// ============================================================================================
namespace PerformativeMail.Sim.Core
{
    /// <summary>Server-allocated, unique per run, never reused. Owned by Mail/.</summary>
    public readonly record struct MailId(uint Value);

    /// <summary>district:street:number[:unit]. Unit 0 means none. One uint on the wire.</summary>
    public readonly record struct AddressId(byte District, byte Street, byte Number, byte Unit)
    {
        public uint Packed => throw new NotImplementedException();
        public static AddressId Unpack(uint packed) => throw new NotImplementedException();
    }

    /// <summary>Index of a MailKindDef after content load (chapter 08 §1).</summary>
    public readonly record struct MailKindId(ushort Value);

    /// <summary>Index of an ItemDef after content load.</summary>
    public readonly record struct ItemDefId(ushort Value);

    /// <summary>A container instance. Allocated by InventorySystem on the server, never reused in a run.</summary>
    public readonly record struct ContainerId(uint Value);

    /// <summary>
    /// A stack's identity. Global, not per container: an entry keeps its id when it moves between
    /// containers, so ops name entries and never cells. 0 is reserved for "no entry" in the occupancy grid.
    /// </summary>
    public readonly record struct EntryId(uint Value)
    {
        public static readonly EntryId None = new EntryId(0);
        public bool IsNone => Value == 0;
    }

    /// <summary>Per-container sequence number. Bumps once per accepted op that touched the container.</summary>
    public readonly record struct ContainerVersion(uint Value)
    {
        public ContainerVersion Next => new ContainerVersion(Value + 1);
    }
}

namespace PerformativeMail.Sim.Inventory
{
    // ========================================================================================
    // file: src/Sim/Inventory/Geometry.cs
    // ========================================================================================

    /// <summary>Unrotated item size in cells. Rotation is a property of the placement, not the item.</summary>
    public readonly record struct Footprint(byte W, byte H)
    {
        public Footprint Rotated => new Footprint(H, W);
        public bool IsSquare => W == H;
        public int Area => W * H;
    }

    public readonly record struct Cell(byte X, byte Y);

    /// <summary>A covered rectangle, derived from placement + footprint. Never stored on an entry.</summary>
    public readonly record struct CellRect(byte X, byte Y, byte W, byte H)
    {
        public IEnumerable<Cell> Cells() => throw new NotImplementedException();
        public bool Overlaps(CellRect other) => throw new NotImplementedException();
    }

    /// <summary>
    /// Origin cell plus a rotation flag. Rotated is normalized to false for square footprints so
    /// equal layouts hash equal; construct via For() so that invariant holds by construction.
    /// </summary>
    public readonly record struct Placement(byte X, byte Y, bool Rotated)
    {
        public static readonly Placement Origin = new Placement(0, 0, false);

        public static Placement For(Footprint footprint, byte x, byte y, bool rotated)
            => new Placement(x, y, rotated && !footprint.IsSquare);
    }

    /// <summary>
    /// Grid(cols, rows), or Slot: a 1x1 grid whose single entry may have any footprint (the cursor).
    /// "The cursor holds at most one entry" falls out of having one cell.
    /// </summary>
    public readonly struct ContainerShape
    {
        public readonly byte Cols;
        public readonly byte Rows;
        public readonly bool IgnoresFootprint;

        private ContainerShape(byte cols, byte rows, bool ignoresFootprint)
        {
            Cols = cols;
            Rows = rows;
            IgnoresFootprint = ignoresFootprint;
        }

        public static ContainerShape Grid(byte cols, byte rows) => new ContainerShape(cols, rows, ignoresFootprint: false);
        public static readonly ContainerShape Slot = new ContainerShape(1, 1, ignoresFootprint: true);

        public int CellCount => Cols * Rows;

        /// <summary>Cells covered by footprint at placement; false if any cell falls outside.
        /// Slot: (0,0,1,1) when at == Origin, regardless of footprint.</summary>
        public bool TryRect(Placement at, Footprint footprint, out CellRect rect) => throw new NotImplementedException();
    }

    // ========================================================================================
    // file: src/Sim/Inventory/Stack.cs
    // ========================================================================================

    public enum StackCategory : byte { Mail, Tool, Material, Consumable, Ammo, Blueprint, Weapon }

    /// <summary>Chapter 03 §3.2 speed points. Bulk (Cargo) never enters a player container.</summary>
    public enum WeightClass : byte { Light = 1, Medium = 3, Heavy = 8, Bulk = 255 }

    /// <summary>
    /// Merge-compatibility key. Mail merges on (kind, address); items on def. Built only through the
    /// factories, so an item key can never carry an address.
    /// </summary>
    public readonly record struct StackKey
    {
        public readonly bool IsMail;
        public readonly ushort Def;      // MailKindId or ItemDefId
        public readonly uint Address;    // AddressId.Packed; 0 for items

        private StackKey(bool isMail, ushort def, uint address)
        {
            IsMail = isMail;
            Def = def;
            Address = address;
        }

        public static StackKey Mail(MailKindId kind, AddressId address) => new StackKey(true, kind.Value, address.Packed);
        public static StackKey Item(ItemDefId item) => new StackKey(false, item.Value, 0);
    }

    /// <summary>
    /// Contents of one entry. Count >= 1 always: an emptied stack becomes a Remove change, never a Stack.
    /// The inventory owns mail *ids* only; MailItem data (value, deadline, flags) stays in Mail/.
    /// </summary>
    public abstract record Stack
    {
        public abstract StackKey Key { get; }
        public abstract int Count { get; }

        /// <summary>Split off n items (1 <= n <= Count). rest is null when n == Count.</summary>
        public abstract Stack Take(int n, out Stack? rest);

        /// <summary>Same Key required (callers check). Result Count == Count + other.Count.</summary>
        public abstract Stack Merge(Stack other);
    }

    public sealed record MailStack : Stack
    {
        public MailKindId Kind { get; }
        public AddressId Address { get; }
        /// <summary>Insertion order. Take removes from the end so the stack's base stays put.</summary>
        public IReadOnlyList<MailId> Ids { get; }

        public MailStack(MailKindId kind, AddressId address, IReadOnlyList<MailId> ids)
        {
            // TODO: ids.Count == 0 -> ArgumentException (the one runtime guard; construction is the boundary)
            Kind = kind;
            Address = address;
            Ids = ids;
        }

        public static MailStack Single(MailKindId kind, AddressId address, MailId id)
            => new MailStack(kind, address, new[] { id });

        public override StackKey Key => StackKey.Mail(Kind, Address);
        public override int Count => Ids.Count;
        public override Stack Take(int n, out Stack? rest) => throw new NotImplementedException();
        public override Stack Merge(Stack other) => throw new NotImplementedException();
    }

    public sealed record ItemStack : Stack
    {
        public ItemDefId Item { get; }
        public override int Count { get; }

        public ItemStack(ItemDefId item, int count)
        {
            // TODO: count < 1 -> ArgumentException
            Item = item;
            Count = count;
        }

        public override StackKey Key => StackKey.Item(Item);
        public override Stack Take(int n, out Stack? rest) => throw new NotImplementedException();
        public override Stack Merge(Stack other) => throw new NotImplementedException();
    }

    /// <summary>Content facts the inventory needs per key. Implemented over loaded defs; hard-coded in tests.</summary>
    public interface IStackCatalog
    {
        Footprint FootprintOf(StackKey key);
        int MaxStackOf(StackKey key);
        WeightClass WeightOf(StackKey key);
        StackCategory CategoryOf(StackKey key);
    }

    /// <summary>
    /// What a container is (ContainerDef, chapter 08 §2.5). Travels inside Reset so a replica can
    /// construct the container from the delta alone. The static specs are M0 constants; M1 loads them.
    /// </summary>
    public sealed record ContainerSpec(ContainerShape Shape, IReadOnlyCollection<StackCategory>? AllowedCategories)
    {
        public bool Accepts(StackCategory category) => AllowedCategories is null || AllowedCategories.Contains(category);

        // Chapter 03 §3.1 lists rows x cols; Grid takes (cols, rows).
        public static ContainerSpec Chest => new ContainerSpec(ContainerShape.Grid(8, 4), null);
        public static ContainerSpec BaseInventory => new ContainerSpec(ContainerShape.Grid(8, 2), null);
        public static ContainerSpec Backpack => new ContainerSpec(ContainerShape.Grid(8, 2), null);
        /// <summary>1x7: hotbar slot 1 (hands) is a Client rendering concern, not a cell.</summary>
        public static ContainerSpec Hotbar => new ContainerSpec(ContainerShape.Grid(7, 1), null);
        public static ContainerSpec Cursor => new ContainerSpec(ContainerShape.Slot, null);
        public static ContainerSpec Intake => new ContainerSpec(ContainerShape.Grid(20, 16), new[] { StackCategory.Mail });
        public static ContainerSpec Depot => new ContainerSpec(ContainerShape.Grid(20, 16), null);
    }

    // ========================================================================================
    // file: src/Sim/Inventory/Change.cs
    // ========================================================================================

    public readonly record struct Entry(EntryId Id, Stack Stack, Placement At);

    /// <summary>
    /// The only mutation language. The server plans ops into Changes and applies them; clients apply
    /// the same Changes through the same reducer. Three primitives cover every op.
    /// </summary>
    public abstract record Change;

    /// <summary>Add, or replace by Entry.Id (old cells vacated first). Covers place, move, split, restack.</summary>
    public sealed record Upsert(Entry Entry) : Change;

    public sealed record Remove(EntryId Id) : Change;

    /// <summary>Replace everything. Sort results, open (full state), join state, resync.</summary>
    public sealed record Reset(ContainerSpec Spec, IReadOnlyList<Entry> Entries) : Change;

    /// <summary>One container's mutation at one version. Hash is the content hash after applying.</summary>
    public sealed record ContainerDelta(ContainerId Container, ContainerVersion Version, ulong Hash, IReadOnlyList<Change> Changes);

    // ========================================================================================
    // file: src/Sim/Inventory/GridContainer.cs
    // ========================================================================================

    /// <summary>
    /// Entries are the truth; the occupancy array is a private projection for O(1) cell queries.
    /// Mutation only via Apply(Change) (internal). Hash is an order-independent XOR fold of per-entry
    /// hashes, updated incrementally, so server and replica agree without sorting.
    ///
    /// Access patterns traced: EntryAt = one array read; a placement check = one read per covered
    /// cell (<= 40); first-fit and merges-first = one row-major scan of the same array; full state,
    /// hash, sort, weight = enumerate entries (<= 320). No later index.
    /// </summary>
    public sealed class GridContainer
    {
        private readonly IStackCatalog _catalog;
        private readonly Dictionary<EntryId, Entry> _entries = new Dictionary<EntryId, Entry>();
        private readonly uint[] _cells;   // row-major EntryId.Value; 0 = empty

        public ContainerId Id { get; }
        public ContainerSpec Spec { get; }
        public ContainerVersion Version { get; private set; }
        public ulong Hash { get; private set; }
        public IReadOnlyCollection<Entry> Entries => _entries.Values;

        internal GridContainer(ContainerId id, ContainerSpec spec, IStackCatalog catalog)
        {
            Id = id;
            Spec = spec;
            _catalog = catalog;
            _cells = new uint[spec.Shape.CellCount];
        }

        // ---- queries (public: planning, other Sim systems, UI hit-testing on the replica) ----

        public bool TryGetEntry(EntryId id, out Entry entry) => _entries.TryGetValue(id, out entry);

        /// <summary>EntryId.None when empty. Out-of-range cells are the caller's bug (trust internal types).</summary>
        public EntryId EntryAt(Cell cell) => throw new NotImplementedException();

        /// <summary>Catalog footprint, rotated per the entry's placement.</summary>
        public Footprint FootprintOf(in Entry entry) => throw new NotImplementedException();

        public CellRect RectOf(in Entry entry) => throw new NotImplementedException();

        /// <summary>Chapter 03 §3.2: sum over entries of Count x WeightClass points. Players turn it into speed.</summary>
        public int WeightPoints => throw new NotImplementedException();

        /// <summary>Rebuilds the occupancy projection from entries and compares, checks bounds and
        /// overlap, recomputes the hash from scratch. Null when consistent. Test hook, cheap enough for the fuzz.</summary>
        public string? CheckInvariants() => throw new NotImplementedException();

        // ---- planning helpers (internal: used by InventoryOp.TryPlan) ----

        /// <summary>Rect for a stack key placed at 'at' in this shape. False = OutOfBounds.</summary>
        internal bool TryRect(Placement at, StackKey key, out CellRect rect) => throw new NotImplementedException();

        /// <summary>Distinct entries covering any cell of rect, excluding 'ignore' (the moving entry itself).</summary>
        internal IReadOnlyList<EntryId> Blockers(CellRect rect, EntryId ignore) => throw new NotImplementedException();

        /// <summary>
        /// Chapter 03 §3.2 auto-place. Returns the Changes for this container; leftover is what did not fit.
        /// </summary>
        internal IReadOnlyList<Change> PlanFit(Stack incoming, bool allowPartial, Func<EntryId> allocate, out Stack? leftover)
        {
            // TODO:
            //   remaining = incoming; changes = []
            //   pass 1 (merges): scan _cells row-major; for each entry id first seen with
            //       Key == remaining.Key and room = MaxStackOf(key) - Count > 0:
            //       k = min(room, remaining.Count); taken = remaining.Take(k, out rest)
            //       changes += Upsert(t with Stack = t.Stack.Merge(taken)); remaining = rest; stop when null
            //   pass 2 (unrotated): for each origin row-major: TryRect(For(fp, x, y, false)) && Blockers(rect, None).Count == 0
            //       -> changes += Upsert(new Entry(allocate(), remaining, at)); remaining = null; break
            //   pass 3 (rotated): same with rotated = true; skipped for square footprints
            //   remaining != null && !allowPartial -> leftover = incoming; return []   (nothing planned)
            //   leftover = remaining; return changes
            throw new NotImplementedException();
        }

        /// <summary>
        /// Chapter 03 §3.2 Sort: merge compatible stacks (keeping the lowest EntryId of each group),
        /// order by (group key, area desc), pack first-fit-decreasing onto an empty shape trying
        /// unrotated then rotated. False when FFD cannot place every stack (bin packing is not monotone).
        /// </summary>
        internal bool TryRepack(SortKey key, out IReadOnlyList<Entry> packed) => throw new NotImplementedException();

        // ---- reducer (internal: Commit on the server, ApplyDelta on a replica) ----

        /// <summary>
        /// Applies one change. Returns false and leaves state untouched when the change conflicts with
        /// current occupancy. Never false on the server (plans are pre-validated; Commit throws if it is).
        /// On a replica, false is a resync signal.
        /// </summary>
        internal bool Apply(Change change)
        {
            // TODO:
            //   Upsert u: rect = RectOf(u.Entry) (TryRect false -> return false)
            //             every cell in rect must be 0 or == u.Entry.Id.Value else return false
            //             if _entries has id: Hash ^= HashEntry(old); vacate old rect
            //             occupy rect; _entries[id] = u.Entry; Hash ^= HashEntry(u.Entry)
            //   Remove r: unknown id -> return false; vacate; Hash ^= HashEntry(old); _entries.Remove
            //   Reset s:  clear cells, entries, Hash; foreach entry: Apply(Upsert(e)) must be true
            //             (a false here means a corrupt snapshot: return false, replica resyncs)
            //   default:  throw new NotSupportedException(change.GetType().Name)   // C# cannot close the hierarchy
            throw new NotImplementedException();
        }

        internal void Bump() => Version = Version.Next;

        internal void SetVersion(ContainerVersion version) => Version = version;

        internal GridContainer Clone() => throw new NotImplementedException();

        /// <summary>splitmix64 over (id, key, count, mail ids, x, y, rotated). Ids are unique so the XOR fold never cancels.</summary>
        private static ulong HashEntry(in Entry entry) => throw new NotImplementedException();
    }

    // ========================================================================================
    // file: src/Sim/Inventory/InventoryOp.cs
    // ========================================================================================

    public enum SortKey : byte { ByAddress, BySize }

    public enum RejectReason : byte
    {
        UnknownContainer,
        UnknownEntry,
        NotOpen,          // player actor, container not open to them
        Forbidden,        // player actor attempting Deposit
        WrongCategory,    // ContainerSpec.AllowedCategories rejects the stack
        OutOfBounds,
        Occupied,         // target cells hold something that is not one compatible stack
        StackFull,        // compatible stack under the target has no room
        BadCount,         // Count < 1 or > source count
        NoRoom,           // QuickMove/Deposit found no placement
        CannotRepack,     // Sort's FFD could not place every stack; nothing changed
    }

    /// <summary>Who is asking. Players are authorized against the viewing registry; System (mail spawn, destinations) is not.</summary>
    public readonly record struct Actor
    {
        public EntityId? PlayerId { get; }
        public bool IsSystem => PlayerId is null;

        private Actor(EntityId? playerId) => PlayerId = playerId;

        public static Actor Player(EntityId id) => new Actor(id);
        public static readonly Actor System = new Actor(null);
    }

    /// <summary>
    /// A request to change container contents. Each op plans itself into Changes; an op without
    /// planning logic does not compile. Ops name entries, never cells, so a request built on a stale
    /// view still means "that stack" after other players have moved things.
    /// </summary>
    public abstract record InventoryOp
    {
        /// <summary>Containers a Player actor must have open.</summary>
        internal abstract IEnumerable<ContainerId> Touched { get; }

        /// <summary>Reads state only (may allocate EntryIds). False rejects with reason; nothing was mutated.</summary>
        internal abstract bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason);
    }

    /// <summary>
    /// move | split | merge on the wire. Count &lt; source count splits; one compatible stack under the
    /// target cells merges up to maxStack with the excess staying in the source; free cells place;
    /// anything else rejects Occupied. Same-container moves may overlap their own old cells.
    /// </summary>
    public sealed record Move(ContainerId From, EntryId Entry, ContainerId To, Placement At, int? Count = null) : InventoryOp
    {
        internal override IEnumerable<ContainerId> Touched => From.Equals(To) ? new[] { From } : new[] { From, To };

        internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
        {
            // TODO:
            //   src = inv[From] ?? UnknownContainer; dst = inv[To] ?? UnknownContainer
            //   e = src.Entry(Entry) ?? UnknownEntry
            //   n = Count ?? e.Stack.Count; 1 <= n <= e.Stack.Count else BadCount
            //   dst.Spec.Accepts(catalog.CategoryOf(e.Stack.Key)) else WrongCategory
            //   moving = e.Stack.Take(n, out rest)                      // rest null when the whole entry moves
            //   at = dst.Spec.Shape.IgnoresFootprint ? Placement.Origin : Placement.For(fp, At.X, At.Y, At.Rotated)
            //   dst.TryRect(at, moving.Key, out rect) else OutOfBounds
            //   ignore = (From == To && rest is null) ? e.Id : EntryId.None   // whole entry may overlap itself
            //   blockers = dst.Blockers(rect, ignore)
            //   0 blockers:
            //       rest is null && From == To && rect == src.RectOf(e): zero steps   (idempotent no-op)
            //       rest is null: [(From, Remove e.Id) unless From == To] + [(To, Upsert(e.Id, moving, at))]
            //       else:         [(From, Upsert(e.Id, rest, e.At)), (To, Upsert(inv.AllocateEntryId(), moving, at))]
            //   1 blocker t, t.Key == moving.Key, t.Id != e.Id:
            //       room = MaxStackOf(key) - t.Count; room == 0 -> StackFull
            //       k = min(room, n); merged = t.Stack.Merge(moving.Take(k, out excess))
            //       leftover = rest + excess (Merge when both non-null)
            //       [(To, Upsert(t with merged)), (From, leftover is null ? Remove e.Id : Upsert(e with leftover))]
            //   otherwise: Occupied
            throw new NotImplementedException();
        }
    }

    /// <summary>quick_move: first-fit into To. Merges first (row-major), then unrotated, then rotated. Moves what fits; NoRoom if nothing did.</summary>
    public sealed record QuickMove(ContainerId From, EntryId Entry, ContainerId To, int? Count = null) : InventoryOp
    {
        internal override IEnumerable<ContainerId> Touched => From.Equals(To) ? new[] { From } : new[] { From, To };

        internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
        {
            // TODO: From == To -> Plan.Empty (the UI always targets the other panel; same-container is an idempotent no-op)
            //   resolve src/dst/e/n as in Move; WrongCategory check;
            //   changes = dst.PlanFit(moving, allowPartial: true, inv.AllocateEntryId, out leftover)
            //   changes empty -> NoRoom
            //   source step: (rest + leftover) is null ? Remove e.Id : Upsert(e with rest + leftover)
            throw new NotImplementedException();
        }
    }

    /// <summary>Repack with first-fit-decreasing grouped by key; compatible stacks merge. Emits one Reset.</summary>
    public sealed record Sort(ContainerId Container, SortKey Key) : InventoryOp
    {
        internal override IEnumerable<ContainerId> Touched => new[] { Container };

        internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
        {
            // TODO: c = inv[Container] ?? UnknownContainer
            //   c.TryRepack(Key, out packed) ? [(Container, Reset(c.Spec, packed))] : CannotRepack
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Items leave the inventory system: drop (SimWorld spawns a WorldItem) and, in U3, deliver
    /// (Destinations.TryDeliver after acceptance rules). The stack comes back in Accepted.Withdrawn.
    /// </summary>
    public sealed record Withdraw(ContainerId From, EntryId Entry, int? Count = null) : InventoryOp
    {
        internal override IEnumerable<ContainerId> Touched => new[] { From };

        internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
        {
            // TODO: resolve src/e/n; taken = e.Stack.Take(n, out rest)
            //   steps = [(From, rest is null ? Remove e.Id : Upsert(e with rest))]; Withdrawn = taken
            throw new NotImplementedException();
        }
    }

    /// <summary>Items enter from outside (mail spawn, belt endpoint later). All-or-nothing. System actor only.</summary>
    public sealed record Deposit(ContainerId To, Stack Stack) : InventoryOp
    {
        internal override IEnumerable<ContainerId> Touched => new[] { To };

        internal override bool TryPlan(InventorySystem inv, out Plan plan, out RejectReason reason)
        {
            // TODO: dst = inv[To] ?? UnknownContainer; WrongCategory check
            //   changes = dst.PlanFit(Stack, allowPartial: false, inv.AllocateEntryId, out leftover)
            //   leftover != null -> NoRoom
            throw new NotImplementedException();
        }
    }

    /// <summary>Validated changes, in order. Only TryPlan constructs one; Commit cannot fail on it.</summary>
    public sealed class Plan
    {
        internal IReadOnlyList<(ContainerId Container, Change Change)> Steps { get; }
        internal Stack? Withdrawn { get; }

        internal Plan(IReadOnlyList<(ContainerId Container, Change Change)> steps, Stack? withdrawn = null)
        {
            Steps = steps;
            Withdrawn = withdrawn;
        }

        internal static readonly Plan Empty = new Plan(Array.Empty<(ContainerId, Change)>());
    }

    public abstract record InventoryOpResult;

    /// <summary>One delta per touched container, each at its new version. Withdrawn is non-null only for Withdraw.</summary>
    public sealed record Accepted(IReadOnlyList<ContainerDelta> Deltas, Stack? Withdrawn = null) : InventoryOpResult;

    public sealed record Rejected(RejectReason Reason) : InventoryOpResult;

    public enum ReplicaResult : byte
    {
        Applied,
        VersionGap,     // delta.Version != Version.Next and not a Reset: request a Snapshot
        Conflict,       // a Change did not fit current occupancy: request a Snapshot
        HashMismatch,   // applied, but content differs from the server's: request a Snapshot
    }

    /// <summary>What SimWorld queues for ServerRuntime to route (U4 shapes SimEvent; this is the inventory payload).</summary>
    public sealed record InventoryResultEvent(EntityId Sender, uint ReqId, InventoryOpResult Result);

    // ========================================================================================
    // file: src/Sim/Inventory/InventorySystem.cs
    // ========================================================================================

    /// <summary>
    /// Aggregate root for every container in the sim. Single writer: the tick thread. Requests apply
    /// in arrival order with no locks; conflicts are semantic (cell now occupied, entry gone), never
    /// version-based, so two players can work one chest without tripping over each other.
    /// The same class is the client's replica (ApplyDelta) and its prediction shadow (Fork + Apply).
    /// </summary>
    public sealed class InventorySystem
    {
        private readonly IStackCatalog _catalog;
        private readonly Dictionary<ContainerId, GridContainer> _containers = new Dictionary<ContainerId, GridContainer>();
        /// <summary>Player-owned containers (hotbar, inventory, backpack, cursor): always open to their owner.</summary>
        private readonly Dictionary<ContainerId, EntityId> _owners = new Dictionary<ContainerId, EntityId>();
        /// <summary>At most one external container per player (chapter 03 §3.3) is a property of this type: one slot.</summary>
        private readonly Dictionary<EntityId, ContainerId> _external = new Dictionary<EntityId, ContainerId>();
        private uint _nextContainer = 1;
        private uint _nextEntry = 1;

        public InventorySystem(IStackCatalog catalog) => _catalog = catalog;

        public IStackCatalog Catalog => _catalog;
        public IEnumerable<GridContainer> Containers => _containers.Values;
        public GridContainer this[ContainerId id] => _containers[id];
        public bool TryGetContainer(ContainerId id, out GridContainer container) => _containers.TryGetValue(id, out container);

        // ---- authoring (server) ----

        public ContainerId CreateContainer(ContainerSpec spec, EntityId? owner = null)
        {
            var id = new ContainerId(_nextContainer++);
            // TODO: _containers[id] = new GridContainer(id, spec, _catalog); if owner is { } o: _owners[id] = o
            throw new NotImplementedException();
        }

        /// <summary>Closes all viewers and returns the contents for the caller to spill (M1: chest destroyed).</summary>
        public IReadOnlyList<Stack> DestroyContainer(ContainerId id) => throw new NotImplementedException();

        // ---- the request path (server) ----

        /// <summary>Authorize -> op.TryPlan -> Commit. Never partially applies: Commit only sees a Plan.</summary>
        public InventoryOpResult Apply(Actor actor, InventoryOp op)
        {
            if (!Authorize(actor, op, out var why)) return new Rejected(why);
            if (!op.TryPlan(this, out var plan, out why)) return new Rejected(why);
            return Commit(plan);
        }

        /// <summary>
        /// Range (2.5 m) is the caller's check; Inventory does not know positions. Replaces the player's
        /// previous external container. Idempotent: opening what is already open returns the same state.
        /// Returns Accepted with one Reset delta at the current version.
        /// </summary>
        public InventoryOpResult Open(EntityId player, ContainerId container) => throw new NotImplementedException();

        /// <summary>No-op when the container is not the player's external one.</summary>
        public void Close(EntityId player, ContainerId container) => throw new NotImplementedException();

        /// <summary>Owner (if any) plus players whose external container this is. Eight players: the scan is the index.</summary>
        public IEnumerable<EntityId> ViewersOf(ContainerId container) => throw new NotImplementedException();

        /// <summary>Reset at the current version and hash. Open, join state, resync.</summary>
        public ContainerDelta Snapshot(ContainerId container) => throw new NotImplementedException();

        // ---- the replica path (client, and the fuzz's shadow) ----

        /// <summary>
        /// Unknown container + Reset: adopt it (construct from the spec). Known: Reset resyncs; otherwise
        /// require Version.Next, apply every change, then compare the hash.
        /// </summary>
        public ReplicaResult ApplyDelta(ContainerDelta delta)
        {
            // TODO:
            //   if !_containers has delta.Container:
            //       delta.Changes[0] is Reset r ? create GridContainer(delta.Container, r.Spec) : return VersionGap
            //   c = _containers[delta.Container]
            //   if delta.Changes[0] is not Reset && delta.Version != c.Version.Next -> VersionGap
            //   foreach change: if !c.Apply(change) -> Conflict
            //   c.SetVersion(delta.Version)
            //   c.Hash != delta.Hash -> HashMismatch
            //   Applied
            throw new NotImplementedException();
        }

        /// <summary>Deep copy: prediction shadow (client) or stale view (tests). Containers, owners, external slots, allocators.</summary>
        public InventorySystem Fork() => throw new NotImplementedException();

        /// <summary>First failing container's report, or null. Fuzz calls it after every op.</summary>
        public string? CheckInvariants() => throw new NotImplementedException();

        // ---- internals ----

        internal EntryId AllocateEntryId() => new EntryId(_nextEntry++);

        internal bool IsOpen(EntityId player, ContainerId container)
            => (_owners.TryGetValue(container, out var owner) && owner.Equals(player))
            || (_external.TryGetValue(player, out var open) && open.Equals(container));

        private bool Authorize(Actor actor, InventoryOp op, out RejectReason reason)
        {
            // TODO:
            //   actor.IsSystem -> true
            //   op is Deposit -> Forbidden
            //   foreach c in op.Touched: !_containers.ContainsKey(c) -> UnknownContainer; !IsOpen(player, c) -> NotOpen
            throw new NotImplementedException();
        }

        private Accepted Commit(Plan plan)
        {
            // TODO:
            //   group plan.Steps by container preserving first-touch order
            //   foreach group: foreach change: if !c.Apply(change) throw new InvalidOperationException("plan violated occupancy")  // a bug, never a runtime condition
            //                  c.Bump(); deltas += new ContainerDelta(c.Id, c.Version, c.Hash, changes)
            //   zero steps -> Accepted(empty deltas): no bump, idempotent no-op
            //   return new Accepted(deltas, plan.Withdrawn)
            throw new NotImplementedException();
        }
    }
}

// ============================================================================================
// file: src/Sim/Net/InventoryCodec.cs   (U2 defines the seam; U4 fills the BitWriter bodies)
// Wire <-> domain at the boundary. ReqId is correlation and lives here, never inside InventorySystem.
// ============================================================================================
namespace PerformativeMail.Sim.Net
{
    public abstract record Request(uint ReqId);

    /// <summary>move | split | merge | quick_move | sort | drop. The parser never yields Deposit.</summary>
    public sealed record InventoryRequest(uint ReqId, InventoryOp Op) : Request(ReqId);

    public sealed record OpenContainerRequest(uint ReqId, ContainerId Container) : Request(ReqId);

    public sealed record CloseContainerRequest(uint ReqId, ContainerId Container) : Request(ReqId);

    public static class InventoryCodec
    {
        public static bool TryParseRequest(ReadOnlySpan<byte> payload, out Request request) => throw new NotImplementedException();

        /// <summary>Chapter 08 §3.4 event: { container, version, hash, apply[], reqId?, ok: true }.</summary>
        public static byte[] EncodeEvent(ContainerDelta delta, uint? reqId) => throw new NotImplementedException();

        public static byte[] EncodeRejected(uint reqId, RejectReason reason) => throw new NotImplementedException();

        public static bool TryParseEvent(ReadOnlySpan<byte> payload, out ContainerDelta delta, out uint? reqId) => throw new NotImplementedException();
    }
}

// ============================================================================================
// file: src/Sim/SimWorld.cs   (proposed change to the U0 scaffold: sender comes from the connection)
// ============================================================================================
namespace PerformativeMail.Sim
{
    public sealed class SimWorld
    {
        private readonly List<InventoryResultEvent> _pendingInventoryEvents = new List<InventoryResultEvent>();

        public uint CurrentTick { get; private set; }
        public InventorySystem Inventory { get; }

        public SimWorld(IStackCatalog catalog) => Inventory = new InventorySystem(catalog);

        public void Tick(uint tick) => CurrentTick = tick;

        public void ApplyInput(EntityId sender, uint tick, ReadOnlySpan<byte> payload) => throw new NotImplementedException();

        public void ApplyRequest(EntityId sender, ReadOnlySpan<byte> payload)
        {
            if (!InventoryCodec.TryParseRequest(payload, out var request)) return;
            switch (request)
            {
                case InventoryRequest r:
                    var result = Inventory.Apply(Actor.Player(sender), r.Op);
                    // TODO(U6): if (result is Accepted { Withdrawn: { } dropped }) World.SpawnItem(sender, dropped);
                    _pendingInventoryEvents.Add(new InventoryResultEvent(sender, r.ReqId, result));
                    break;
                case OpenContainerRequest o:
                    // TODO(U6): range <= 2.5 m via Players before opening.
                    _pendingInventoryEvents.Add(new InventoryResultEvent(sender, o.ReqId, Inventory.Open(sender, o.Container)));
                    break;
                case CloseContainerRequest c:
                    Inventory.Close(sender, c.Container);
                    break;
            }
        }

        /// <summary>Drained by ServerRuntime at Events.Flush; deltas fan out via Inventory.ViewersOf.</summary>
        public IReadOnlyList<InventoryResultEvent> DrainInventoryEvents() => throw new NotImplementedException();
    }
}

// ============================================================================================
// file: tests/Sim.Tests/Inventory/Fuzz/*.cs   (the lever for M0 criterion 3, and criterion 1 in-process)
// ============================================================================================
namespace PerformativeMail.Sim.Tests.Inventory
{
    /// <summary>Hard-coded chapter 03 §1.1 sizes and stack limits. No content loading in U1/U2.</summary>
    public sealed class TestCatalog : IStackCatalog
    {
        public static readonly TestCatalog Default = new TestCatalog();
        public Footprint FootprintOf(StackKey key) => throw new NotImplementedException();
        public int MaxStackOf(StackKey key) => throw new NotImplementedException();
        public WeightClass WeightOf(StackKey key) => throw new NotImplementedException();
        public StackCategory CategoryOf(StackKey key) => throw new NotImplementedException();
    }

    public static class Seed
    {
        /// <summary>Deposits `count` letters spread over `addresses` addresses as Actor.System.</summary>
        public static void Letters(InventorySystem inv, ContainerId into, int addresses, int count) => throw new NotImplementedException();
        public static void Packages(InventorySystem inv, params ContainerId[] into) => throw new NotImplementedException();
    }

    /// <summary>Multiset of every MailId plus per-item counts across all containers. Equal before and after every op.</summary>
    public sealed record MailLedger(IReadOnlyDictionary<MailId, int> Mail, IReadOnlyDictionary<ItemDefId, int> Items)
    {
        public static MailLedger Of(InventorySystem inv) => throw new NotImplementedException();
        // TODO: value equality over dictionary contents (records compare references for dictionaries)
    }

    public static class OpGen
    {
        /// <summary>
        /// Builds a random Move / QuickMove / Sort / Withdraw-free op from a possibly stale view: picks
        /// an entry the view believes exists in `mine` or `shared`, a random target cell and rotation,
        /// sometimes a partial count. Stale entries and occupied cells are the point: they must reject
        /// cleanly, never corrupt.
        /// </summary>
        public static InventoryOp Random(Random rng, InventorySystem view, EntityId actor, ContainerId mine, ContainerId shared)
            => throw new NotImplementedException();
    }
}

// ============================================================================================
// file: src/Sim/Core/IsExternalInit.cs   (netstandard2.1 lacks it; records and init need it)
// ============================================================================================
#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
