# M0 unit checklist

Check a box only when evidence exists.

## U0 Solution scaffold

- [x] `PerformativeMail.sln` builds
- [x] `dotnet test` green
- [x] ContentValidator exits 0 on `content/`
- [x] Sim has no Godot references
- [x] CI workflow runs restore + build + test + ContentValidator
- [x] CI Godot job uses `barichello/godot-ci:mono-4.7.2` (Godot 4.7.2 .NET)

## U1 GridContainer

- [x] Place / rotate / stack-by-address / quick-move unit tests
- [x] Hotbar hands cell blocked
- [x] Version and hash bump on Apply

## U2 InventorySystem

- [x] Apply plans then privately commits
- [x] Open / NotOpen authorization
- [x] Sort first-fit-decreasing packer
- [x] InventoryAudit conservation on commit
- [x] Replica ApplyDelta matches authoritative hashes
- [x] Concurrent 10 000-op chest fuzz conserves MailIds

## U8 Godot

- [x] Godot 4.7.2 .NET is verified in CI (`godot` on PATH, `--version` is 4.7.2 mono/.NET, `--headless --quit`, `dotnet` still 8.x)
- [x] Headless boot smoke of `game/` plus host/join on 127.0.0.1:7777

## Later units

See `docs/m0-frame.md`.
