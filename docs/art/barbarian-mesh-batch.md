# Barbarian enemy mesh batch — locked specs (M3 / sim #345)

**Goal:** Godot-facing art for raid Barbarians so they aren’t capsules/boxes.  
**Style:** `docs/art/style-guide.md` — stylized low-poly (Satisfactory / Raft / Muck / Crab Game). No photoreal.  
**Folder convention:** `game/art/enemies/` (keep `pawns/` for friendly mail carriers only).  
**Don’t build yet** — lock only. Blender builds after Percival kicks; eng wires `ArtMesh` path after PASS.

Combat baseline (`spec/05-combat.md`): Barbarian — melee swarmer, HP 60, groups of 3–6, unlock shift 2. One mesh for v1; Mega = eng scale ×2 + tint later (no second mesh this batch).

---

## Convention

| Field | Spec |
| :- | :- |
| Units | Metres, Y-up glTF |
| Origin | Ground centre between feet |
| Axis | **+Z = forward** (face / charge direction toward PO when on approach path) |
| Bevel | 2–4 cm on prop edges; chunky limbs OK |
| Distinct from | `pawn_remote.glb` — no postal blue kit, no mail bag, bulkier torso, weapon in hand |

---

## `barbarian_01` (only kind this batch)

| Field | Spec |
| :- | :- |
| Path | `game/art/enemies/barbarian_01.glb` |
| Role | Ground melee raid enemy |
| Height | **1.75 m** to top of head (range 1.6–1.8); shoulder width **~0.55 m** |
| Stance | Standing idle/run-ready; weapon held right side; feet planted |
| Tris LO0 | ≤ **900** |
| Optional LO1 | `barbarian_01_lo1.glb` ≤ **350** (drop fingers, belt trim; keep weapon + head silhouette) |
| Optional LO2 | `barbarian_01_lo2.glb` ≤ **120** (capsule-ish body + head + weapon stick) |
| Materials | see palette below |
| Notes | Humanoid biped. Horned helm or wild hair OK. One-handed club or axe (prefer **club** for silhouette weight). Leather/fur wrap, not plate armor. Slight forward lean optional. No facial detail required. |

### Palette (hex)

| Material | Hex | Role |
| :- | :- | :- |
| `mat_barb_skin` | `#C4A882` | Skin (warmer/dirtier than pawn skin `#EBE0A8`-ish — use this exact) |
| `mat_barb_cloth` | `#6B3A2E` | Tunic / wrap |
| `mat_barb_fur` | `#8B6914` | Fur skirt / pauldrons |
| `mat_barb_leather` | `#5C4033` | Belt / straps |
| `mat_barb_metal` | `#8A8E9A` | Buckle / helm rim |
| `mat_barb_weapon` | `#5A5C66` | Club/axe head + haft dark wood `#6B5344` as `mat_barb_haft` if split |
| `mat_barb_accent` | `#E85D3A` | War paint / cloth stripe (hostile read vs postal blue) |

**Out of bounds:** postal `#2F3A8C`, mail bag, cute proportions, photoreal skin/hair.

### Silhouette checklist

- Readable at sprint (~15–20 m) and at first-person eye height  
- Wider shoulders / thicker limbs than `pawn_remote`  
- Weapon mass visible in side profile  
- Not confusable with player kit colours (no `#3884FF` family)

---

## Variants (later — not this batch)

| Future path | When |
| :- | :- |
| `barbarian_01_mega.glb` or eng ×2 scale + `mat_barb_accent` brighter | Mega spawn (5%+ chance) |
| `archer_01.glb`, `giant_01.glb`, … | Full roster after Barbarian wires |

M3 frame names **one** Barbarian kind for art now → **single** `barbarian_01.glb` + optional LODs.

---

## Suggested eng wire

```
res://art/enemies/barbarian_01.glb
```

Map from sim enemy kind Barbarian → that path (new `ArtMesh.PathForEnemy` or equivalent). LOD via existing `TryInstantiateLod` if lo1/lo2 ship.

---

## Shots / AD

Blender exports + orthographic shots after kick; AD PASS before eng wire.

## Acceptance (style check)

- [ ] Path under `enemies/`; height ~1.75 m; +Z forward; ground origin  
- [ ] Hex match table; no postal blue / mail bag  
- [ ] Distinct from `pawn_remote` at a glance  
- [ ] LO0 ≤ 900; weapon readable  
- [ ] Shot: bright key + grey ground  

Ping Game Art Director when Blender exports + shots land.
