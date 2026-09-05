# Performative Mail — Asset Change List

Locked against `docs/art/style-guide.md`. Visual / scene only. No Sim, net, or content-table redesign.

One job per PR. Do not bundle later P-tiers into an earlier slice.

---

## Non-goals

- Photoreal skin, wood grain, filmic dirt, PBR chrome
- Neon cyber / vaporwave unless a Postage Stamp asks for it
- Magenta / checker placeholders on any Playing path
- Replacing the local first-person hide-mesh rule
- New pawn hues (must update `PawnPalette` + tests)
- DebugMenu restyle (utilitarian is fine)
- LOD pipeline, `.glb` import tree, or `game/art/` until a mesh actually lands
- WorldStage dump / Control inspect path changes
- P0.2 / P0.3 / P0.4 / P1 work inside the P0.1 PR

---

## P0 — playable town reads as a town

Acceptance for the P0 set: sprint through spawn town and name PO, intake, a house number, and a mailbox without opening debug.

### P0.1 Lighting + sky + ground

**Where:** `game/Main.cs` `BuildWorld` (tiny helper OK).

**Do:**

1. `ShadowEnabled = true` on the DirectionalLight. Keep rotation (−50, −30, 0). Soft shadow blur. One sun, no second key.
2. Ambient sky-tinted `#A8C8E0` at energy **0.4** (not white @ 0.5).
3. `Sky` + `ProceduralSkyMaterial` (top `#7EB8E8`, horizon `#C5DCF0`, ground `#6FA86A`) **and** a large grass slab at walking y=0, colour `#6FA86A`, covering Small Island / spawn town. Prefer both if cheap. Do not bring back the retired 40 m `PlaneMesh` placeholder — use a box slab parented to Main, not `WorldStage`.
4. Depth fog `#A8C8E0`, begin 40 m, end 120 m.

**Accept:** soft contact shadows under PO / houses / boxes; ground is grass, not void. World dumps and Control inspect paths unchanged.

**Out:** street / PO / house / mailbox mesh or colour retunes (those are P0.2–P0.4).

### P0.2 Post Office silhouette

**Where:** `game/WorldStage.cs` PO + spawn pad only.

**Do:**

- Body `#A04B3A` (from current `#8C4738`).
- Trim / awning / badge `#F2D24A`.
- Height ~4–5 m. Distinct roof + porch. Biggest silhouette in start town.
- Spawn pad `#C4A84A` (keep the gold slab role).
- Door / porch faces the street.

**Accept:** PO reads as the landmark from 20 m, not a brick box the same height as houses.

**Out:** intake hopper (P0.4), glass, authored `.glb`.

### P0.3 Houses + roofs

**Where:** `game/WorldStage.cs` house boxes only.

**Do:**

- Stucco `#E0CFA8` (from current `#C7B28C` → lighter).
- Roof planes `#6B4E6E` that break the box silhouette.
- Footprint stays lot × 0.7. Height 2.4–3.2 m.
- Door faces street. Address label stays readable at eye height and 15–20 m.

**Accept:** a row of houses is a street of roofs, not a crate pile.

**Out:** district swatches / street stripes on the facade (P1).

### P0.4 Mailbox + intake

**Where:** `game/WorldStage.cs` mailbox + mail intake only.

**Do:**

- Mailbox body `#2F3A8C`, flag `#E85D3A` on the street-facing side.
- Size ~0.35 × 1.2 × 0.4 m. High-contrast number plate; keep Label3D dump text.
- Intake ~1.0 × 1.2 × 1.0 m, yellow accent band (`#F2D24A`), hopper mouth faces approach.

**Accept:** mailbox and intake stay readable at a sprint; flag is the unread cue.

**Out:** held letter / package meshes (P1).

---

## P1 — address language and play props (summary)

Do not start these in a P0 PR.

- Street asphalt `#5A5C66` (from `#616166` → cooler) plus curb / lane edge `#8A8E9A`.
- District swatches + patterns (hue and stripe / dots / chevron together). Street trim at 70% brightness of the same set.
- Held letter (`#F7F1DE`, address plate on face) and package S/M/L (cardboard + district sticker).
- Remote pawn: hat / bag cue. Keep `PawnPalette`. Hide local mesh stays.
- Play HUD / lobby / pause theme that is not DebugMenu `StyleBoxFlat`. Debug may stay utilitarian.
- Soft clay materials (roughness bands from the style guide). No chrome spam.
- Optional later: PO glass `#A8D4F0` @ 40%.

---

## Suggested engineer order

1. **P0.1** lighting + sky + grass — unblocks judging every other mesh.
2. **P0.2** Post Office + spawn pad — biggest landmark.
3. **P0.3** houses + roofs — town silhouette.
4. **P0.4** mailbox + intake — readable at a sprint.
5. **P1** streets, district language, held mail, remote pawn kit, play UI theme.

Warm postal town first; logistics hardware second. Ship small.
