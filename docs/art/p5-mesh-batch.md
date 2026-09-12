# P5 mesh batch — locked specs (U7 vehicles / structures)

Tickets in flight: **#228 Oil Pump**, **#229 Pier/rowboat**, **#230 Motorboat/port**, **MailTruck** (replace placeholder after #320).

Style: `docs/art/style-guide.md`. Units: metres. Y-up glTF. Bevel 2–4 cm. Shots: bright key + grey ground (boats: include water-plane cue optional). Tile = **2 m**.

**Vehicle axis (all):** origin ground centre between contact points; **+Z = forward** (same as bike/cart). No rider / no NPC mesh.

**Do not rebuild** P1–P4 assets. Bike stays `bike_01.glb`.

**Build order for Blender:** `oil_pump_01` → `truck_01` (mail truck) → `rowboat_01` + `motorboat_01` → `pier_01` → `port_01` (small).

Eng wires after PASS. Mail truck path must match `VehicleArt.MailTruck` = `res://art/props/truck_01.glb` (see note below).

---

## 1. `oil_pump_01` — #228 (**PRIORITY**)

| Field | Spec |
| :- | :- |
| Path | `game/art/world/oil_pump_01.glb` |
| Role | Buildable extractor; fuel for vehicles |
| Footprint | **2.0 × 2.0 m** on ground (1 tile); height **3.2 m** to top of walking beam |
| Origin | Bottom centre of base pad; +Z = “face” / pump-jack swing plane normal optional — prefer jack arm swings in **XZ** with head toward **+Z** |
| Tris | ≤ **700** |
| Materials | `mat_pump_base` `#5A5C66`; `mat_pump_metal` `#8A8E9A`; `mat_pump_accent` `#E67E22` (oil orange on beam/counterweight); `mat_pump_pad` `#5A5C66` |
| Notes | Readable pump-jack silhouette: base, upright A-frame, walking beam, horse head, stub pipe. Chunky low-poly, not photoreal derrick. Optional tiny flame-free “oil” blob mat `#2A3340` at well. |
| LOD later | `oil_pump_01_lo1` ≤ 250; `lo2` ≤ 60 |

**Acceptance:** reads as oil pump at 20 m; orange accent ≠ postal blue.

---

## 2. `truck_01` — Mail truck (**PRIORITY**, replaces placeholder)

| Field | Spec |
| :- | :- |
| Path | `game/art/props/truck_01.glb` |
| Code path | Must stay `res://art/props/truck_01.glb` (`VehicleArt.MailTruck`). Do **not** ship only as `mail_truck_01.glb` unless eng renames the const + tests. |
| Role | Mail Truck vehicle; wider/longer than bike |
| Size | Match placeholder: length **4.6 m**, width **1.85 m**, height **1.55 m** (cab roof) |
| Origin | Ground between axles; **+Z forward** (cab toward +Z, cargo rear toward −Z) |
| Tris | ≤ **1200** |
| Materials | `mat_truck_cab` `#2F3A8C` (postal blue); `mat_truck_cargo` `#ECF0F1`; `mat_truck_trim` `#F2D24A`; `mat_truck_wheel` `#2A3340`; `mat_truck_glass` `#A8D4F0` |
| Notes | Boxy USPS-adjacent step-van / box truck OK. Rear cargo door readable. Dual or single rear axle. Chunky wheels Ø ~0.7 m. No driver. |
| Placeholder colour was red — **override** to postal blue family so bike/truck/cart match. |

**Acceptance:** clearly larger than `bike_01`; postal blue + cream cargo; readable at 15 m.

---

## 3. `rowboat_01` — #229 (**PRIORITY with pier**)

| Field | Spec |
| :- | :- |
| Path | `game/art/props/rowboat_01.glb` |
| Role | Small boat (Sea kit); inventory 2×8 class |
| Size | Length **3.2 m**, beam **1.2 m**, gunwale height **0.55 m**, overall height **0.7 m** |
| Origin | Waterline centre (y=0 at water); **+Z forward** (bow +Z) |
| Tris | ≤ **400** |
| Materials | `mat_boat_hull` `#9A8468`; `mat_boat_interior` `#C4A078`; `mat_boat_trim` `#2F3A8C` (small postal stripe/band); `mat_oar` `#8B7355` (optional stowed oars) |
| Notes | Open rowboat; two thwart seats; no motor. Keel simple. |

**Acceptance:** small open boat ≠ motorboat; wood read.

---

## 4. `motorboat_01` — #230 (**PRIORITY**)

| Field | Spec |
| :- | :- |
| Path | `game/art/props/motorboat_01.glb` |
| Role | Medium boat; inventory 10×8 class |
| Size | Length **5.5 m**, beam **1.8 m**, cabin/console height **1.4 m** |
| Origin | Waterline centre; **+Z forward** |
| Tris | ≤ **900** |
| Materials | `mat_motor_hull` `#ECF0F1`; `mat_motor_stripe` `#2F3A8C`; `mat_motor_deck` `#5A5C66`; `mat_motor_outboard` `#2A3340`; `mat_motor_trim` `#F2D24A` |
| Notes | Cabin or console + windshield; outboard at −Z stern; postal stripe. Larger silhouette than rowboat. |

**Acceptance:** clearly bigger than rowboat; cream + postal blue.

---

## 5. `pier_01` — #229

| Field | Spec |
| :- | :- |
| Path | `game/art/world/pier_01.glb` |
| Role | Buildable pier / boat docking strip |
| Footprint | **8.0 × 2.0 m** (4×1 tiles); deck height **0.45 m** above ground/waterline y=0; piles down to **y = −1.2** |
| Origin | Bottom of shore-end piles at y=0 ground; long axis **+Z** toward water (shore at −Z, water end +Z) |
| Tris | ≤ **600** |
| Materials | `mat_pier_deck` `#9A8468`; `mat_pier_pile` `#6B5344`; `mat_pier_rail` `#C4A078` (optional low rail) |
| Notes | Planked deck, vertical piles, simple cleat cubes at water end. Snap-friendly rectangular footprint. |

**Acceptance:** walkable pier mass; wood family ≠ asphalt.

---

## 6. `port_01` — #230 small port

| Field | Spec |
| :- | :- |
| Path | `game/art/world/port_01.glb` |
| Role | Small port building + apron (boat unload / NPC captain hub) |
| Footprint | **6.0 × 6.0 m** (3×3 tiles); building height **3.5 m**; apron/dock lip toward +Z |
| Origin | Bottom centre of footprint; **+Z** = water / berth face |
| Tris | ≤ **1000** |
| Materials | `mat_port_wall` `#A04B3A` (PO brick family); `mat_port_roof` `#6B4E6E`; `mat_port_trim` `#F2D24A`; `mat_port_dock` `#9A8468`; `mat_port_metal` `#5A5C66` |
| Notes | Warehouse shed + short dock apron on water side. Distinct from PO (smaller, dock attached). No cranes. |

**Acceptance:** brick shed + wood dock; berth face obvious.

---

## Priority / skip

| Build first | Then | Trail / skip |
| :- | :- | :- |
| oil_pump_01, truck_01 | rowboat_01, motorboat_01, pier_01 | port_01 (same PR OK); LODs later |

**Out of scope:** semi, large boat, deep port, rail, belts, enemies, riders.

---

## Acceptance checklist (style check)

- [ ] oil_pump: jack silhouette + orange accent  
- [ ] truck_01: 4.6×1.85×1.55-ish, postal blue cab, path `truck_01.glb`  
- [ ] rowboat < motorboat in length; wood vs cream hull  
- [ ] pier: 8×2 wood deck, piles  
- [ ] port: brick + dock, +Z berth  
- [ ] All vehicle +Z forward; origins as specified  
- [ ] Under tris; hex match this doc  

When done: glTFs on paths above + shots under `/workspace/playtest-vm/blender-p5/` + report JSON. Ping Game Art Director for pass/fail.
