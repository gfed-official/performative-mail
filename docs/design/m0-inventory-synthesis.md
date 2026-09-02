# M0 inventory synthesis

## Base

Candidate 1 from the arena. Entries are the truth. Cell occupancy is a private projection. Every `InventoryOp` plans into `Change`s. `Commit` applies them and stamps one version and hash per touched container. The client runs the same reducer via `ApplyDelta`. Conflicts are semantic (`Occupied`, `UnknownEntry`), not version CAS. The server is a single writer that applies requests in arrival order.

## Grafts

- `Amount.All` / `Amount.Of(n)` instead of nullable counts
- `PositiveCount` and private validated stack construction
- `BeforeVersion` plus after stamp on each delta
- Conservation assert over touched `MailId`s inside private commit (debug)
- Blocked-cell mask for hotbar hands slot (1x8 with cell 0 blocked)
- `InventoryAudit` report for the 10k fuzz oracle

## Rejections

- Strict expected-version CAS (candidate 2)
- Public `TryPlace` / `Validate`+`Apply` pairs that callers can misuse (candidates 3 and 4)
- Journal as source of truth for M0
- Separate unreplicated `MailContents`

## U1 then U2

1. U1: branded ids, geometry, Stack, GridContainer reducer, place/rotate/stack/quick-move unit tests
2. U2: InventorySystem.Apply, Open/Close, concurrent 10k fuzz

Sketch: `docs/design/m0-inventory-types.sketch.cs`
Cross-judge: `docs/design/m0-inventory-cross-judge.md`
