# Godot CI script

`tools/godot/ci.sh` is the Godot test entrypoint. The GitHub Actions `godot` job runs each command inside `barichello/godot-ci:mono-4.7.2`.

## Commands

`tools/godot/ci.sh` takes one argument. Omit it to run `all`.

| Command | What it runs |
| --- | --- |
| `verify` | Godot 4.7.2 .NET on PATH, `godot --headless --quit`, and `dotnet` 8.x |
| `import` | `godot --import` and `dotnet build` of `game/` |
| `boot` | Headless main-scene smoke. Asserts the C# `_Ready` marker `performative-mail boot ok`. |
| `hud` | `inspect-hud.sh` with `SKIP_BUILD=1`. Binds HudFrame and reads Control text for match, then mismatch. |
| `overlay` | `inspect-overlay.sh` with `SKIP_BUILD=1`. Opens InventoryOverlay from a U2 replica and reads cell text. |
| `lobby` | `inspect-lobby.sh` with `SKIP_BUILD=1`. Binds LobbyFrame and reads seed and ready text. |
| `overlays` | `inspect-overlays.sh` with `SKIP_BUILD=1`. Binds payday, draft, and results frames and reads Control text. |
| `debug` | `inspect-debug.sh` with `SKIP_BUILD=1`. Opens DebugMenu from DebugBoot and reads inspect and cheat labels. |
| `join` | Two-process LAN host and join on `127.0.0.1:7777`. Asserts both reports are Playing with at least two pawns. |
| `play` | Solo Host play report. Asserts Playing, golden `worldHash` `0x821670054873680E`, HUD phase PREP, and shift `Shift 1 / 5`. Keys, units, and jq are in [report.md](report.md). |
| `debug-world` | Solo Host play report with `--debug-world`. Asserts Playing, PREP, 2 houses / 2 mailboxes, and debug `worldHash` `0x4CF184F2FA4D4EEE`. |
| `debug-helpers` | Solo Host `--debug-world --debug-helper=intake`. Asserts the local pawn report is at Intake tile centre `1100, 500`. |
| `worldstage` | Solo Host `--debug-world` plus `--world-dump=`. Asserts SmokeReport mailbox count ≥ 2 and live Label3D text for Post Office, Mail, and `1 Debug Lane` / `2 Debug Lane`. |
| `all` | Every command above, in that order. `all` is the default. |
| `-h`, `--help` | Print the command list. |

Control-text commands call a sibling `inspect-*.sh`. `ci.sh` sets `SKIP_BUILD=1` because `import` already built `game/`.

| Command | Script | `Main.cs` flag | Dump flag |
| --- | --- | --- | --- |
| `hud` | `inspect-hud.sh` | `--inspect-hud` | `--hud-dump=` |
| `overlay` | `inspect-overlay.sh` | `--inspect-overlay` | `--overlay-dump=` |
| `lobby` | `inspect-lobby.sh` | `--inspect-lobby` | `--lobby-dump=` |
| `overlays` | `inspect-overlays.sh` | `--inspect-overlays` | `--overlays-dump=` |
| `debug` | `inspect-debug.sh` | `--inspect-debug` | `--debug-dump=` |

`join`, `play`, `debug-world`, `debug-helpers`, and `worldstage` live inline in `ci.sh`.

`--debug-helper=` runs one host cheat on the first Playing frame. Values are `intake`, `mailbox`, `give-mail`, and `overlay`. F3 and `` ` `` still toggle DebugMenu for the same cheats.

## Environment

`GODOT_PIN` defaults to `4.7.2`. `PROJECT_PATH` defaults to `game`. `REQUIRE_DOTNET_8` defaults to `1`. Set `REQUIRE_DOTNET_8=0` if the image only has SDK 9.

## CI

The `godot` job in `.github/workflows/ci.yml` runs each command as its own step. It does not call `all`. The steps are `verify`, `import`, `boot`, `hud`, `overlay`, `lobby`, `overlays`, `debug`, `join`, `play`, `debug-world`, `debug-helpers`, and `worldstage`.

## Add a Control-text smoke

1. Copy `tools/godot/inspect-hud.sh` to `tools/godot/inspect-<name>.sh`.
2. Change the dump path, the Main flags, and the `expect` lines to match the Control text you assert.
3. Add `--inspect-<name>` and `--<name>-dump=` in `game/Main.cs` `ApplyArgs`.
4. In `tools/godot/ci.sh`, add a function that runs `SKIP_BUILD=1 bash "$ROOT/tools/godot/inspect-<name>.sh" "$(mktemp)"`.
5. Add the command to `usage`, the `case`, and the `all` branch.
6. Add a step in `.github/workflows/ci.yml` after `import`.
7. List the command in this file and in the repo README Test section.

To add a process smoke like `join` or `play`, write a function in `ci.sh` instead of an inspect script.
