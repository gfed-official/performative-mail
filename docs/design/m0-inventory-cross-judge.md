# Inventory design cross-judge

## Score summary

| Candidate | C1 | C2 | C3 | C4 | C5 | C6 | Total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Candidate 1 | 2 | 2 | 2 | 2 | 1 | 2 | **11** |
| Candidate 2 | 2 | 2 | 2 | 2 | 1 | 1 | **10** |
| Candidate 3 | 1 | 0 | 1 | 1 | 0 | 1 | **4** |
| Candidate 4 | 2 | 0 | 2 | 1 | 1 | 1 | **7** |

Criterion 2 is a hard gate. A design that exposes a stale plan or raw cross-container mutators cannot recover by scoring well elsewhere.

## Candidate 1

| Criterion | Score | One-line note |
| --- | ---: | --- |
| 1 | 2 | `Placement`, `StackKey`, `MailStack`, and `Entry` encode rotation, footprint-derived placement, and kind-plus-address stacking. |
| 2 | 2 | `InventorySystem.Apply` owns authorization, planning, and private commit; callers cannot construct a `Plan` or invoke `Commit`. |
| 3 | 2 | Per-container versions, request-correlated rejections, next-version and hash checks, and `Reset` resync form a complete rollback contract. |
| 4 | 2 | The API states single-writer call order and sketches a seeded 10,000-op stale-view fuzz with an exact `MailId` ledger and replica hashes. |
| 5 | 1 | One aggregate hides the rules and leaks no wire or Godot type, but its public container, replica, fork, and test hooks are broader than needed. |
| 6 | 2 | It targets `netstandard2.1`, supplies the record polyfill, joins through `SimWorld`, and orders `GridContainer` before `InventorySystem` for U1 then U2. |

## Candidate 2

| Criterion | Score | One-line note |
| --- | ---: | --- |
| 1 | 2 | `PositiveCount`, `GridPlacement`, payload variants, and `MailStack(kind, address, ids)` encode the core inventory rules. |
| 2 | 2 | `Apply` builds every immutable next projection and publishes one root only after access, fit, version, and conservation checks pass. |
| 3 | 2 | Before and after versions, hashes, `VersionConflict`, current stamps, and duplicate-delta handling provide the clearest conflict response. |
| 4 | 2 | Monotonic `ArrivalSequence`, a Sim-thread guard, exact retry handling, conservation checks, and journal replay make the fuzz safety explicit. |
| 5 | 1 | The two main methods hide much, but callers still coordinate arrival sequences, nested expected versions, and journal-shaped decisions. |
| 6 | 1 | It is engine-free and compatible with the target, but strict version CAS changes the grounded request shape and rejects harmless stale arrival-order operations. |

## Candidate 3

| Criterion | Score | One-line note |
| --- | ---: | --- |
| 1 | 1 | Rotation and address-aware `ItemKey` exist, but mutable `Placement`, tagged family state, nullable ids, and zero-means-all leave invariants to checks. |
| 2 | 0 | Public `TryRemove`, `TryPlace`, `TryMergeInto`, and `BumpVersion` bypass `Submit` and recreate the forbidden half-apply path. |
| 3 | 1 | Versions and request receipts exist, but deltas lack a hash and before-version, and no replica apply or resync contract is defined. |
| 4 | 1 | Arrival call order and exact `MailId` census are shown, but the unversioned cursor, per-container entry ids, and bypass mutators weaken safety. |
| 5 | 0 | `GridContainer` exposes placement, merge, removal, sorting, version bumps, occupancy, and snapshots, so the complexity is the public interface. |
| 6 | 1 | It avoids Godot and follows U1 then U2, but its cursor is not the required versioned one-slot container and the `SimWorld` seam is only asserted. |

## Candidate 4

| Criterion | Score | One-line note |
| --- | ---: | --- |
| 1 | 2 | `Placement`, `Rotation`, `StackKey`, `Amount`, `SlotHint`, and the four op variants model most rules directly. |
| 2 | 0 | Both `Validate` and `Apply` are public, so a caller can retain a plan across another `Execute`; the plan carries no state stamp to reject that TOCTOU use. |
| 3 | 2 | Request rejection, per-container versions, event hashes, next-version checks, and `NeedsResync` make rollback and replica recovery clear. |
| 4 | 1 | Single-thread arrival order and `InventoryAudit` are explicit, but stale plans and the separate `MailContents` mutation stop safety following from the API. |
| 5 | 1 | `Execute` is deep, but public `Validate`, `Apply`, `ApplyBatch`, the mutable event log, and audit mechanics expose transaction machinery. |
| 6 | 1 | It targets U1 and U2 without Godot, but removing `MailId`s from replicated container state conflicts with the grounded full-state schema. |

## Recommended base

Use **candidate 1**.

It is the only candidate that combines the grounded semantic conflict rule, one caller-proof apply path, stable entry addressing, and one reducer for authoritative and replica state without making a journal or version CAS foundational. Candidate 2 is close, but its strict expected-version protocol rejects contention that the spec permits and leaks coordination into callers. Candidate 4 has a fatal public validate-then-apply split. Candidate 3 exposes the mutations that `Submit` is supposed to own.

Candidate 1 still needs two corrections before implementation. Model the hotbar as the specified 1x8 grid with a blocked hands cell, not a 1x7 client convention. Also reduce production visibility for `Catalog`, the indexer, container enumeration, and invariant test hooks.

## Grafts worth keeping

- From candidate 2, use `PositiveCount` and private validated stack construction instead of nullable counts and constructor TODOs.
- From candidate 2, put `BeforeVersion` and the complete after stamp on each delta, and return current stamps with a conflict rejection.
- From candidate 2, run exact touched-container `MailId` and item-count conservation as a debug assertion inside the private commit path.
- From candidate 3, keep a separate blocked-cell mask for the 1x8 hotbar. Do not encode the hands slot by deleting a column.
- From candidate 4, replace nullable move counts with `Amount.All` or `Amount.Of(n)`.
- From candidate 4, consider `SlotHint.Auto` or `SlotHint.At` so the Net boundary can normalize `quick_move` and `move` into one domain operation.
- From candidate 4, keep the structured `InventoryAudit` report as the reusable oracle for the 10,000-op fuzz.

## Rejections

- Drop candidate 2's authoritative journal, strict expected-version CAS, and caller-supplied `ArrivalSequence`. They add a second protocol and solve requirements not present in U1 or U2.
- Drop candidate 3's public grid mutators, public version bump, zero-as-`All`, per-container `EntryId` allocation, cursor sentinel, and fixed 320-cell architecture.
- Drop candidate 4's public `Validate` and `Apply`, separate server-only `MailContents`, high-water-only retry response, and two-effect optimization.
- Drop candidate 1's 1x7 hotbar and public test-only inspection hooks.
- Do not add locks. `ServerRuntime` establishes arrival order and `SimWorld` remains the single writer.
