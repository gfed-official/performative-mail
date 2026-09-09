# P4 mesh batch — locked specs (tickets #275 #278 #277 #286)

Style: `docs/art/style-guide.md`. Units: metres. Y-up glTF. Origin ground centre unless noted. Bevel 2–4 cm. Shots: bright key + grey ground. Do not rebuild P1/P3 assets except where this doc says **revise**.

District swatches (plate / pole `mat_district` — eng sets index 0–7):

| Index | Hex | Pattern cue (optional emboss, not required) |
| :- | :- | :- |
| 0 | `#3D7EFF` | solid |
| 1 | `#E85D3A` | diagonal |
| 2 | `#2ECC71` | dots |
| 3 | `#F1C40F` | chevron |
| 4 | `#9B59B6` | horizontal |
| 5 | `#1ABC9C` | crosshatch |
| 6 | `#E67E22` | vertical |
| 7 | `#ECF0F1` | outline — use dark edge `#2A3340` so it reads |

Street stripe on mail / poles: same hue at **70% brightness**, or fixed `mat_street_stripe` `#C4C8D0` when street id is unknown at mesh time. Prefer **named material slots** eng recolors — one mesh, not 8 files — unless noted.

**Build order:** #275 → #278 → (#277 and #286 can trail).

---

## #275 — Held-mail district address plate (**PRIORITY**)

**Goal:** Letter + packages read address by **colour** at arm’s length (district plate + street stripe). Text optional later.

**Revise in place** (keep paths; do not rename):

| Path | Size (locked) | Tris | Materials |
| :- | :- | :- | :- |
| `game/art/props/mail_letter.glb` | 0.20 × 0.02 × 0.28 m | ≤ **60** | `mat_mail_paper` `#F7F1DE`; `mat_district` (stamp + address block, default `#3D7EFF`); `mat_street_stripe` thin bar under address `#C4C8D0` |
| `game/art/props/mail_pkg_s.glb` | 0.25 cube | ≤ **80** | `mat_cardboard` `#9A8468`; `mat_district` sticker 0.08×0.08 on +Z face; `mat_street_stripe` 0.10×0.02 under sticker |
| `game/art/props/mail_pkg_m.glb` | 0.40 cube | ≤ **80** | same mats; sticker 0.12×0.12 |
| `game/art/props/mail_pkg_l.glb` | 0.60 cube | ≤ **80** | same mats; sticker 0.16×0.16 |

| Field | Spec |
| :- | :- |
| Origin | Letter: centre of mass (held attach). Pkgs: bottom centre. |
| Face | Address / sticker on **+Z** (toward camera when held). |
| Readable | District plate ≥ 25% of letter face width; sticker contrast vs cardboard. |
| Default colours | Ship with district 0 `#3D7EFF` + stripe `#C4C8D0`. Eng swaps `mat_district` Base Color per mail destination. |
| Out | No baked text glyphs. No brown stamp-only (must be district-coloured plate). |

**Acceptance:** at 1–2 m eye height, letter and each pkg show a clear colour block ≠ paper/cardboard; stripe secondary.

---

## #278 — Street / address poles (**PRIORITY 2**)

**Goal:** World poles so district + street read without mailbox Label3D alone (`spec/09`: address on street sign).

### `street_pole_01`
| Field | Spec |
| :- | :- |
| Path | `game/art/world/street_pole_01.glb` |
| Role | Corner / mid-block address pole; MultiMesh-friendly |
| Size | Post **0.12 × 0.12 × 2.4 m**; sign blade **0.55 × 0.04 × 0.28 m** at height centre ~2.0 m |
| Origin | Bottom centre of post; +Z = street-facing (blade faces street) |
| Tris | ≤ **220** |
| Materials | `mat_pole` `#5A5C66`; `mat_district` blade face `#3D7EFF` (default); `mat_street_stripe` top edge band of blade `#C4C8D0`; `mat_plate` back `#ECF0F1` |
| Notes | Chunky low-poly; slight bevel. Optional small number plate quad under blade (same `mat_plate`) — no glyphs. Eng instances per street corner and recolors `mat_district`. |

### `street_pole_cap_01` — NO separate file
Cap is part of `street_pole_01`.

**Acceptance:** silhouette reads “sign on stick” at 20 m; district colour dominates blade; distinct from mailbox blue body.

---

## #277 — LOD LO1 / LO2 for existing world props (**TRAIL**)

Distances (style guide): **LO1 past 30 m**, **LO2 past 60 m**. Separate files so Godot can swap without importer guesswork.

| LO0 (existing) | LO1 path | LO1 tris ≤ | LO1 keep | LO2 path | LO2 tris ≤ | LO2 keep |
| :- | :- | :- | :- | :- | :- | :- |
| `mailbox_01.glb` | `mailbox_01_lo1.glb` | 120 | body + flag + plate | `mailbox_01_lo2.glb` | 40 | body + flag only |
| `intake_01.glb` | `intake_01_lo1.glb` | 140 | hopper + yellow band | `intake_01_lo2.glb` | 48 | single box + band colour |
| `po_01.glb` | `po_01_lo1.glb` | 800 | massing + porch + sign bar | `po_01_lo2.glb` | 120 | box + roof slab |
| `house_a.glb` | `house_a_lo1.glb` | 160 | roof + body + door | `house_a_lo2.glb` | 48 | body + roof |
| `house_b.glb` | `house_b_lo1.glb` | 200 | roof + dormer mass | `house_b_lo2.glb` | 48 | body + roof |
| `house_c.glb` | `house_c_lo1.glb` | 220 | porch mass + roof | `house_c_lo2.glb` | 48 | body + roof |
| `crate_01.glb` | `crate_01_lo1.glb` | 60 | cube + one strap | — | — | skip LO2 |
| `cart_01.glb` | `cart_01_lo1.glb` | 120 | bed + frame + wheels | `cart_01_lo2.glb` | 40 | box + 2 wheels |
| `street_pole_01.glb` | `street_pole_01_lo1.glb` | 80 | post + blade | `street_pole_01_lo2.glb` | 24 | post stub + flat blade |

**Skip LOD:** `street_tile_01`, `street_curb_01`, `grass_tile_01`, `spawn_pad_01`, held mail (always near camera), `pawn_remote` (separate pass later).

**Rules:** same origin/axis as LO0; same primary material hex; drop window frames, rails, small bevels; mailbox **keeps flag through LO1**.

---

## #286 — Bike mesh (**TRAIL**)

| Field | Spec |
| :- | :- |
| Path | `game/art/props/bike_01.glb` |
| Role | Player / world mail bike; stylized low-poly |
| Size | Length **1.7 m**, width **0.45 m**, handle height **1.05 m**, wheel diameter **0.55 m** |
| Origin | Ground between wheels; **+Z = forward** (handlebars toward −Z / rear toward −Z wait: forward = +Z, so front wheel +Z, handle near −Z like cart) — match cart: **+Z forward, handle at −Z** |
| Tris | ≤ **600** LO0; optional `bike_01_lo1.glb` ≤ 200 later |
| Materials | `mat_bike_frame` `#2F3A8C` (postal blue); `mat_bike_seat` `#2A3340`; `mat_bike_wheel` `#2A3340`; `mat_bike_bag` `#9A8468` (rear mail bag) |
| Notes | Two wheels, diamond/step-through frame OK, rear bag required (mail read). No rider. Chunky tires. |

**Acceptance:** reads as bike + mail bag at 15 m; not a scooter; palette matches mailbox/cart blue family.

---

## Out of scope this batch

Truck / boat / belts / enemies / UI icons. No photoreal. No ASCII.

## Blender handoff

1. #275 revise four mail glTFs + shots `blender-p4/mail_*.png`  
2. #278 `street_pole_01` + shot  
3. Trail #277 LODs (mailbox → houses → po → intake → cart/crate/pole)  
4. Trail #286 bike  

Ping Game Art Director for style check when shots land. Eng re-pulls after PASS.
