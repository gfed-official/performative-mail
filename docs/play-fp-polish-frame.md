# Play first-person polish frame

## Definition of done

Falsifiable on the real artifacts:

1. Issues exist for first-person camera, visual identifiers, overlapping Host/Join text, lobby/host chrome hide, and Esc-only post-join actions.
2. In `PlaySession.Playing` and `Connecting`, the Host/Join/Leave/status chrome is not visible. It is visible only on `Menu` and `Failed`.
3. Leave after join is only reachable through the Esc pause menu (`PauseFrame.LeaveId` → confirm).
4. While Playing, the camera sits at eye height on the local pawn and yaw comes from mouse look into `MoveIntent.Yaw`. The follow offset `(0, 9, 8)` is gone.
5. The authored M0 atlas is staged with labeled Post Office, Intake (Mail), house addresses, and mailboxes.

## Scope

Touch `game/` (Main, InputSampler, PawnStage, new WorldStage) and Net.Tests contracts that read `Main.cs`. Do not wait on U11.5 `WorldTables` binding. Do not start M2.

## Rigor

Medium. Host has no Godot binary. Gates are source contracts, `dotnet test`, and CI Godot when it runs.

## Workflow

| Unit | Change | Verify |
| --- | --- | --- |
| 1 | GitHub issues 163–167 | `gh issue view` |
| 2 | Session chrome hide + Esc-only Leave | MainPlayBootTests / new chrome tests |
| 3 | First-person camera + mouse yaw | Source contract: no `(0, 9, 8)`; InputSampler yaw |
| 4 | WorldStage markers from m0 atlas | WorldStage dump names PO/mail/address |
| 5 | Tests green, PR | `dotnet test`; PR URL |

## Decision trail

`.audit/play-fp-polish.tsv`
