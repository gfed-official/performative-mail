# performative-mail

Co-op rogue-lite logistics game (Arcade v1).

## Spec

Implementation specs are in [`spec/`](spec/).

## Solution layout

`PerformativeMail.sln` holds Sim, Server, Client, App, ContentValidator, BotClient, Sim.Tests, and Net.Tests.

Sim targets netstandard2.1 and has no Godot references. Server and Client are net8.0 class libraries that reference Sim. App is a net8.0 class library that boots a listen-server pair over `LoopbackTransport`.

## Test

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet test PerformativeMail.sln
```

## Validate content directories

From the repo root, check that `content/` exists with the chapter 07 subdirectories. This does not check schemas yet.

```bash
dotnet run --project tools/ContentValidator -- content
```
