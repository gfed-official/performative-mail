# U6.1 split

## Why split it

U6.1 (issue #224) bundles four different concerns: a new vehicle kind, a belt/inserter
integration for a container that moves, a shop purchase gated by a blueprint the
codebase does not enforce anywhere yet, and a fuel meter. Each is independently
landable and independently testable. One PR per concern keeps the review small and
lets a broken piece fail in isolation instead of inside one large truck PR.

## Units

| Unit | Landable change | Verify | Depends on | Issue |
| --- | --- | --- | --- | ---: |
| U6.1.1 | Mail Truck vehicle kind, speed context, spawn, empty cargo container | Spawn yields the right body and an empty 8x10 mail-only container; road/off-road speed match chapter 10 | M1 `VehicleStep` | [#310](https://github.com/gfed-official/performative-mail/issues/310) |
| U6.1.2 | Parked loading face: Inserter and belt endpoints can reach the cargo container | Push succeeds when parked, rejects when moving | U6.1.1, U4.2 (#220) | [#313](https://github.com/gfed-official/performative-mail/issues/313) |
| U6.1.3 | Shop grant for `mail_truck` requires owning `bp_truck` | Buy rejected before the blueprint, succeeds and mounts after | U6.1.1 | [#312](https://github.com/gfed-official/performative-mail/issues/312) |
| U6.1.4 | Fuel: 1 Oil Can per 5 min driving, halts at empty | Consumes after 300 s driving; halts with no oil can aboard | U6.1.1 | [#311](https://github.com/gfed-official/performative-mail/issues/311) |

U6.1.2, U6.1.3, and U6.1.4 each branch from U6.1.1's tip and stay independent of each
other. None of them touch the same files as the other two.

## Playbook

`figure-it-out`/`multi-phase-plan` scoped the split. `how` grounded `VehicleTable`,
`VehicleBody`, `VehicleContext`, `Inserter`, `BeltEndpoints`, `ShopCatalog`,
`ShopSession`, and `PlaySessionMachine` before writing any code. The full plan,
including the sketches compared for the loading-face design, lives outside this repo
in the requesting Cursor Project's store at `docs/issue-224-plan.md`.
