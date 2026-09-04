# Godot scriptable testing frame

## Definition of done

Falsifiable on the real artifacts:

1. Parent issue `#175` and children `#176`–`#185` exist for harness, report schema, debug world, debug teleports, and first Godot smokes.
2. Godot CI stays on `tools/godot/ci.sh` inside `barichello/godot-ci:mono-4.7.2`. No GdUnit/GUT unless a later issue chooses it.
3. Sim and App logic stay in xUnit (`dotnet test`). Godot smokes prove bind, WorldStage/PawnStage, interact edges, and ENet presence.
4. A debug world boots from CLI with PO, Mail intake, and at least one mailbox at known positions.
5. `--report` (or successor JSON) exposes phase, shift, worldHash, wallet, pawn count, and world entity counts that bash/jq can assert.
6. At least one CI path asserts pickup→deliver inside a Godot process, not only loopback `LiveLoopTests`.

## Scope

Touch `tools/godot/`, `game/` (Main CLI, WorldStage, DebugMenu, reports), and Net.Tests contracts that read `game/`. Do not move `game/` into `PerformativeMail.sln`. Do not start M2.

## Issue order

| # | Title | Depends |
|---|---|---|
| 175 | Parent: Godot scriptable testing | #153 |
| 176 | Formalize headless harness | none |
| 177 | Shared JSON report schema | soft: #176 |
| 185 | Extract WorldStage/PawnStage pure helpers | none (parallel) |
| 178 | Debug world for scriptable tests | soft: #177 |
| 179 | Debug menu teleports and spawn helpers | soft: #178 |
| 180 | WorldStage live dump | #177 and/or #178 |
| 183 | HUD binds from Playing not Placeholder | soft: #177 |
| 181 | Interact pickup and deliver smoke | #178, #179 |
| 182 | Live inventory overlay dump | soft: #181 or #179 |
| 184 | Pause Leave returns to Menu | soft: #176, #177 |

## Rigor

Medium-high. Host may lack Godot. Gates are `dotnet test`, source contracts, and CI Godot smokes when the container runs.

## Decision trail

`.audit/godot-scriptable-test.tsv`
