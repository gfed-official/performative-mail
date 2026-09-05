# M2 automation frame

## Definition of done

Every M2 acceptance criterion in `spec/12-milestones.md` is falsifiable and must pass on the real artifact:

1. 5000 belt items simulate server-side inside the 8 ms tick budget. 400 belt tiles stay at ≤ 60 fps on a mid-range client.
2. After 10 min of a 30-segment factory under 100 ms / 2% loss, checksum resends stay ≤ 1 per segment per minute. No item renders at an endpoint before the server confirms.
3. A tester builds the chapter 11 §9.1 reference factory (40 belt tiles, 1 Address Sorter, 4 Inserters) from harvested and shop materials in one Prep plus one Delivery on a Small Island seed.
4. A sorter with per-street filters routes 1000 generated items with zero misroutes (unit test). Overflow receives exactly the unmatched items.
5. An NPC truck completes a 3-stop route and delivers only matching mail in an integration test. Player takeover and hand-back finish within one tick.
6. Solo shift 3 and shift 5 viability checks pass in BalanceSim with the automation agents.

This run cuts those gates into landable child issues. The current landable unit is U1.1 (issue 208). Do not start U1.1 implementation in the frame PR. Do not start M3.

## Scope

Reuse `ConstructRegistry.TryPlace`, `BuildingDef`, and `BuildingBehaviour.Belt`. New belt compile and step live in `src/Sim/Automation/` (chapter 07). Touch `content/buildings/` and `content/recipes/` for `belt_mk1`. Keep Sim free of Godot refs.

U1.1 places `belt_mk1`, compiles one straight segment, and steps two lanes. Sim + xUnit only. No Godot build mode. No `LaneInsert` / `LaneChecksum` replication. No sorter, truck, or boat.

Do not replace Wooden Wall or Chest placement. Do not flatten terrain, drag `PlaceLine`, or deconstruct in U1.1.

## Rigor

High for the acceptance predicate and for U1.1. Compiled segments plus contiguous lane arrays are the 5000-item / 8 ms path (chapter 07 §8). Gates are executable tests and measured tick / fps numbers.

U1.1 shape is already concrete in chapter 04 §2.1 (compiled runs, two lanes, 0.5 m spacing, Mk1 2 m/s) and in `ConstructRegistry.TryPlace`. Arena is skipped for the skeleton. Two cut sketches compared in-thread.

## Blockers found while grounding

| Blocker | Impact | Mitigation |
| --- | --- | --- |
| `content/buildings/` has only `wall_wood` and `chest` | `belt_mk1` has no def or recipe | U1.1 adds `belt_mk1` JSON and `recipe_belt_mk1`. Reuse `BuildingCatalog` and `BuildingBehaviour.Belt` |
| `ConstructRegistry` rejects street tiles unless `OnStreet` | Chapter 04 lets belts cross streets only when elevated | U1.1 keeps `onStreet: false`. Elevated street crossing is U1.3 |
| No Godot binary on the agent host | Build-mode ghost and 400-tile fps cannot be live-checked here | Keep U1.1 in Sim + xUnit. Godot build mode waits for U9. Client fps is U10.2 |
| Belt replication is a one-way door | Event + checksum cost can blow the 40 kbps budget | U1 proves compile and step first. U3 owns `LaneInsert` / `LaneChecksum` / interest. U10.3 is the 10 min desync gate |
| M1 live play still uses the authored atlas beside `WorldOffer` | Intake outfeed needs a real PO face | U1.5 binds outfeed to the Intake construct face. U1.1 does not touch Intake |

## Workflow (Phase B)

Riskiest unknown first: compiled segments and lane stepping (tick budget). Smallest landable first: U1.1.

| Unit | Landable change | Verify |
| --- | --- | --- |
| U1 | `belt_mk1` place, compile, lane step, corners, ramps, split/merge, endpoints, `PlaceLine` | One straight segment steps; later children add geometry and sinks |
| U2 | Terrain flatten deltas; `PlaceConstruct` / `RemoveConstruct` events | Flatten stays under 1 m. Place and remove emit confirmed events |
| U3 | Belt replication | `LaneInsert` / `LaneRemove` / `LaneChecksum` / `LaneState`; interest enter sends full state |
| U4 | Address Sorter, Inserter, Depot | 1000-item zero misroute; inserter 1 / 0.8 s; depot belt faces |
| U5 | Pipes behind `bp_pipes` | Capsule routing to a chosen outlet; unmatched use the default |
| U6 | Mail Truck, Vehicle Depot, NPC drivers | 3-stop route delivers matching mail only; takeover in one tick |
| U7 | Oil Pump, pier, rowboat, motorboat, Small Port, NPC captain | Pump 1 Oil Can / 45 s; water route uses the navmesh |
| U8 | Map layers, filter chips, pings, route editor | Stops persist on the depot route; pings rate-limit 1 / s |
| U9 | Build-mode ghost and sorter filter panel | Ghost validity colour; filter chips list unlocked addresses |
| U10 | M2 acceptance gates | Criteria 1–6 on the real artifact |

### U1 children (belts)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U1.1 | `belt_mk1` place + compile one straight segment and step lanes | Recipe consume; one N-tile line is one segment; two lanes at 2 m/s and 0.5 m spacing | M1 on main (`ConstructRegistry`, `BuildingDef`) |
| U1.2 | Compile maximal runs (corners, adjacent same-tier joins) | A bent run is one segment between endpoints; a join at a facing change stays one compiled path | U1.1 |
| U1.3 | Ramps (2 tiles, 1 m) and elevated street crossing | Street tile accepts elevated; flat `belt_mk1` still rejects street | U1.2 |
| U1.4 | Splitter and merger (compile breaks, round-robin) | 1→3 skip-blocked; 3→1; optional kind filter on splitter | U1.2 |
| U1.5 | Endpoints into mailbox, chest, air-drop `WorldItem`; Intake outfeed FIFO | Head item leaves the lane only when the sink accepts; unmatched air-drop despawn path exists | U1.1, M1 destinations and chest |
| U1.6 | `PlaceLine` straight drag and deconstruct | One request places a line; Prep returns 100%; Delivery returns 50% | U1.1 |

### U1.1 acceptance

Title: `[M2 U1.1] belt_mk1 place + compile one straight segment and step lanes`

Landable change:

- Add `content/buildings/belt_mk1.json` and `content/recipes/recipe_belt_mk1.json` (chapter 10 §2.1: 1 tile, 80 HP, 1 Plank + 1 Iron Ingot, no blueprint).
- Place through `ConstructRegistry.TryPlace("belt_mk1", tile, facing, owner)`. Reuse slope, bounds, water, street, occupied, and recipe consume.
- Compile consecutive same-facing `belt_mk1` tiles into one straight `BeltSegment` with two lanes. U1.1 segment identity is the current tile run. Recompile after place. Stable ids wait for U3.
- Step both lanes in Sim at Mk1 2 m/s. Minimum spacing is 0.5 m (4 letter slots per lane per 2 m tile).
- Letters only. Cargo both-lanes waits for U1.5 item rules or a later occupancy test on this segment type.
- New types live under `src/Sim/Automation/`. Sim stays free of Godot refs.

Out of this unit:

- Godot build mode, ghost, pipette, drag UI
- `LaneInsert`, `LaneRemove`, `LaneChecksum`, `LaneState`, interest
- Corners, ramps, elevated, splitter, merger, sorter, inserter, depot
- Truck, boat, pipes, map, flatten, `PlaceLine`, deconstruct
- Player-on-belt carry and push-to-boost

Verify:

- `dotnet test` places `belt_mk1` and consumes the recipe.
- A 4-tile east-facing line compiles to exactly one segment of length 8 m.
- Insert at lane 0 position 0. After 30 ticks (1 s at 30 Hz) the item is at 2 m. Lane 1 is empty.
- Insert at lane 1 position 0 on a fresh segment. After 30 ticks that item is at 2 m. Lane 0 is empty.
- A second insert on the same lane closer than 0.5 m behind the head is rejected or blocked.
- The same segment hashed or listed twice matches. A 3-tile line is a different segment length.
- `ContentValidator` still exits 0 on repo content.
- No new net `MessageKind`. No Godot scene. No sorter / truck / boat types.

### U2 children (build remainder)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U2.1 | Terrain flattening deltas under buildings (≤ 1 m) | Slope that failed U8.2 becomes placeable after flatten; world delta is an event | U1.1, M1 wall/chest |
| U2.2 | `PlaceConstruct` and `RemoveConstruct` requests | Confirmed event reaches other clients; inventory consume stays server-side | U1.6, M1 constructs |

### U3 children (replication)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U3.1 | `LaneInsert` and `LaneRemove` events | Client lane count matches server after insert and remove | U1.1, M1 wire codec |
| U3.2 | `LaneChecksum` every 2 s and `LaneState` resend | Quantised 0.25 m hash mismatch triggers one full resend | U3.1 |
| U3.3 | Client visual sim and segment interest | Entering interest sends `LaneState`. Endpoint render waits for server remove | U3.2, chapter 06 §4 |

### U4 children (sorter, inserter, depot)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U4.1 | Address Sorter Mk1 (1 / 0.5 s, 1 filter slot, overflow, 2×4 buffer) | 1000 generated items, zero misroutes; overflow is exactly the unmatched set | U1.5 |
| U4.2 | Inserter Mk1 (1 / 0.8 s, optional kind filter) | Pulls from the tile behind into the tile ahead | U1.5 |
| U4.3 | Depot and PO Depot belt faces | Belts feed any side; inserters pull any side | U4.2, M1 chest container |

### U5 children (pipes)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U5.1 | Pipe pieces, capsules, inlet filter (`bp_pipes`) | Inlet maps filter → outlet id; unmatched use default outlet | U1.5, M1 shop blueprint |
| U5.2 | Junctions, underground (2× cost), vertical | Graph routes around a blocked outlet | U5.1 |

### U6 children (truck, NPC)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U6.1 | Mail Truck plus parked loading zone | Belt or inserter loads only when speed < 0.1 m/s | U4.2, M1 `VehicleStep` |
| U6.2 | Vehicle Depot and route model | Ordered stops: address, district macro, or construct | U6.1 |
| U6.3 | NPC driver 3-stop route, matching mail only, takeover | Integration: no misdelivery; takeover and hand-back in one tick | U6.2 |
| U6.4 | Flee stub (enemies within 20 m → return to depot) | Route aborts to depot; no combat AI | U6.3 |

### U7 children (sea, oil)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U7.1 | Oil Pump (1 Oil Can / 45 s) | Output item and rate from chapter 11 | U1.1, M1 harvest |
| U7.2 | Pier and rowboat placement | Shallow-water tiles; NPC cannot operate the rowboat | U2.1, M1 shop / Sea kit |
| U7.3 | Motorboat, Small Port, NPC captain | Dock load by belt; water route on the navmesh | U6.3, U7.1, U7.2 |

### U8 children (map)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U8.1 | Map layers, filter chips, pings | Ping event rate-limit 1 / s | M1 HUD bind path |
| U8.2 | Route editor on the map | Click order and district macro write the U6.2 route | U6.2, U8.1 |

### U9 children (build and filter UI)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U9.1 | Build-mode ghost, validity colour, pipette, drag line | Reason text matches Sim reject; drag sends `PlaceLine` | U1.6, U2.2, M1 Godot HUD |
| U9.2 | Sorter filter panel | Chips are unlocked addresses only; live unmatched count | U4.1, U9.1 |

### U10 children (gates)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U10.1 | 5000 belt items inside 8 ms | Server tick p99 ≤ 8 ms on the packed factory | U1.5, U1.4 |
| U10.2 | 400 belt tiles at ≤ 60 fps | Mid-range client frame time during the packed view | U3.3, U9.1 |
| U10.3 | 10 min desync on a 30-segment factory | ≤ 1 checksum resend per segment per minute; no early endpoint render | U3.3 |
| U10.4 | Reference factory in one Prep + one Delivery | 40 belts, 1 sorter, 4 inserters from harvest / shop | U4.1, U4.2, U9.1 |
| U10.5 | BalanceSim solo shift 3 and shift 5 | Automation agents meet chapter 11 §4 cells | U4.1, U6.3, M1 BalanceSim |

Architect arena runs before U3.1 (replication events are a one-way door) and before U6.3 (NPC route ownership). U1.1 shape is already concrete in chapter 04 §2.1 and `ConstructRegistry.TryPlace`, so arena is skipped for the skeleton. U1.2–U1.6, U2, U4, U5, U7, U8, U9, and U10 compose named spec tables, so arena is skipped; two sketches compared in-thread when a unit forks.

**Cut sketches (architect Phase B).**

1. Family cut (chosen). Ten families that match M1. U1 is Sim place / compile / step. Net, Godot, sorter, truck, boat, map, and gates stay later. U1.1 is one PR.
2. Transport lump (rejected). One unit that compiles belts, sends `LaneInsert`, and draws ghosts. Not PR-sized. Mixes Sim, net, and UI. Subtract-before-add loses.

## GitHub issues

Parent: [#14](https://github.com/gfed-official/performative-mail/issues/14).

| Unit | Issue |
| --- | ---: |
| U1 | [#198](https://github.com/gfed-official/performative-mail/issues/198) |
| U1.1 | [#208](https://github.com/gfed-official/performative-mail/issues/208) |
| U1.2 | [#209](https://github.com/gfed-official/performative-mail/issues/209) |
| U1.3 | [#210](https://github.com/gfed-official/performative-mail/issues/210) |
| U1.4 | [#211](https://github.com/gfed-official/performative-mail/issues/211) |
| U1.5 | [#212](https://github.com/gfed-official/performative-mail/issues/212) |
| U1.6 | [#213](https://github.com/gfed-official/performative-mail/issues/213) |
| U2 | [#199](https://github.com/gfed-official/performative-mail/issues/199) |
| U2.1 | [#214](https://github.com/gfed-official/performative-mail/issues/214) |
| U2.2 | [#215](https://github.com/gfed-official/performative-mail/issues/215) |
| U3 | [#200](https://github.com/gfed-official/performative-mail/issues/200) |
| U3.1 | [#216](https://github.com/gfed-official/performative-mail/issues/216) |
| U3.2 | [#217](https://github.com/gfed-official/performative-mail/issues/217) |
| U3.3 | [#218](https://github.com/gfed-official/performative-mail/issues/218) |
| U4 | [#201](https://github.com/gfed-official/performative-mail/issues/201) |
| U4.1 | [#219](https://github.com/gfed-official/performative-mail/issues/219) |
| U4.2 | [#220](https://github.com/gfed-official/performative-mail/issues/220) |
| U4.3 | [#221](https://github.com/gfed-official/performative-mail/issues/221) |
| U5 | [#202](https://github.com/gfed-official/performative-mail/issues/202) |
| U5.1 | [#222](https://github.com/gfed-official/performative-mail/issues/222) |
| U5.2 | [#223](https://github.com/gfed-official/performative-mail/issues/223) |
| U6 | [#203](https://github.com/gfed-official/performative-mail/issues/203) |
| U6.1 | [#224](https://github.com/gfed-official/performative-mail/issues/224) |
| U6.2 | [#225](https://github.com/gfed-official/performative-mail/issues/225) |
| U6.3 | [#226](https://github.com/gfed-official/performative-mail/issues/226) |
| U6.4 | [#227](https://github.com/gfed-official/performative-mail/issues/227) |
| U7 | [#204](https://github.com/gfed-official/performative-mail/issues/204) |
| U7.1 | [#228](https://github.com/gfed-official/performative-mail/issues/228) |
| U7.2 | [#229](https://github.com/gfed-official/performative-mail/issues/229) |
| U7.3 | [#230](https://github.com/gfed-official/performative-mail/issues/230) |
| U8 | [#205](https://github.com/gfed-official/performative-mail/issues/205) |
| U8.1 | [#231](https://github.com/gfed-official/performative-mail/issues/231) |
| U8.2 | [#232](https://github.com/gfed-official/performative-mail/issues/232) |
| U9 | [#206](https://github.com/gfed-official/performative-mail/issues/206) |
| U9.1 | [#233](https://github.com/gfed-official/performative-mail/issues/233) |
| U9.2 | [#234](https://github.com/gfed-official/performative-mail/issues/234) |
| U10 | [#207](https://github.com/gfed-official/performative-mail/issues/207) |
| U10.1 | [#235](https://github.com/gfed-official/performative-mail/issues/235) |
| U10.2 | [#236](https://github.com/gfed-official/performative-mail/issues/236) |
| U10.3 | [#237](https://github.com/gfed-official/performative-mail/issues/237) |
| U10.4 | [#238](https://github.com/gfed-official/performative-mail/issues/238) |
| U10.5 | [#239](https://github.com/gfed-official/performative-mail/issues/239) |

## Playbook

`figure-it-out` owns the run. `how` grounded `ConstructRegistry` / `BuildingDef` / chapter 04 / chapter 06 §4. `architect` compared the two cut sketches. `prove-it-works` checks the frame file, the issue list, and the PR body (must not close #14). `subtract-before-you-add` keeps U1.1 letters-only on one straight segment.

Each later unit uses Feature discipline inside the loop (named data shape, delegated code, real-artifact verify, small commits). Decision trail: `docs/m2-decisions.tsv`.
