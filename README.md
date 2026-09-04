# performative-mail

Co-op rogue-lite logistics game (Arcade v1).

## Spec

Implementation specs are in [`spec/`](spec/).

## Solution layout

`PerformativeMail.sln` holds Sim, Server, Client, App, ContentValidator, BotClient, Sim.Tests, and Net.Tests.

Sim targets netstandard2.1 and has no Godot references. Server and Client are net8.0 class libraries that reference Sim. App is a net8.0 class library that boots a listen-server pair over `LoopbackTransport`. The Godot 4.7.2 C# shell lives in `game/` and is **not** in the solution, so `dotnet test` never loads GodotSharp.

## Play (LAN host / join)

Godot 4.7.2 .NET. Friend A hosts; Friend B joins by LAN IP. Default port is UDP **7777**. Allow inbound UDP 7777 on the host. No NAT traversal in this slice.

```bash
godot --path game
```

1. Friend A clicks **Host**. Host/Join chrome hides; Esc pause shows the LAN join address.
2. Friend B types that address (`192.168.1.20` or `192.168.1.20:7777`) and clicks **Join**.
3. First-person WASD + mouse look. Shift sprints. E interacts. Esc opens Leave / Controls / Options.
4. The live world labels the Post Office, Mail intake, house addresses, and mailboxes.

## Test

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet test PerformativeMail.sln
```

Godot integration is tested in GitHub Actions inside `barichello/godot-ci:mono-4.7.2` (official Godot **4.7.2** .NET / C# editor). The `godot` job runs `tools/godot/ci.sh` once per command.

- `verify` checks Godot 4.7.2 .NET, `godot --headless --quit`, and `dotnet` 8.x.
- `import` imports `game/` and runs `dotnet build`.
- `boot` boots the main scene headless and asserts the C# `_Ready` marker.
- `hud` binds HudFrame and reads Control text.
- `overlay` opens InventoryOverlay and reads cell text.
- `lobby` binds LobbyFrame and reads Control text.
- `overlays` binds payday, draft, and results frames and reads Control text.
- `debug` opens DebugMenu and reads inspect and cheat labels.
- `join` runs a two-process LAN host and join on `127.0.0.1:7777`.
- `play` runs a solo Host play report with the golden worldHash, HUD, and world entity counts listed in [`tools/godot/report.md`](tools/godot/report.md).
- `debug-world` runs a solo Host play report with `--debug-world`. It asserts Playing, PREP, two houses, and debug worldHash `0x4CF184F2FA4D4EEE`.
- `debug-helpers` runs `--host --debug-world --debug-helper=intake` and asserts the local pawn is at Intake (`1100`, `500`). DebugMenu (F3 or backtick) buttons also teleport to Intake or a mailbox, give mail if the hotbar is empty, and open the inventory overlay.
- `worldstage` runs `--host --debug-world --report= --world-dump=` and asserts live WorldStage Label3D text for Post Office, Mail, and the debug addresses, plus SmokeReport `worldEntityCounts.mailboxes >= 2`.
- `interact` runs `--host --debug-world --debug-helper=interact`. It stocks Intake, teleports, holds Interact through pickup and mailbox deliver, and asserts SmokeReport `wallet` is `8`.

`all` runs every command above. `all` is the default when you omit the argument. The command list and the steps to add a Control-text smoke are in [`tools/godot/README.md`](tools/godot/README.md).

```bash
# Same script CI runs (skip the 8.x pin if the image only has SDK 9):
docker run --rm -e REQUIRE_DOTNET_8=0 -v "$PWD":/src -w /src \
  barichello/godot-ci:mono-4.7.2 bash tools/godot/ci.sh
```

## Validate content

From the repo root, ContentValidator loads the M0 test map plus `streets.json`, `world/archetypes.json`, `balance.json`, `unlocks.json`, and the item, mail, building, recipe, shop, perk, and stamp defs. Unknown ids, unknown `Stat` keys, unknown `RuleFlag` values, and district-house sums outside the grown town band fail the process. A recipe whose `produces.building` does not name a loaded building also fails.

```bash
dotnet run --project tools/ContentValidator -- content
```
