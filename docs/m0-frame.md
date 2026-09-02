# M0 foundation frame

## Definition of done

Every M0 acceptance criterion in `spec/12-milestones.md` is falsifiable and must pass on the real artifact:

1. 8 clients (2 real, 6 bots) on a listen server for 10 min with no inventory desync (server and client container hashes match on every version).
2. Prediction error under 200 ms latency with 5% loss stays below 10 cm RMS for the local player; no visible rubber-banding at 100 ms.
3. Two players moving items in the same chest concurrently never duplicate or lose an item across 10 000 randomized ops (fuzz test).
4. Delivery and misdelivery pay or deduct exactly per chapter 03 §2.2 in unit tests.
5. Server tick ≤ 2 ms with 8 players on the test map.

## Scope

Touch chapters 03 (inventory, destinations), 06 (netcode core), 07 (architecture skeleton). Build Sim with no Godot refs first. Wire Server, Client, App, ContentValidator, BotClient, and xUnit projects around it. Ship a fixed hand-authored map (one street, 10 houses, PO with Intake), mail spawn, wallet, misdelivery, minimal HUD, and a headless bot that walks and delivers.

## Rigor

High. Netcode and concurrent inventory are one-way doors. Gates are executable tests and measured tick/latency numbers, not "it compiles".

## Blockers found while grounding

| Blocker | Impact | Mitigation |
| --- | --- | --- |
| Snapshot had no .NET SDK | Cannot build Sim | Installed .NET 8.0.424 into `~/.dotnet` for this session |
| No Godot binary | Cannot verify HUD or Godot transports yet | Sequence Sim + loopback harness before Godot glue; install Godot .NET when U8 starts |
| Personal environment has no committed `environment.json` | Next agents may lack .NET | Propose an install step that installs the SDK after first green Sim tests |

## Workflow (Phase B draft)

Riskiest unknowns after scaffold: concurrent inventory and prediction under loss.

| Unit | Landable change | Verify |
| --- | --- | --- |
| U0 | Solution layout, empty Sim/Server/Client/App/tools/tests | `dotnet test` green; ContentValidator exits 0 on empty content dir |
| U1 | Core ids, tick, MailItem, GridContainer placement/rotate/stack | Unit tests for place/rotate/stack/quick-move |
| U2 | InventoryOp request/validate/apply + versioning | Concurrent fuzz 10 000 ops (M0 criterion 3) |
| U3 | Destinations.TryDeliver + wallet §2.2 | Unit tests for pay, half late, reject, misdelivery floor |
| U4 | Loopback transport, ServerRuntime/ClientRuntime 30 Hz | Integration: host + client exchange inputs/snapshots |
| U5 | Movement prediction and reconciliation | Latency/loss harness (M0 criterion 2) |
| U6 | Fixed test map + mail spawn timer + Intake | Map loads; mail appears; bot can see addresses |
| U7 | Headless BotClient walk-and-deliver | Bot delivers one letter; wallet increases |
| U8 | Godot project + minimal HUD | Boot smoke; timer/wallet/interact prompt visible |
| U9 | 8-client soak + tick budget | Criteria 1 and 5 |

Architect arena runs before U1/U2 and before U4/U5. U0 shape is already concrete in chapter 07, so arena is skipped for the scaffold.

## Playbook

`figure-it-out` owns the run. Each unit uses Feature discipline inside the loop (named data shape, delegated code, real-artifact verify, small commits). Decision trail: `.audit/m0-foundation.tsv`.
