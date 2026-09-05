# P3 prop batch — locked specs (for Blender)

Style: `docs/art/style-guide.md` (stylized low-poly). Units: metres. Origin: ground centre unless noted. Bevel: 2–4 cm on prop edges. Y-up, export glTF. Headless shots with bright key + neutral grey ground.

**P1 DONE — do not rebuild:** mailbox_01, intake_01, po_01, house_a/b/c, pawn_remote, mail_letter, mail_pkg_s/m/l.

---

## YES — build this batch

### 1. `street_curb_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/street_curb_01.glb` |
| Role | MultiMesh edge strip along street tiles; marks street vs grass |
| Footprint | **2.0 × 0.15 × 0.18 m** (X along street edge = one tile width, Z thickness into grass, Y height) |
| Origin | Bottom centre; mesh sits on y=0; eng places at tile edge |
| Tris | ≤ **80** |
| Materials | `mat_curb` `#8A8E9A` (street edge). Optional top lip 2 cm lighter `#9AA0AC` as same material or second |
| Notes | Single watertight mesh, no LODs. Axis: long axis = local +X so MultiMesh can rotate 90° for N/S edges. Soft bevel, not a razor slab. |

### 2. `street_tile_01` — YES (start-town asphalt; replaces BoxMesh streets)
| Field | Spec |
| :- | :- |
| Path | `game/art/world/street_tile_01.glb` |
| Role | MultiMesh road tile; one instance per street tile |
| Footprint | **2.0 × 0.08 × 2.0 m** (matches current `BoxMesh` size = `tileM`) |
| Origin | Bottom centre; top face at y=0.08 |
| Tris | ≤ **48** (chamfered slab OK; keep cheap) |
| Materials | `mat_asphalt` `#5A5C66`. Optional centre dashed stripe as second mat `mat_stripe` `#C4C8D0` (thin inset, can omit if tris tight) |
| Notes | Flat enough for MultiMesh. No curb baked in — curb is separate. |

### 3. `spawn_pad_01` — YES (PO spawn readability)
| Field | Spec |
| :- | :- |
| Path | `game/art/world/spawn_pad_01.glb` |
| Role | Gold pad at PO spawn tile; replaces flat yellow box |
| Footprint | **1.8 × 0.12 × 1.8 m** (0.9 × tileM) |
| Origin | Bottom centre |
| Tris | ≤ **120** |
| Materials | `mat_pad` `#C4A84A`; edge bevel `mat_pad_edge` `#A8903A` (or same mat) |
| Notes | Slight bevel / raised lip so it reads at eye height. No text. |

### 4. `grass_tile_01` — YES (optional but recommended; kills void)
| Field | Spec |
| :- | :- |
| Path | `game/art/world/grass_tile_01.glb` |
| Role | MultiMesh fill for non-street ground near start town (eng may also use a big plane; this tile keeps seams consistent) |
| Footprint | **2.0 × 0.04 × 2.0 m** |
| Origin | Bottom centre; top at y=0.04 (under street top) |
| Tris | ≤ **24** |
| Materials | `mat_grass` `#6FA86A` |
| Notes | Dead-simple slab. Skip if eng already ships a single ground plane — then mark N/A in export report. |

### 5. `crate_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/props/crate_01.glb` |
| Role | Scatter near PO porch / intake; postal clutter |
| Footprint | **0.55 × 0.55 × 0.55 m** cube-ish |
| Origin | Bottom centre |
| Tris | ≤ **200** |
| Materials | `mat_crate_wood` `#9A8468`; `mat_crate_band` `#5A5C66` (metal straps) |
| Notes | Lid seam or strap cross on top for silhouette. Stackable visually (flat top). Bevel corners. |

### 6. `cart_01` (mail hand truck / platform cart)
| Field | Spec |
| :- | :- |
| Path | `game/art/props/cart_01.glb` |
| Role | Near PO / intake; logistics silhouette |
| Footprint | **1.1 × 0.55 × 0.95 m** (L×W×H); handle height ~1.0 m |
| Origin | Ground centre under bed (between wheels); +Z = forward (handle at −Z) |
| Tris | ≤ **400** |
| Materials | `mat_cart_frame` `#2F3A8C` (postal blue); `mat_cart_bed` `#C4A078` (wood); `mat_cart_wheel` `#2A3340` |
| Notes | Two or four chunky wheels, upright handle, flat bed. Readable as “mail cart” at 15 m. No mail loaded (empty). |

---

## NO — not this batch

| Item | Why |
| :- | :- |
| Street name signs / address poles | Need district colour system wiring; later |
| Extra house variants | A/B/C locked |
| Vehicles (bike/truck) | Content later; not start-town P3 |
| Raid enemies / defenses | Out of this polish slice |
| UI meshes / icons | P2 eng Theme work, not Blender props |
| Night/raid lighting | Eng environment, not mesh |

---

## Engine wiring (WorldStage)

- Streets: `street_tile_01` MultiMesh (one instance per street tile). Stripe runs along local +Z; E/W runs yaw 90°. BoxMesh `#5A5C66` fallback if the glTF is missing.
- Curbs: `street_curb_01` MultiMesh on street edges that do not neighbor another street tile. Long axis +X; yaw so local +Z (thickness) points into grass. BoxMesh `#8A8E9A` fallback.
- Spawn: `spawn_pad_01` at `SpawnPadTile` (gold box fallback).
- Grass: **keep** `Main.AddGrassGround` island plane (`#6FA86A`, void-killer). Overlay `grass_tile_01` MultiMesh on start-town lots + PO footprint, excluding street tiles, so yard/PO seams read as 2 m tiles above the plane.
- Clutter: five instances near PO / intake — three `crate_01`, two `cart_01` (cart +Z toward intake or street). Skip if a glTF is missing.

WORLD_DUMP still walks Label3D only (PO / Mail / houses / mailboxes). MultiMesh tiles and clutter are unlabeled.

## Build order for Blender

1. `street_tile_01` + `street_curb_01` (MultiMesh pair)  
2. `spawn_pad_01`  
3. `crate_01` + `cart_01`  
4. `grass_tile_01` if eng wants tiles (confirm with Percival; default YES)

## Acceptance (style check)

- [ ] Curb reads as edge, not another asphalt slab (`#8A8E9A` ≠ `#5A5C66`)  
- [ ] Street tile tiles flush at 2 m; height ~0.08 m  
- [ ] Spawn pad gold `#C4A84A`, raised lip  
- [ ] Crate wood + strap; cart postal-blue frame + wood bed + dark wheels  
- [ ] All under tris budgets; origins correct  
- [ ] Shots: bright key, grey ground (not green wash)

When done: drop glTFs on paths above + shots under `/workspace/playtest-vm/blender-p3/` and ping Game Art Director for pass/fail.
