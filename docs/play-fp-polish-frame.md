# Play first-person polish frame

## Definition of done

Falsifiable on the real artifacts:

1. Issues exist for first-person camera, visual identifiers, overlapping Host/Join text, lobby/host chrome hide, and Esc-only post-join actions (`#163`–`#167`).
2. In `PlaySession.Playing` and `Connecting`, Host/Join/status chrome is not visible. It is visible only on `Menu` and `Failed`.
3. Leave after join is only reachable through the Esc pause menu (`PauseFrame.LeaveId` → confirm). No `_leave` button in `Main.cs`.
4. While Playing, the camera sits at eye height on the local pawn and yaw comes from mouse look into `MoveIntent.Yaw`. The follow offset `(0, 9, 8)` is gone.
5. Live `WorldTables` staging labels Post Office, Mail intake, house addresses, and mailboxes.
6. `BindHud(playing.Hud)` and `_world.Sync(playing.World)` from `#162` remain.

## Scope

Touch `game/` (Main, InputSampler, PawnStage, WorldStage) and App/Net.Tests contracts. Do not start M2.

## Rigor

Medium. Host may lack Godot binary. Gates are source contracts, `dotnet test`, and CI Godot when it runs.

## Decision trail

`.audit/play-fp-polish.tsv`
