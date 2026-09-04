# Play report JSON

`--report=` writes one compact JSON object. `SmokeReport.Render` owns the keys. `game/Main.cs` only passes overlay and debug open flags.

`state` selects the object shape. Menu and Connecting emit `state` only. Failed emits `state` and `error`. Playing emits the keys below.

## Keys

| Key | States | Type | Unit or spelling |
| --- | --- | --- | --- |
| `state` | all | string | `Menu`, `Connecting`, `Playing`, or `Failed` |
| `local` | Playing | number | Local `EntityId.Value` |
| `worldHash` | Playing | string | `0x` plus 16 uppercase hex digits |
| `phase` | Playing | string | `RunPhase.ToString()`, such as `Prep` |
| `shift` | Playing | number | Raw shift |
| `wallet` | Playing | number | Wallet cents |
| `quota` | Playing | number | Quota cents |
| `hudShift` | Playing | string | `HudFrame.ShiftLabel`, such as `Shift 1 / 5` |
| `hudPhase` | Playing | string | `HudFrame.PhaseLabel`, such as `PREP` |
| `hudTimer` | Playing | string | `HudFrame.TimerLabel`, as `MM:SS` |
| `pawns` | Playing | array | `{id, role, x, y}` |
| `pawns[].id` | Playing | number | `EntityId.Value` |
| `pawns[].role` | Playing | string | `PawnRole.ToString()`, `Local` or `Remote` |
| `pawns[].x` | Playing | number | Pose X, centimetres |
| `pawns[].y` | Playing | number | Pose Y, centimetres |
| `worldEntityCounts.postOffices` | Playing | number | `1` when `World` is present, else `0` |
| `worldEntityCounts.intakes` | Playing | number | `1` when `World` is present, else `0` |
| `worldEntityCounts.houses` | Playing | number | `WorldTables.Houses.Length`, else `0` |
| `worldEntityCounts.mailboxes` | Playing | number | Same as `houses` |
| `overlayOpen` | Playing | boolean | Inventory overlay `IsOpen` |
| `debugOpen` | Playing | boolean | Debug menu `IsOpen`. Missing debug control is `false` |
| `error` | Failed | string | `FailReason.Message()`, JSON-escaped |

There is no `pawnCount` and no `schemaVersion`. jq uses `.pawns | length`.

## Null world

When `PlaySession.Playing.World` is null, `worldHash` is `0x0000000000000000` and every `worldEntityCounts` field is `0`.

`--host --debug-world` writes the same Playing keys. `worldEntityCounts.houses` and `mailboxes` are `2`, and `worldHash` is `0x4CF184F2FA4D4EEE`.

`--debug-helper=` does not add keys. `intake` and `mailbox` change `pawns[].x` / `pawns[].y`. `overlay` sets `overlayOpen` to true. `give-mail` is visible in inventory, not in this object.

`--world-dump=` is a sidecar Label3D dump from `WorldStage.Dump`. It is not a SmokeReport key. `tools/godot/ci.sh worldstage` asserts `worldEntityCounts` here and the live labels in that dump.

## Example Playing object

```json
{"state":"Playing","local":1,"worldHash":"0x821670054873680E","phase":"Prep","shift":1,"wallet":1820,"quota":640,"hudShift":"Shift 1 / 5","hudPhase":"PREP","hudTimer":"00:00","pawns":[{"id":1,"role":"Local","x":1200,"y":3400}],"worldEntityCounts":{"postOffices":1,"intakes":1,"houses":50,"mailboxes":50},"overlayOpen":false,"debugOpen":false}
```

## Example jq

`tools/godot/ci.sh play` checks this expression after the compact greps:

```bash
jq -e '
  .state == "Playing"
  and .phase == "Prep"
  and .shift == 1
  and .worldHash == "0x821670054873680E"
  and (.pawns | length) >= 1
  and .worldEntityCounts.postOffices == 1
  and .worldEntityCounts.intakes == 1
  and .worldEntityCounts.houses == 50
  and .worldEntityCounts.mailboxes == 50
  and .overlayOpen == false
  and .debugOpen == false
' "$report"
```
