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

1. Friend A clicks **Host**. The status line shows the LAN address to read out.
2. Friend B types that address (`192.168.1.20` or `192.168.1.20:7777`) and clicks **Join**.
3. Both walk with WASD (Shift sprints). You are the pawn labelled **You**; the friend is a different colour.

## Test

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet test PerformativeMail.sln
```

Godot integration is tested in GitHub Actions inside `barichello/godot-ci:mono-4.7.2` (official Godot **4.7.2** .NET / C# editor). That job checks `godot --version`, `godot --headless --quit`, that `dotnet --version` is still 8.x, then imports `game/`, boots the main scene headless, and runs a two-process LAN host/join on `127.0.0.1:7777`.

```bash
# Same script CI runs (skip the 8.x pin if the image only has SDK 9):
docker run --rm -e REQUIRE_DOTNET_8=0 -v "$PWD":/src -w /src \
  barichello/godot-ci:mono-4.7.2 bash tools/godot/ci.sh
```

## Validate content directories

From the repo root, check that `content/` exists with the chapter 07 subdirectories. This does not check schemas yet.

```bash
dotnet run --project tools/ContentValidator -- content
```
