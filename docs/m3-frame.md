# M3 combat frame

## Definition of done

Every M3 acceptance criterion in `spec/12-milestones.md` is falsifiable and must pass on the real artifact:

1. 40 enemies in interest replicate to 8 clients inside the 40 kbps per-client budget. Worst case stays at 80 kbps.
2. Enemy pathing never stalls. A fuzz test that spawns waves against 200 random wall layouts finds a path or a wall to break within 2 s for every enemy.
3. Wall Breakers hit walls. Hog Riders hop walls and steal from depots. Balloons target turrets. Tanks push belts. Each case is a scripted scenario test.
4. Chapter 11 §10.1 sanity checks reproduce in `BalanceSim` with combat agents. A solo player who built 6 wooden walls loses no belts on shift 2.
5. No friendly fire. No enemy damage to mailboxes or houses. Both are assertion tests.

This run cuts those gates into landable child issues. The current landable unit is U1.1. Do not start U1.1 implementation in the frame PR. Do not start M4 or M5.

## Scope

Reuse `RoutingGraph.TryNearestNode`, `RoutingGraph.TryPath`, and `RoutePath`. New enemy types live in `src/Sim/Combat/` (chapter 07). Touch `src/Sim/Core/EntityClass.cs` so agents can own an `EntityId`. Keep Sim free of Godot refs.

U1.1 spawns a Barbarian at a tile, paths toward the Post Office on the existing routing graph, and steps at 4.5 m/s. Sim and xUnit only. No Godot enemy mesh. No wave scheduler. No mega. No new `MessageKind`.

Do not replace `wall_wood` placement. Do not walk `SpawnEdgeRecord.PathToPo` as a pathfinder. Do not change `NpcDriver` or `RouteEnemy`.

## Rigor

High for the acceptance predicate and for U1.1. Server-only agents on `RoutingGraph` are the 40-enemy pathing and bandwidth path (chapter 05 §3.3, chapter 06). Gates are executable tests and measured kbps and stall numbers.

U1.1 shape is already concrete in chapter 05 §2 (HP 60, speed 4.5 m/s, melee swarmer) and in `RoutingGraph.TryPath`. Arena is skipped for the skeleton. Two cut sketches compared in-thread.

## Blockers found while grounding

| Blocker | Impact | Mitigation |
| --- | --- | --- |
| No `src/Sim/Combat/` and no `EntityClass.Enemy` | Agents have no home or id class | U1.1 adds `src/Sim/Combat/` and `EntityClass.Enemy = 4`. Chapter 07 §4.2 already names enemy as a class |
| `RoutingGraph` is a static street node graph. `TryPath` ignores `ConstructRecord` walls | Chapter 05 §3.3 needs walls impassable | U1.1 paths on the existing graph with no walls. U1.6 queries `ConstructRegistry.TryGetAt` off the street edges. U8.2 is the 200-layout fuzz |
| `wall_wood` is `onStreet: false`. `ConstructRegistry` returns `PlaceReject.Street` on street tiles | Street graph edges cannot hold a wall. A wall overlay on Dijkstra street hops never sees a blocker | U1.6 treats walls on lots, the PO pad, and other legal tiles. U8.2 places fuzz walls only where `TryPlace` accepts them. Do not invent a second road graph |
| `SimWorld` does not own `RoutingGraph` or `WorldTables` | Live tick has no graph to query | U1.1 tests construct `new RoutingGraph(...)` the way `NpcDriverTests` does. Binding tables onto `SimWorld` waits until a later unit needs spawn edges |
| `BuildingCatalog` drops JSON `params` | `wall_wood` `wallBreakerResist` never loads. Stone 50 percent resist cannot be data | U5.1 parses `params` or stores resist on `BuildingDef`. U1.1 does not touch the catalog |
| `RoutePath.Tiles` is node tiles, not every street tile | A step that hops nodes skips the road | U1.1 interpolates the node polyline the way `NpcDriver` hops. Not a new nav |
| `RouteEnemy` already exists for NPC flee | Easy to treat it as the agent type | Leave `NpcDriver` and `RouteEnemy` alone in U1.1. Later units may project agent pose into `NoticeEnemies` |
| `ConstructRecord` has `Hp` and `MaxHp` but no damage API | Melee and siege cannot land | U3.1 owns `ApplyDamage`. U1.1 deals none. `wall_wood` stays at 300 HP |
| `PlayerBody` has `HpPct` only | Player combat has no integer HP | U4.1 adds integer HP 100. U1.1 does not touch players |
| `BalanceSim` has delivery agents only | Chapter 11 §10.1 combat agents do not exist | U8.4 adds them. U1.1 does not touch `BalanceSim` |
| No Godot binary on the agent host | Enemy mesh and raid HUD cannot be live-checked here | Keep U1.1 in Sim and xUnit. Godot views wait for U7 |
| Enemy replication is a one-way door | Early `MessageKind` values can blow the 40 kbps budget | U1.1 adds no `MessageKind`. U1.11 is codec tests. U8.1 is the 40 kbps gate |
| PO is `PostOfficeRecord`, not a construct | PO death cannot end the run today | U1.1 paths to `PostOfficeRecord.SpawnPadTile` (already a route node). U3.5 owns PO HP 3000 and run end |
| Mailboxes and houses are atlas destinations, not constructs | "No damage" needs an explicit deny list | U1.3 and U8.5 assert enemies never select those destination types |
| `content/enemies/` and `content/waves/` exist and are empty | `ContentValidator` only requires the directories | U1.1 hardcodes Barbarian constants from chapter 05 §2. `EnemyDef` JSON waits until a second kind or U1.4 |
| `ShiftClock` already enters `RunPhase.Raid` in the last 90 s of Delivery on shift 2 and later | A second raid clock would desync from M1 | U2.1 binds waves to the existing phase. U1.1 does not read the clock |

## Workflow (Phase B)

Riskiest unknown first: a server-only enemy that reuses `RoutingGraph`. Smallest landable first: U1.1.

| Unit | Landable change | Verify |
| --- | --- | --- |
| U1 | Enemy agents spawn, path on `RoutingGraph`, carry traits, pick targets | One Barbarian steps toward the PO. Later children add targeting, wall overlay, and the roster |
| U2 | Wave scheduler: budget, pulses, spawn edges, warning, end-of-raid flee | `RunPhase.Raid` spends budget. Shift 1 stays out of Raid. Clock end flees |
| U3 | Construct damage, HP snapshot while damaged, ruins, spill, PO death ends run | `wall_wood` HP falls. Ruin at 0. PO at 0 ends the run |
| U4 | Player combat: melee arc, hitscan plus rewind, weapons, bandages | Arc hits an enemy. Hitscan rewind matches the buffer. No friendly fire |
| U5 | Stone wall, gate, spikes, turret, alarm, repair hammer | Each def places and does the chapter 05 §4 job |
| U6 | Drops, Lost Parcels, Mega variants, Cursed Mail flag | Death drop table. Mega roll. Mail can carry the Cursed flag. Mini-raids wait for M4 |
| U7 | Raid HUD: warning, compass markers, under-attack, construct HP bars | Warning bind at 15 s. Damaged construct shows a bar |
| U8 | M3 acceptance gates | Criteria 1 to 5 on the real artifact |

### U1 children (enemy agents)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U1.1 | Barbarian spawn, path toward PO on `RoutingGraph`, Sim step | Recipe-free spawn. One agent marches 4.5 m in 1 s. No damage | M2 on main (`RoutingGraph`, `ConstructRegistry`) |
| U1.2 | Last 15 m straight-line and 1 s retarget to nearest player or construct | At 15 m the agent leaves the graph. Retarget after 1 s picks the nearest valid class. For 1 or 2 players, constructs win ties (chapter 01 §8) | U1.1 |
| U1.3 | Barbarian melee 10 damage / 1.0 s through the U3.1 API | A `wall_wood` loses 10 HP. Mailbox and house tiles are never selected | U1.2, U3.1 |
| U1.4 | Archer (HP 40, 8 damage, 1.5 s, 4 m/s, ranged 15 m, keep 10 m) | Priority is player then turret then construct. Agent retreats when a player closes | U1.2 |
| U1.5 | Giant (HP 400, 40 damage, 2.0 s, 3 m/s). Buildings first | Ignores players unless one attacks within 5 m for 3 s | U1.2, U3.1 |
| U1.6 | Occupancy overlay via `ConstructRegistry.TryGetAt`. Walls impassable except Hog Rider | A wall on a legal (non-street) tile is a named blocker. Street `RoutePath` hops stay open. Not a second graph. `Pipes.ClimbId` stays `wall_wood` | U1.2, M1 `wall_wood` |
| U1.7 | Wall Breaker. Suicide 150 to walls, gates, belts. 20 otherwise | Runs to the wall on the shortest blocked path to the PO and explodes (r = 1.5 m) | U1.6, U3.1 |
| U1.8 | Hog Rider. Jump 1 m. Each container hit drops 1 item 3 m away | Hops a `wall_wood`. Steals from a depot or chest | U1.6, U3.1, M2 depot or chest |
| U1.9 | Balloon. Airborne 6 m. Bombs turrets | Only ranged weapons and turrets hit it. Priority is turret then depot | U1.4, U5.4 |
| U1.10 | Tank. Shift 4 and 5. Pushes constructs. Destroys belts in path | Priority is PO then depot then any construct. Pickaxe 2× waits for U4 | U1.6, U3.1 |
| U1.11 | Enemy snapshot fields (pos, yaw, anim, HP percent) in Sim and Net tests | Encode and decode. Enemy payload stays near 12 bytes (chapter 06). No Godot view | U1.1, M1 wire codec |

### U1.1 acceptance

Title: `[M3 U1.1] Barbarian enemy agent: spawn + path toward PO on RoutingGraph + Sim step`

Landable change:

- Add types under `src/Sim/Combat/`. Sim stays free of Godot refs.
- Named shape: `EnemyKind` closed id (`barbarian` first). `EnemyAgent` row holds `EntityId`, kind, pose in cm, HP 60, speed 4.5 m/s, and a path cursor along a `RoutePath`. `EnemyTable` owns spawn and step. Organizing structure is a table plus path cursor, not scattered booleans.
- U1.1 state machine is Spawned, then Marching, then Arrived. No Attack state.
- Add `EntityClass.Enemy = 4` so agents get `EntityId`s.
- Spawn a Barbarian at a `TileCoord`. A spawn-edge tile is allowed as that coordinate.
- Tests construct `new RoutingGraph(...)` from node and edge lists. Do not add `WorldTables` to `SimWorld` in this unit. Do not walk `SpawnEdgeRecord.PathToPo`.
- Call `RoutingGraph.TryPath` from the spawn tile to `PostOfficeRecord.SpawnPadTile`. Follow `RoutePath.Tiles` as a polyline at 4.5 m/s. Reuse `RoutingGraph`. Do not invent a second nav system.
- Stats from chapter 05 §2 are HP 60, speed 4.5 m/s, and melee-swarmer targeting of the nearest player or construct. This unit marches toward the PO only.
- `SimWorld.Tick` calls the combat step the way it already calls `Belts.Step`.

Out of this unit:

- Damage dealing (U1.3 and U3.1)
- Last 15 m straight-line and 1 s retarget (U1.2)
- Wall-aware path overlay (U1.6)
- Wave scheduler, pulses, warning, flee (U2)
- Mega, drops, Lost Parcels, Cursed Mail (U6)
- Godot enemy mesh and `EnemyView`
- Any new `MessageKind`
- Changes to `NpcDriver` or `RouteEnemy`
- Stone wall, gate, spikes, turret, alarm, repair hammer
- Player weapons, hitscan, bandages
- M4 and M5

Verify:

- `dotnet test` spawns a Barbarian on a line graph. After `RoutingGraph.TryPath` the agent's `RoutePath` last tile is the PO node.
- After 30 ticks (1 s at 30 Hz) the agent has moved 4.5 m along that polyline, within one tick of 0.15 m.
- HP is 60. Speed is 4.5 m/s. Kind is barbarian. `EntityId.Class` is `EntityClass.Enemy`.
- A second spawn gets a distinct `EntityId`.
- `SimWorld.Tick` advances the agent when a table is bound.
- `ContentValidator` still exits 0 on repo content.
- No new `MessageKind`. No Godot scene. No wave, mega, or damage types.

### U2 children (wave scheduler)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U2.1 | Bind waves to existing `ShiftClock` `RunPhase.Raid` (last 90 s, shift ≥ 2) | Shift 2 Delivery enters Raid at 90 s left. Shift 1 never does. No second clock | U1.1, M1 `ShiftClock` |
| U2.2 | `waveBudget(shift, n)` from chapter 05 §3.2 | Shift 2 solo is 96. Shift 1 is 0. `baseBudget` is `[0, 120, 220, 360, 560]` | U2.1 |
| U2.3 | Six pulses every 15 s. Pulse 6 gets +50%. Edges 1 then 2 | Pulse spend sums to the wave budget. Pulses 4 to 6 use two spawn edges | U2.2, U1.1 |
| U2.4 | Warning 15 s before the first spawn. Spawn edge marked | Warning flag is on 15 s before the first pulse. Edge id is set | U2.3 |
| U2.5 | End-of-raid Flee to the spawn edge for 5 s, then despawn | Shift clock end starts Flee. No enemy remains in Prep | U2.3, U1.1 |
| U2.6 | Weighted composition, unlock shifts, Tank caps | Shift 2 rolls only Barbarian and Archer. Shift 4 Tank cap is 1. Shift 5 cap is 3 | U2.3 |

### U3 children (construct damage)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U3.1 | `ApplyDamage` on `ConstructRecord.Hp`. `wall_wood` starts at 300 | One hit of 10 leaves 290. At 0 the row is ready for ruin | U1.1, M1 `ConstructRegistry` |
| U3.2 | Construct HP snapshot as uint8 percent only while damaged and in interest | Full HP sends no HP field. Damaged 150 of 300 encodes 50 percent | U3.1, M1 wire codec |
| U3.3 | Ruin at 0 HP. Prep rebuild at 50 percent materials. Delivery must deconstruct | Ruin occupies the tiles and does not function. Prep rebuild restores filters | U3.1 |
| U3.4 | Item spill from a destroyed container. Belt items on a dead segment drop | Chest contents become `WorldItem`s. Mail returns to Intake after despawn | U3.3, M1 chest, M2 belts |
| U3.5 | PO HP 3000. At 0 the run ends. Regen 5 HP/s during Prep | One uninterrupted Giant (20 DPS) needs 150 s. Shift 3 cannot finish the PO if a player answers | U3.1, M1 `RunState` |

### U4 children (player combat)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U4.1 | Player integer HP 100. Regen 2 HP/s after 5 s without damage | A 10-damage hit leaves 90. After 5 s idle, 1 s later HP is 92 | M1 `PlayerBody` |
| U4.2 | Melee 0.6 s swing, 60° arc. Fists 5 at 1.5 m. Axe 15 at 2 m | An enemy in the arc loses the table damage. One outside does not | U4.1, U1.1 |
| U4.3 | Hitscan plus 250 ms rewind buffer (chapter 06) | A lagged shot hits the rewound pose and misses the live pose | U4.1, M0 tick |
| U4.4 | Shop Mail Bat and Slingshot. Bandage heals 50 HP | Bat 25 at 2.5 m. Slingshot spends 1 Stone per 5 shots. Bandage is instant | U4.2, U4.3, M1 shop |
| U4.5 | No friendly fire. Players cannot damage constructs with weapons | A punch that overlaps a teammate deals 0. An axe on `wall_wood` deals 0 | U4.2, U3.1 |
| U4.6 | Package Cannon. 40 AoE, r = 2 m, consumes a Small Package | Package is destroyed. Misdelivery penalty does not apply. Mail is lost | U4.3, M1 mail |

### U5 children (defenses)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U5.1 | Stone wall. 800 HP. Wall Breaker damage reduced 50 percent | Place `wall_stone`. A 150 Wall Breaker hit deals 75 | U3.1, U1.7, chapter 10 `bp_defense` |
| U5.2 | Gate. 500 HP. Opens for players and friendly vehicles. Enemies treat it as a wall | Player and truck pass. Barbarian does not. Hog Rider still jumps | U1.6, M2 `VehicleStep` |
| U5.3 | Spike strip. 150 HP. 15 dmg/s to ground enemies. Does not block | A Barbarian on the tile loses 15 HP per second. Players take 0 | U1.3, U4.1 |
| U5.4 | Turret auto. 400 HP. 12 damage / 0.5 s, 18 m. Can hit Balloons | Nearest enemy in range is hit. Balloon in range is hit | U1.2, U3.1 |
| U5.5 | Turret operated. 2× fire rate and manual aim | Mounted fire is 12 / 0.25 s. Player stays damageable | U5.4, U4.1 |
| U5.6 | Alarm post. 100 HP. Warning +15 s. Marks enemies within 40 m | Warning duration grows by 15 s per post. Early Warning perk waits for M4 | U2.4 |
| U5.7 | Repair hammer. 50 HP/s. Consumes 25 percent of build cost per full bar | A 300 HP wall full repair spends 25 percent of `recipe_wall_wood` | U3.1, M1 shop 100 ¢ |

### U6 children (drops, mega, cursed)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U6.1 | Death drops. `WorldItem` despawn 2 min. Wall Breaker drops nothing | Barbarian 1 Fiber at 50 percent. Giant 3 Stone and 1 Iron Ore at 100 percent | U1.3, M2 `WorldItem` |
| U6.2 | Mega roll. 5 percent plus 2 percent per shift. Shift 5 pulse 6 always has at least one Mega | HP ×2.5, damage ×1.5, scale ×1.4, speed ×0.9. Aura +20 percent speed within 8 m. Megamail ×2 waits for M4 | U1.1, U2.3 |
| U6.3 | Lost Parcel on Mega death. Medium Package, random unlocked address, value ×3, plus 2 Iron Ingots | Parcel address is unlocked. Value is 3× the medium baseline | U6.2, M1 mail |
| U6.4 | Cursed Mail flag on a mail item (chapter 12 M3 flag support) | Flag round-trips on the item. Mini-raid spawn and ×1.5 pay wait for M4 | M1 mail |

### U7 children (raid HUD)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U7.1 | Raid warning banner and horn bind (15 s) | Bound snapshot shows the warning text and the spawn edge | U2.4, M1 HUD bind path |
| U7.2 | Compass raid-edge icon and construct-under-attack marker | Attacked construct is flagged global (chapter 06). Compass lists it | U3.1, U7.1, M1 compass |
| U7.3 | Construct HP bars when damaged | Full-HP constructs show no bar. Damaged `wall_wood` shows remaining HP | U3.2, U7.2 |
| U7.4 | Mega sighted announcement | Spawn of a Mega emits the HUD line with the edge ("Mega Giant sighted, north") | U6.2, U7.1 |

### U8 children (gates)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U8.1 | 40 enemies in interest to 8 clients | Per-client down ≤ 40 kbps. Worst case ≤ 80 kbps | U1.11, U2.3 |
| U8.2 | Pathing fuzz. 200 random wall layouts on legal place tiles | Every enemy finds a path or a wall to break within 2 s. Hog Rider and Balloon use their trait success (jump, air) | U1.6, U1.7, U2.3 |
| U8.3 | Scripted Wall Breaker, Hog Rider, Balloon, Tank | Each chapter 12 scenario test passes | U1.7, U1.8, U1.9, U1.10 |
| U8.4 | `BalanceSim` chapter 11 §10.1 combat agents | Solo shift 2 with 6 `wall_wood` tiles loses 0 belts. Giant 20 DPS kills a belt in 4 s, a depot in 40 s, the PO in 150 s | U2.2, U3.1, U1.3, U1.4, U1.5, M1 `wall_wood` |
| U8.5 | No friendly fire. No enemy damage to mailboxes or houses | Assertion suite on both rules | U4.5, U1.3 |

Architect arena runs before U1.11 (replication payload is a one-way door) and before U4.3 (hitscan rewind). U1.1 shape is already concrete in chapter 05 §2 and `RoutingGraph.TryPath`, so arena is skipped for the skeleton. U1.2 to U1.10, U2, U3, U4.1, U4.2, U4.4 to U4.6, U5, U6, U7, and U8 compose named spec tables, so arena is skipped. Two sketches compared in-thread when a unit forks.

**Cut sketches (architect Phase B).**

1. Family cut (chosen). Eight families that match the M3 systems list. U1 is Sim spawn, path, and step. Net, Godot, waves, damage, HUD, and gates stay later. U1.1 is one PR.
2. Raid lump (rejected). One unit that spawns, schedules waves, deals damage, and draws a raid HUD. Not PR-sized. Mixes Sim, net, and UI. Subtract-before-add loses.

## GitHub issues

Parent: [#15](https://github.com/gfed-official/performative-mail/issues/15).

| Unit | Issue |
| --- | ---: |
| U1 | TBD |
| U1.1 | TBD |
| U1.2 | TBD |
| U1.3 | TBD |
| U1.4 | TBD |
| U1.5 | TBD |
| U1.6 | TBD |
| U1.7 | TBD |
| U1.8 | TBD |
| U1.9 | TBD |
| U1.10 | TBD |
| U1.11 | TBD |
| U2 | TBD |
| U2.1 | TBD |
| U2.2 | TBD |
| U2.3 | TBD |
| U2.4 | TBD |
| U2.5 | TBD |
| U2.6 | TBD |
| U3 | TBD |
| U3.1 | TBD |
| U3.2 | TBD |
| U3.3 | TBD |
| U3.4 | TBD |
| U3.5 | TBD |
| U4 | TBD |
| U4.1 | TBD |
| U4.2 | TBD |
| U4.3 | TBD |
| U4.4 | TBD |
| U4.5 | TBD |
| U4.6 | TBD |
| U5 | TBD |
| U5.1 | TBD |
| U5.2 | TBD |
| U5.3 | TBD |
| U5.4 | TBD |
| U5.5 | TBD |
| U5.6 | TBD |
| U5.7 | TBD |
| U6 | TBD |
| U6.1 | TBD |
| U6.2 | TBD |
| U6.3 | TBD |
| U6.4 | TBD |
| U7 | TBD |
| U7.1 | TBD |
| U7.2 | TBD |
| U7.3 | TBD |
| U7.4 | TBD |
| U8 | TBD |
| U8.1 | TBD |
| U8.2 | TBD |
| U8.3 | TBD |
| U8.4 | TBD |
| U8.5 | TBD |

## Playbook

Feature owns this frame. `how` grounded `RoutingGraph`, `SimWorld`, `ConstructRecord`, chapter 05, and chapter 12 § M3. `architect` compared the two cut sketches. `prove-it-works` checks the frame file against the M3 criteria and the PR body (must not close #15). `subtract-before-you-add` keeps U1.1 path-only on one Barbarian.

Each later unit uses Feature discipline inside the loop (named data shape, delegated code, real-artifact verify, small commits).
