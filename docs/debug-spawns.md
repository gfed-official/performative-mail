# Debug spawns

Host F3 lists one spawn row per content item, per mail kind, and Bike. Rows come from `DebugSpawnCatalog.From(bundle, ids)`, not a hand-maintained id list. Clicking a row on a Playing host calls `PlaySessionMachine.TrySpawn`. Guests see the rows disabled and `TrySpawn` returns false.

## Add an item

Drop a new JSON file under `content/items/`. `ContentFiles.Load` picks it up. The spawn catalog and the host menu list it on the next boot. No `DebugMenu` edit.

## Add a ContentBundle facet

`DebugSpawnCoverage.RequireComplete` reflects every public property on `ContentBundle`. Give the property a spawn policy (`SpawnItems`, `SpawnMail`, `SpawnBike`) or mark it `Deferred` / `NotGrantable` with a reason. Perks, stamps, buildings, and shop non-vehicle rows stay deferred with no apply path.

CI enforces this through Sim.Tests coverage tests and `tools/ContentValidator`. `DebugMenu.Dump` prints `SpawnCount=` plus `Spawn.axe=`, `Spawn.letter=`, and `Spawn.bike=` sentinels. It does not pin a full id golden list.
