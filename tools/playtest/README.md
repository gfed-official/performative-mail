# Playtest harness

PM Playtest / routines entrypoint:

```bash
bash tools/playtest/run.sh              # headless default
bash tools/playtest/run.sh --gui        # headless then short GUI
bash tools/playtest/run.sh --gui-only   # GUI only
bash tools/playtest/run.sh --deep-gui   # --gui with longer waits / extra shots
bash tools/playtest/run.sh --help
```

The harness is a thin wrapper. Headless work calls `tools/godot/ci.sh` and `dotnet test`. It does not replace Godot CI.

A dirty git tree is left alone. `H1` runs `ci.sh import` (godot `--import` + `dotnet build game/`) when Godot is on PATH. Without Godot, Godot-backed checks are skipped.

## Display

GUI flags use `$DISPLAY`, default **`:19`**.

```bash
DISPLAY=:19 bash tools/playtest/run.sh --gui
```

`--gui` is the short pass. `--deep-gui` is the same path with longer `--quit-after-ms` (override with `PLAYTEST_DEEP_GUI_MS`, default 20000). Use `--deep-gui` on fail or when you need more time to look.

## Artifacts

Each run writes `artifacts/playtest/<run-id>/`:

| Path | What |
| --- | --- |
| `report.json` | Suite result (shape below) |
| `logs/` | Per-check command output |
| `shots/` | GUI screenshots (`scrot`, ImageMagick `import`, or `ffmpeg` x11grab) |

`artifacts/` is gitignored. Exit code is 0 if and only if every non-skipped check passed.

## Headless check IDs

| ID | Name | How |
| --- | --- | --- |
| H1 | verify + import + boot | `ci.sh verify`, `import`, `boot` |
| H2 | lobby | `ci.sh lobby` |
| H3 | hud + live-hud | `ci.sh hud`, `live-hud` |
| H4 | overlays + overlay + live-overlay | `ci.sh overlays`, `overlay`, `live-overlay` |
| H5 | debug | `ci.sh debug` — already asserts `panelRect` width/height > 0 and `panelGlobal.x >= 0` |
| H6 | join | `ci.sh join` |
| H7 | LanAddress / Host advertise | `dotnet test` filter `FullyQualifiedName~LanAddress` plus a default-route `src` assert (`ip route get 8.8.8.8`). Host uses `LanAddress.FirstNonLoopbackIPv4()` (`HostAdvertisement`). |
| H8 | interact | `ci.sh interact` |
| H-play | play | `ci.sh play` |
| H-debug-world | debug-world | `ci.sh debug-world` |
| H-worldstage | worldstage | `ci.sh worldstage` |
| H-leave | leave | `ci.sh leave` |
| H9 | `dotnet test` | `dotnet test PerformativeMail.sln` (Sim + Net + Bot) |
| H-env | toolchain | Fails only when every `H*` check was skipped (no Godot and no `dotnet`) |

Godot-backed IDs skip when `godot` is not on PATH. H7/H9 skip when `dotnet` is not on PATH (H7 still fails if a default-route `src` is loopback/APIPA).

## GUI check IDs (best-effort)

Run only with `--gui`, `--gui-only`, or `--deep-gui`. Scripted Host play uses existing CLI flags (`--host --debug-world --debug-helper=… --quit-after-ms=`).

| ID | Name | How |
| --- | --- | --- |
| G-A | Host play on DISPLAY | GUI `godot --path game -- --host --debug-world`; report `state` is `Playing` |
| G-B | HUD / playing window | Same process; report `hudPhase` is `PREP` |
| G-C | Debug menu (F3 path) | Reuses `ci.sh debug` (panel rect). **F3 / backtick is not key-injected.** |
| G-D | Teleport helper (intake) | `--debug-helper=intake` |
| G-E | Overlay helper | `--debug-helper=overlay`; expects `overlayOpen: true` |
| G-F | Leave / Esc | `--debug-helper=leave`; report `state` is `Menu` |
| G-G | Screenshot set | At least one PNG under `shots/` |

## GUI gaps

Full mouse-look / click automation is out of scope. These are not driven:

- Menu Host/Join button clicks (CLI `--host` / `--join=` is used instead)
- F3 / backtick press to toggle DebugMenu (panel assert is headless `inspect-debug`)
- WASD, mouse look, E interact in the GUI window (`ci.sh interact` covers pickup/deliver headless)
- Guest join on a real LAN IP from the GUI

On visual fail, re-run `bash tools/playtest/run.sh --deep-gui` and inspect `shots/`.

## `report.json` shape

```json
{
  "ok": true,
  "sha": "ca8103852d48cfacd10869efb9516cde27b1ac9b",
  "startedAt": "2026-09-05T19:40:00Z",
  "finishedAt": "2026-09-05T19:42:10Z",
  "checks": [
    {
      "id": "H1",
      "name": "verify + import + boot",
      "status": "pass",
      "detail": "optional tail of the check log",
      "shots": []
    }
  ],
  "shots": ["artifacts/playtest/20260905T194000Z-ca81038/shots/host.png"]
}
```

`status` is `pass`, `fail`, or `skip`.
