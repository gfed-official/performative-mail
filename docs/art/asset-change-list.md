# Performative Mail — Prioritized art / UI change list

Audience: **PM Engineer** (`20cf5c72-1c33-4b64-be97-82a82b67c25d`).  
Source of truth: [`style-guide.md`](style-guide.md).  
Constraint: visual / scene / Theme work only — no Sim/net redesign. Game Art Director does not ship PRs or model meshes; engineer implements with primitives → glTF as assets arrive.

Current baseline (facts from code):
- `game/WorldStage.cs` — all world = `BoxMesh` + flat `StandardMaterial3D`
- `game/PawnStage.cs` — remote pawn = `CapsuleMesh`
- `game/Main.cs` `BuildWorld` — DirectionalLight **shadows off**, flat sky `#73B8EB`, white ambient 0.5
- `assets/` empty; no Theme resources for play UI
- Pause / Debug = dark `StyleBoxFlat` (~`#1F2129`)

---

## P0 — Stop looking like a prototype (1–2 days of engineer time)

Concrete enough to land without new meshes.

### P0.1 Lighting + sky
**Where:** `Main.cs` `BuildWorld`  
**Do:**
1. Enable `ShadowEnabled = true` on the DirectionalLight; keep rotation (−50, −30, 0).
2. Set ambient to sky-tinted colour `#A8C8E0` at energy **0.4** (not pure white).
3. Replace flat `BackgroundColor` with a simple `Sky` + `ProceduralSkyMaterial` (top `#7EB8E8`, horizon `#C5DCF0`, ground `#6FA86A`) **or** keep colour BG but add a large ground plane mesh under the world at y=0, colour grass `#6FA86A`, size covering spawn town.
4. Optional: fog colour `#A8C8E0`, begin 40, end 120.

**Done when:** Playing shot has soft shadows under boxes and a ground that is not void.

### P0.2 World colour pass (still boxes)
**Where:** `WorldStage.cs` colour literals  
**Replace with locked hex (convert to 0–1 floats):**

| Prop | From | To |
| :- | :- | :- |
| Post Office | (0.55,0.28,0.22) | `#A04B3A` |
| Spawn pad | (0.72,0.62,0.28) | `#C4A84A` |
| Mail intake | (0.95,0.82,0.2) | `#F2D24A` |
| Streets | (0.38,0.38,0.4) | `#5A5C66` |
| Houses | (0.78,0.7,0.55) | `#E0CFA8` |
| Mailboxes | (0.18,0.2,0.55) | `#2F3A8C` |

**Also:** add a second box on each house as roof: size ~90% of footprint XZ, height 0.55 m, colour `#6B4E6E`, stacked on body. Breaks “Minecraft dirt block” read.

**Also:** mailbox — add thin flag BoxMesh (0.02 × 0.12 × 0.22) in `#E85D3A` on the +street side.

**Done when:** PO / house / mailbox / intake read as different roles at a glance in a screenshot.

### P0.3 Label3D readability
**Where:** `WorldStage` / `PawnStage` Label3D  
**Do:** keep billboard; set `OutlineSize` ≥ 8; modulate white; consider `PixelSize` so house numbers stay ~readable at 15 m. Prefer a dark plate Quad behind the number later (P1).

### P0.4 Play Theme seed (HUD + pause, not debug)
**Where:** new `game/art/ui/theme_play.tres` (or build StyleBoxes in code once) + `PauseMenu.cs`, HUD scene if present  
**Do:**
- Panel BG `#1A2433` @ 92% opacity, corner radius 8, border `#3D5A80` 2 px
- Primary button fill `#3D7EFF`, hover `#5A93FF`, text white
- Danger (Leave confirm) `#E85D3A`
- Body text `#ECF0F1`, muted `#9AA4B2`
- Keep **DebugMenu** on its own utilitarian style (allowed)

**Done when:** Esc pause no longer twins F3 debug chrome; HUD labels use theme font colour, not default gray-on-gray.

---

## P1 — Silhouette upgrades (primitives or first glTFs)

### P1.1 Mailbox kit
- Body, door, flag, number plate (Label3D or textured quad)
- Export path: `game/art/world/mailbox_01.glb`
- Spawn from `SpawnMailboxes` instead of single box
- Poly budget: ≤ 400 tris LO0

### P1.2 Mail intake
- Hopper + yellow band + “Mail” badge
- Path: `game/art/world/intake_01.glb`
- Poly ≤ 500 tris

### P1.3 Post Office blockout → mesh
- Porch, double door, flat roof with raised sign bar
- Path: `game/art/world/po_01.glb`
- Scale to `po.SizeTiles` × tileM; Y scale fixed ~4.5 m
- Poly ≤ 2k tris LO0

### P1.4 House variants (3)
- `house_a/b/c.glb` — stucco + roof + door facing street (`toward` vector already computed)
- Rotate instance to face street
- Poly ≤ 800 tris each

### P1.5 Remote pawn
- Replace capsule with simple humanoid + bag + kit colour material slot
- Path: `game/art/pawns/pawn_remote.glb`
- Kit colour = existing `PawnPalette.Rgb` on vest/hat material
- Poly ≤ 1.5k tris

### P1.6 Held mail mesh
- Letter + 3 package sizes; address colour plate uses district swatch
- Attach to camera/local hand when hotbar has mail (engineer wires attachment; art supplies meshes)

---

## P2 — UI chrome to “shipped co-op”

Align with `spec/09-ui-ux.md` without building every widget yet.

| Surface | Priority work |
| :- | :- |
| Lobby | Card layout: dark panel + accent; Host/Join as primary buttons; seed/archetype as cards not raw labels |
| HUD | Compact top bar (shift / phase / timer / quota / wallet); timer amber `#FFBF33` / red `#E53333` (already in `Hud.cs`); quota green `#40D959` when met |
| Inventory overlay | Slot `#292E38`, selected `#3D7EFF` border, dim overlay keep ~0.35 alpha |
| Payday / Draft / Results | Reuse `theme_play`; rarity frames later |
| Debug | Untouched utilitarian |

District colours on mail cells / compass: use style-guide district table + pattern, not pawn palette.

---

## P3 — Environment depth

- Street curb strip (MultiMesh edge)
- Simple prop scatter near PO (crate, cart) — optional
- Night / raid lighting grade (cooler key, red compass already planned in UX spec)
- LOD swaps once mesh count rises

---

## Explicit non-goals (this pass)

- Photoreal materials
- Slicing sprite sheets / 2D pixel audits
- Rewriting Sim, netcode, or ContentValidator
- Scoring playtest
- Replacing debug UI with fancy chrome

---

## Suggested engineer order

1. P0.1 lighting + ground  
2. P0.2 colour + roof + mailbox flag  
3. P0.4 play theme on pause + HUD  
4. P1.1 mailbox + P1.2 intake (highest interactable readability)  
5. P1.3 PO + P1.4 houses  
6. P1.5 pawn + P1.6 held mail  
7. P2 lobby/HUD polish  
8. P3 environment

## Acceptance screenshot checklist

- [ ] Soft shadows visible under PO and pawn  
- [ ] Grass or ground plane, not infinite void  
- [ ] PO brick ≠ house stucco ≠ mailbox blue ≠ intake yellow  
- [ ] House has a roof plane  
- [ ] Mailbox has a red flag  
- [ ] Pause menu theme ≠ DebugMenu theme  
- [ ] Labels still dumpable for CI (`WORLD_DUMP` / Control paths unchanged)
