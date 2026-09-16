# P6 construct mesh batch — locked specs

**Goal:** Replace `ConstructStage` BoxMesh placeholders with style-guide glTFs.  
**Style:** `docs/art/style-guide.md`. Units: metres. Tile = **2 m**. Y-up glTF. Bevel 2–4 cm.  
**Axis:** **+Z = forward** (belt flow, sorter infeed face, inserter reach, wall “outside” face). Origin = **ground centre** of footprint.  
**Do not redo:** `oil_pump_01` (content id `pump`), `pier_01`, `port_01`.

Eng today: `ConstructStage` still spawns `BoxMesh` — no `ArtMesh.TryPathForConstruct` yet. Paths below are the contract for that map (content `buildings/*.json` ids → glTF).

**Build order:** `address_sorter_01` first → kit (`belt_mk1`, `inserter_01`, `splitter_01`, `merger_01`, `chest_01`, `wall_wood_01`) → trail depots / belt variants / pipes.

Shots: `/workspace/playtest-vm/blender-p6/` + report JSON.

---

## Priority 1 — Address sorter (sorter play)

### `address_sorter_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/address_sorter_01.glb` |
| Content id | `address_sorter_mk1` (footprint **2×2** tiles → **4.0 × 4.0 m**) |
| Height | **2.2 m** |
| Tris | ≤ **800** |
| Origin | Ground centre; **+Z = infeed** (mail enters from −Z toward machine, exits on side ports) |
| Materials | `mat_sorter_body` `#5A5C66`; `mat_sorter_panel` `#8A8E9A`; `mat_sorter_accent` `#F2D24A` (filter / UI cue); `mat_sorter_port` `#2F3A8C` (in/out mouths); `mat_sorter_window` `#A8D4F0` optional |
| Silhouette | Chunky sorting machine: central body, clear **infeed slot on −Z**, **four output ports** on +X/−X/+Z (or three sides + rear) as distinct openings/hoods so filter UI can associate to a face. Top panel / screen slab with yellow accent for “filter machine” read. |
| Notes | Not a plain cube. Ports must be visually distinct (recessed mouths or hood lips). Eng will rotate with building rotation. |

**Acceptance:** readable as sorter at 15 m; yellow accent; ≥1 infeed + multiple out ports obvious.

---

## Priority 2 — Core kit

### `belt_mk1`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/belt_mk1.glb` |
| Content id | `belt_mk1` (footprint **1×1** → **2.0 × 2.0 m**) |
| Height | Deck **0.35 m**; rails to **0.55 m** |
| Tris | ≤ **200** (MultiMesh / drag-line friendly) |
| Origin | Ground centre; **+Z = flow** direction |
| Materials | `mat_belt_frame` `#5A5C66`; `mat_belt_deck` `#2A3340`; `mat_belt_rail` `#8A8E9A`; `mat_belt_stripe` `#F2D24A` (center flow arrow / chevron, optional) |
| Notes | Flat conveyor segment filling the tile; two lanes suggested by center divider. Straight only (corners/ramps later). Soft bevel. |

### `inserter_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/inserter_01.glb` |
| Content id | `inserter` (1×1 → **2.0 × 2.0 m**) |
| Height | **1.4 m** (arm up) |
| Tris | ≤ **350** |
| Origin | Ground centre; **+Z = reach / grab direction** |
| Materials | `mat_inserter_base` `#5A5C66`; `mat_inserter_arm` `#8A8E9A`; `mat_inserter_claw` `#F2D24A`; `mat_inserter_joint` `#2A3340` |
| Notes | Pedestal + arm + claw pointing +Z. Readable “picks from +Z neighbor”. |

### `splitter_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/splitter_01.glb` |
| Content id | `splitter` (1×1 → **2.0 × 2.0 m**; **3 ways**) |
| Height | **0.9 m** |
| Tris | ≤ **280** |
| Origin | Ground centre; **+Z = primary infeed** |
| Materials | `mat_split_body` `#5A5C66`; `mat_split_deck` `#2A3340`; `mat_split_accent` `#3D7EFF` |
| Notes | One in (−Z), three outs (e.g. +Z / +X / −X). Y-junction or T with three mouths. |

### `merger_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/merger_01.glb` |
| Content id | `merger` (1×1 → **2.0 × 2.0 m**; **3 ways**) |
| Height | **0.9 m** |
| Tris | ≤ **280** |
| Origin | Ground centre; **+Z = primary outfeed** |
| Materials | `mat_merge_body` `#5A5C66`; `mat_merge_deck` `#2A3340`; `mat_merge_accent` `#2ECC71` |
| Notes | Three ins → one out (+Z). Mirror of splitter language; green accent so ≠ splitter blue. |

### `chest_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/chest_01.glb` |
| Content id | `chest` (1×1 → **2.0 × 2.0 m**) |
| Size | Footprint fill **~1.2 × 0.9 × 1.0 m** (W×D×H) centred on tile; not full 2 m cube |
| Tris | ≤ **250** |
| Origin | Ground centre; **+Z = lid / open face** |
| Materials | `mat_chest_wood` `#9A8468`; `mat_chest_band` `#5A5C66`; `mat_chest_latch` `#F2D24A` |
| Notes | Cratelike chest with lid seam + latch; postal storage read. |

### `wall_wood_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/wall_wood_01.glb` |
| Content id | `wall_wood` (1×1 → **2.0 × 2.0 m** footprint) |
| Size | Segment **2.0 m wide × 0.25 m thick × 2.4 m tall** |
| Tris | ≤ **120** |
| Origin | Ground centre; wall plane along **X**; **+Z = outside** face |
| Materials | `mat_wall_wood` `#9A8468`; `mat_wall_edge` `#6B5344` |
| Notes | Drag-line friendly straight segment. No posts required (optional end posts OK if ≤ tris). |

---

## Suggested eng path map (`TryPathForConstruct`)

| Building id | Path |
| :- | :- |
| `address_sorter_mk1` | `res://art/world/address_sorter_01.glb` |
| `belt_mk1` | `res://art/world/belt_mk1.glb` |
| `inserter` | `res://art/world/inserter_01.glb` |
| `splitter` | `res://art/world/splitter_01.glb` |
| `merger` | `res://art/world/merger_01.glb` |
| `chest` | `res://art/world/chest_01.glb` |
| `wall_wood` | `res://art/world/wall_wood_01.glb` |
| `pump` | already `oil_pump_01.glb` |
| pier / port | already P5 |

---

## Trail (same doc, later Blender pass — not blocking sorter)

| Path | Content id | Footprint | Tris ≤ | Notes |
| :- | :- | :- | :- | :- |
| `depot_01.glb` | `depot` | 2×2 | 600 | Warehouse box + door +Z |
| `vehicle_depot_01.glb` | `vehicle_depot` | 3×3 | 900 | Garage + parking pad |
| `belt_mk1_ramp.glb` | `belt_mk1_ramp` | 2×1 | 250 | Rise along +Z |
| `belt_mk1_elevated.glb` | `belt_mk1_elevated` | 1×1 | 220 | Raised deck ~1.5 m |
| pipe set | `pipe*` | 1×1 | 150 ea | Defer unless eng asks |

---

## Out of scope

Pipes (unless asked), turrets/gates, belt corners, LODs (add after LO0 lands), redoing oil pump / pier / port.

---

## Acceptance (style check)

- [ ] Sorter: not a cube; infeed + multi outs; yellow accent; ≤800 tris; 4×4 m class  
- [ ] Belt: flat tile segment; +Z flow  
- [ ] Inserter: arm points +Z  
- [ ] Splitter blue accent ≠ merger green  
- [ ] Chest wood + latch; wall wood segment  
- [ ] Hex + origins match this doc  

Ping Game Art Director when shots land.
