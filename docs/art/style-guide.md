# Performative Mail — Style Guide (locked)

**Medium:** stylized low-poly 3D (not photoreal, not flat 2D).  
**Camera today:** first-person eye height (play polish). Spec still lists third-person as the long-term assumption; props must read from both eye-level and a future follow cam.  
**Refs (from `spec/00-readme.md`):** Satisfactory, Raft, Muck, Crab Game — chunky silhouettes, readable materials, soft light, bold local color.  
**Tone:** friendslop surface, factory depth. Warm postal town first; logistics hardware second. Never graybox forever.

No style fork needed: this matches the shipped design assumption. Alternative (industrial grimy / photoreal) is out unless Evan asks.

---

## Three pillars

1. **Readable at a sprint.** Mailbox, intake, house number, and held mail stay readable at 15–20 m and at eye height while moving.
2. **Address is colour + shape, text is backup.** District = swatch. Street = stripe / trim. Number = high-contrast plate. Matches `spec/09-ui-ux.md`.
3. **One town, one material language.** Soft clay-like albedo, mild roughness variation, no PBR chrome spam. Props share bevel width and colour family.

---

## Palette (world)

| Role | Hex | RGB 0–1 (Godot) | Use |
| :- | :- | :- | :- |
| Sky wash | `#7EB8E8` | 0.49, 0.72, 0.91 | Horizon / clear day BG (replace flat `#73B8EB` only slightly cooler) |
| Ground / grass | `#6FA86A` | 0.44, 0.66, 0.42 | Non-street tiles (missing today — add) |
| Street asphalt | `#5A5C66` | 0.35, 0.36, 0.40 | Streets (current `#616166` → nudge cooler) |
| Street edge | `#8A8E9A` | 0.54, 0.56, 0.60 | Curb / lane edge |
| PO brick | `#A04B3A` | 0.63, 0.29, 0.23 | Post Office body (current `#8C4738`) |
| PO trim | `#F2D24A` | 0.95, 0.82, 0.29 | PO awning / badge / intake accent |
| Spawn pad | `#C4A84A` | 0.77, 0.66, 0.29 | Spawn pad (current gold slab) |
| House stucco | `#E0CFA8` | 0.88, 0.81, 0.66 | House body (current `#C7B28C` → lighter) |
| House roof | `#6B4E6E` | 0.42, 0.31, 0.43 | Roof planes (new — break box silhouette) |
| Mailbox body | `#2F3A8C` | 0.18, 0.23, 0.55 | USPS-adjacent blue (current `#2E338C`) |
| Mailbox flag | `#E85D3A` | 0.91, 0.36, 0.23 | Flag / unread cue |
| Mail paper | `#F7F1DE` | 0.97, 0.95, 0.87 | Letter / postcard albedo |
| Shadow wash | `#2A3340` @ 35% | — | Ambient occlusion tint, not black |

### District swatches (colour-blind safe set)

Use hue + pattern (stripe / dots / chevron) together. Eight slots for unlock districts:

| Index | Hex | Pattern |
| :- | :- | :- |
| 0 | `#3D7EFF` | solid |
| 1 | `#E85D3A` | diagonal stripe |
| 2 | `#2ECC71` | dots |
| 3 | `#F1C40F` | chevron |
| 4 | `#9B59B6` | horizontal stripe |
| 5 | `#1ABC9C` | crosshatch |
| 6 | `#E67E22` | vertical stripe |
| 7 | `#ECF0F1` | outline only |

Street stripe colours reuse the same set at 70% brightness for trim.

### Pawn kit colours (keep existing `PawnPalette`)

| Index | Hex | Notes |
| :- | :- | :- |
| 0 | `#3884FF` | (56,132,255) |
| 1 | `#FF6347` | tomato |
| 2 | `#2ECC71` | |
| 3 | `#F1C40F` | |
| 4 | `#9B59B6` | |
| 5 | `#1ABC9C` | |
| 6 | `#E67E22` | |
| 7 | `#ECF0F1` | light — needs dark outline on bright ground |

Do not invent new pawn hues without updating `PawnPalette` + tests.

---

## Light and environment

| Setting | Target |
| :- | :- |
| Key light | Directional, ~−50° pitch, −30° yaw (keep), **shadows on**, soft shadow blur |
| Ambient | Sky colour, energy ~0.35–0.45 (today white @ 0.5 washes everything) |
| Sky | Gradient or simple sky material; avoid flat `BGMode.Color` forever |
| Fog | Light distance fog, colour `#A8C8E0`, start ~40 m, end ~120 m |
| Exposure | Slight warm grade; no heavy bloom |

Shadow rule: one sun, soft contact shadows under props. No second competing key.

---

## Silhouette and scale

Physical units from spec: **1 unit = 1 m**, building tile = **2 m**.

| Asset | Target size (approx) | Notes |
| :- | :- | :- |
| Pawn body | capsule → stylized humanoid ~1.7 m | Hide local mesh; remote needs hat/bag cue |
| Mailbox | 0.35 × 1.2 × 0.4 m | Flag on street-facing side; number plate readable |
| House | lot × 0.7 footprint, height 2.4–3.2 m | Roof break required; door faces street |
| Post Office | footprint from tables, height ~4–5 m | Distinct roof + porch; biggest silhouette in start town |
| Mail intake | 1.0 × 1.2 × 1.0 m | Yellow accent band; hopper mouth faces approach |
| Letter (held) | ~0.2 × 0.28 × 0.02 m | Address plate on face |
| Package S/M/L | 0.25 / 0.4 / 0.6 m cube-ish | Brown cardboard + district sticker |

Bevel: 2–4 cm chamfer on prop edges. No razor-sharp boxes.

LOD: LO0 full, LO1 drop small trim past 30 m, LO2 box+roof past 60 m. Mailbox keeps flag + plate through LO1.

---

## Materials

| Class | Albedo | Roughness | Notes |
| :- | :- | :- | :- |
| Stucco / clay | palette house / PO | 0.75–0.85 | Soft, matte |
| Asphalt | street | 0.9 | Slight value noise OK |
| Painted metal | mailbox | 0.45–0.55 | Soft specular, not chrome |
| Paper / cardboard | mail | 0.8 | Flat |
| Glass (PO) | `#A8D4F0` @ 40% | 0.15 | Optional later |

No normal-map noise that fights readability. Prefer vertex colour / trim meshes over busy textures.

---

## Out of bounds

- Photoreal skin, photoreal wood grain, filmic dirt overlays
- Neon cyber / vaporwave unless a Postage Stamp modifier asks for it
- Pure Magenta / checker placeholders in any Playing path
- Default Godot capsule as the long-term remote pawn
- Flat unshadowed DirectionalLight forever
- UI that looks like DebugMenu (dark StyleBoxFlat) for lobby / pause / HUD — debug may stay utilitarian

---

## Naming / paths (for pipeline)

Suggested under `game/art/` (create when assets land):

```
game/art/
  world/   po_*.glb  house_*.glb  street_*.glb  mailbox_*.glb  intake_*.glb
  props/   mail_letter.glb  mail_pkg_{s,m,l}.glb
  pawns/   pawn_remote.glb  pawn_kit_{0-7}.tres
  mats/    mat_*.tres
  ui/      theme_play.tres  theme_lobby.tres  icons/
```

Godot import: meshes as scenes; materials as `.tres`; keep albedo hex comments in material resource names.

Export: Y-up, metres, origin at ground centre (buildings) or ground centre under post (mailbox).
