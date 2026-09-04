# M1 vertical slice frame

## Definition of done

Every M1 acceptance criterion in `spec/12-milestones.md` is falsifiable and must pass on the real artifact:

1. Same seed and settings produce a bit-identical `worldHash` on Windows and Linux clients.
2. A solo tester can win shift 1 and fail shift 2 by hand delivery alone. With a bike and quick play, shift 2 is winnable (BalanceSim plus one human confirmation).
3. A 4-player session completes a 5-shift run (no raids) in under 32 minutes with quota met each shift, using bikes and hand sorting.
4. Join state is ≤ 200 KB after 3 shifts. A player who joins in Prep of shift 3 sees the same world and containers.
5. Generation is ≤ 3 s on a mid-range laptop. 100 random seeds pass validation without manual intervention (reroll rate ≤ 5%).

This run cuts those gates into landable child issues. The current landable unit is U10.1 (issue 107): solo shift 1 win / shift 2 fail by hand delivery. Do not start U10.2+.

## Scope

Touch chapter 11 §4 and chapter 07 `tools/BalanceSim`. Reuse `QuotaBudget` and `balance.json`. Keep `HudBoot.Placeholder` inspect-only, HUD `mouse_filter` Ignore, and Host/Join clickable. Do not start M2–M5.

U10.1 records solo hand-only earnings against live quota for shift 1 and shift 2. No bike agent, four-player run, join-state size, or Windows hash.

## Rigor

High for the acceptance predicate. Recorded earnings must come from the chapter 11 hand rates and live `QuotaBudget`, not a hardcoded pass. Generation determinism stays a one-way door from U1.

## Blockers found while grounding

| Blocker | Impact | Mitigation |
| --- | --- | --- |
| CI runs Ubuntu only | Windows vs Linux `worldHash` is an M1 gate | U1.1 pins a golden hash from integer math on Linux. U10.4 adds a Windows runner |
| No Godot binary on the agent host | SeedViewer and lobby screens cannot be live-checked here | Keep U1.1 in Sim + xUnit. Godot tools wait for U1.5 / U9 |
| Authored M0 map is the live atlas | Generation must not replace `WorldAtlasLoader` in this unit | New `WorldTables` + `WorldHash` sit beside the authored atlas |

## Workflow (Phase B)

Riskiest unknown first: cross-platform generation determinism. Smallest landable first: U1.1.

| Unit | Landable change | Verify |
| --- | --- | --- |
| U1 | Small Island generation pipeline and `worldHash` | Golden hash, 100-seed validation, client regen, ≤ 3 s |
| U2 | Content pipeline (JSON defs, validator, hot reload) | ContentValidator fails bad refs in CI |
| U3 | Run state machine, quota, complaint, death bag | Phase transitions and quota formula tests |
| U4 | Shop and bike | Buy request; bike prediction path |
| U5 | StatSheet and ~12 perks | Draft offer + modifier resolution |
| U6 | Lobby, join-in-progress, disconnect grace | Join in Prep sees same `worldHash` |
| U7 | Results and MetaProfile (XP only) | XP formula; profile file round-trip |
| U8 | Resource nodes and hand-built wall/chest | Harvest yield; place wall and chest |
| U9 | Lobby, HUD additions, payday, draft, results | Live Control text on the bound snapshot |
| U10 | M1 acceptance gates | Criteria 1–5 on the real artifact |

### U1 children (generation)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U1.1 | Small Island generate skeleton + `worldHash` for a fixed seed | Integer-quantised golden hash; same hash twice | M0 on main |
| U1.2 | Fixed-point OpenSimplex2 heightmap, coastline, falloff | Sea level 0; buildable-area reroll bound | U1.1 |
| U1.3 | Town, streets, lots, addresses, districts, PO | Unique addresses; district 1 has PO + 8–12 houses | U1.2 |
| U1.4 | Roads, resources, spawn edges, 100-seed validation | ≤ 5% reroll; resource minimums | U1.3 |
| U1.5 | Client regen, hash mismatch, SeedViewer, ≤ 3 s | Client hash matches server; mismatch disconnects | U1.4 |

### U2 children (content)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U2.1 | Archetype, streets, balance JSON + validator rules | Bad archetype fails CI | U1.1 types only |
| U2.2 | Item, mail, building, recipe, shop defs | All refs resolve | U2.1 |
| U2.3 | Perk, stamp, unlock defs and closed Stat / RuleFlag enums | Unknown stat fails validator | U2.1 |
| U2.4 | Editor hot reload | Disk edit reloads Defs through `ContentFiles.Load` | U2.2, U2.3 |

### U3 children (run)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U3.1 | `RunState` phases and legal transitions | Illegal edges rejected | M0 tick |
| U3.2 | Shift clock and Ready early-exit | Prep ends on all Ready | U3.1 |
| U3.3 | Quota and spawn budget formulas | `playerScale` 1/2/4/8 samples | U3.1 |
| U3.4 | Complaint, deadlines, mail mix | Late pays half; complaint +5 letter | U3.3, M0 mail |
| U3.5 | Death, respawn, death bag | Inventory drops; 10 s respawn | U3.1, M0 inventory |

### U4 children (shop, bike)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U4.1 | Shop offers and buy request | Wallet debit; once-per-run | U3.1, U2.2 |
| U4.2 | Bike vehicle and driver prediction | Prediction uses the bike path | U4.1, M0 movement |

### U5 children (perks)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U5.1 | StatSheet and modifier resolution | `base × Π(mul) + Σ(add)` | U2.3 |
| U5.2 | Twelve Arcade perks and draft offer | Shared 3-card offer from perks stream | U5.1, U3.1 |
| U5.3 | Prerequisites, exclusions, team vs personal | Team perk greys out after one pick | U5.2 |

### U6 children (lobby)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U6.1 | Lobby `RunSettings` (seed, archetype, kit) | Settings round-trip on Hello/join | U2.1 |
| U6.2 | Join-in-progress in Prep with `worldHash` | Joiner hash matches host | U6.1, U1.1, U3.1 |
| U6.3 | Disconnect grace | 120 s then inventory drop | U6.2, U3.5 |

### U7 children (results)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U7.1 | Results payload and Postal Rank XP | `100 × shifts + 50 × victory + 5 × deliveries` | U3.1 |
| U7.2 | MetaProfile persist (XP only) | Write and reload `profile.json` | U7.1 |

### U8 children (nodes, building)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U8.1 | Harvestable resource nodes | Yield and HP from chapter 02 table | U1.4, U2.2 |
| U8.2 | Wooden Wall and Chest placement | Recipe consume; construct registry | U8.1, U2.2 |

### U9 children (UI)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U9.1 | Lobby screen | Bound seed, archetype, kit, ready | U6.1, M0 Godot |
| U9.2 | HUD shift, quota, complaint | Live Control text equals `HudFrame` | U3.2, U3.3, U3.4, M0 HUD |
| U9.3 | Payday, draft, results overlays | Bound payload strings visible | U3.1, U5.2, U7.1 |

### U10 children (gates)

| Unit | Landable change | Verify | Depends on |
| --- | --- | --- | --- |
| U10.1 | Solo shift 1 win / shift 2 fail | BalanceSim + recorded hand-delivery path | U3–U5, U8 |
| U10.2 | Four-player five-shift bot run | < 32 min; quota met each shift | U10.1, U4.2 |
| U10.3 | Join state size after 3 shifts | ≤ 200 KB; world and containers match | U6.2, U1.5 |
| U10.4 | Windows and Linux `worldHash` CI | Same golden hash on both runners | U1.1 (full after U1.5) |

Architect arena runs before U1.2 (OpenSimplex2 and coastline are a one-way door) and before U3.1 (run phases). U1.1 shape is already concrete in chapter 02 §2 (PCG32, int16 cm, 0.5 m lattice, 64-bit FNV), so arena is skipped for the skeleton. U7.2 shape is already concrete in chapter 08 §3.8, so arena is skipped for persist. U8.1 shape is already concrete in chapter 02 §3.9, so arena is skipped for harvest. U8.2 shape is already concrete in chapter 04 §1.2 and chapter 07 `Constructs.TryPlace`, so arena is skipped for placement. U9.1 shape is already concrete in chapter 09 §2.2 and the HUD bind path, so arena is skipped for the lobby screen. U9.2 shape is already concrete in chapter 09 §2.4 and the same bind path, so arena is skipped for HUD quota and complaint. U9.3 shape is already concrete in chapter 09 §2.9–2.11 and the same bind path, so arena is skipped for the overlays. U10.1 shape is already concrete in chapter 11 §4 (hand agent rates and the two viability cells), so arena is skipped for BalanceSim.

## GitHub issues

Parent: [#13](https://github.com/gfed-official/performative-mail/issues/13).

| Unit | Issue |
| --- | ---: |
| U1 | [#68](https://github.com/gfed-official/performative-mail/issues/68) |
| U1.1 | [#78](https://github.com/gfed-official/performative-mail/issues/78) |
| U1.2 | [#79](https://github.com/gfed-official/performative-mail/issues/79) |
| U1.3 | [#80](https://github.com/gfed-official/performative-mail/issues/80) |
| U1.4 | [#81](https://github.com/gfed-official/performative-mail/issues/81) |
| U1.5 | [#82](https://github.com/gfed-official/performative-mail/issues/82) |
| U2 | [#69](https://github.com/gfed-official/performative-mail/issues/69) |
| U2.1 | [#83](https://github.com/gfed-official/performative-mail/issues/83) |
| U2.2 | [#84](https://github.com/gfed-official/performative-mail/issues/84) |
| U2.3 | [#85](https://github.com/gfed-official/performative-mail/issues/85) |
| U2.4 | [#86](https://github.com/gfed-official/performative-mail/issues/86) |
| U3 | [#70](https://github.com/gfed-official/performative-mail/issues/70) |
| U3.1 | [#87](https://github.com/gfed-official/performative-mail/issues/87) |
| U3.2 | [#88](https://github.com/gfed-official/performative-mail/issues/88) |
| U3.3 | [#89](https://github.com/gfed-official/performative-mail/issues/89) |
| U3.4 | [#90](https://github.com/gfed-official/performative-mail/issues/90) |
| U3.5 | [#91](https://github.com/gfed-official/performative-mail/issues/91) |
| U4 | [#71](https://github.com/gfed-official/performative-mail/issues/71) |
| U4.1 | [#92](https://github.com/gfed-official/performative-mail/issues/92) |
| U4.2 | [#93](https://github.com/gfed-official/performative-mail/issues/93) |
| U5 | [#72](https://github.com/gfed-official/performative-mail/issues/72) |
| U5.1 | [#94](https://github.com/gfed-official/performative-mail/issues/94) |
| U5.2 | [#95](https://github.com/gfed-official/performative-mail/issues/95) |
| U5.3 | [#96](https://github.com/gfed-official/performative-mail/issues/96) |
| U6 | [#73](https://github.com/gfed-official/performative-mail/issues/73) |
| U6.1 | [#97](https://github.com/gfed-official/performative-mail/issues/97) |
| U6.2 | [#98](https://github.com/gfed-official/performative-mail/issues/98) |
| U6.3 | [#99](https://github.com/gfed-official/performative-mail/issues/99) |
| U7 | [#74](https://github.com/gfed-official/performative-mail/issues/74) |
| U7.1 | [#100](https://github.com/gfed-official/performative-mail/issues/100) |
| U7.2 | [#101](https://github.com/gfed-official/performative-mail/issues/101) |
| U8 | [#75](https://github.com/gfed-official/performative-mail/issues/75) |
| U8.1 | [#102](https://github.com/gfed-official/performative-mail/issues/102) |
| U8.2 | [#103](https://github.com/gfed-official/performative-mail/issues/103) |
| U9 | [#76](https://github.com/gfed-official/performative-mail/issues/76) |
| U9.1 | [#104](https://github.com/gfed-official/performative-mail/issues/104) |
| U9.2 | [#105](https://github.com/gfed-official/performative-mail/issues/105) |
| U9.3 | [#106](https://github.com/gfed-official/performative-mail/issues/106) |
| U10 | [#77](https://github.com/gfed-official/performative-mail/issues/77) |
| U10.1 | [#107](https://github.com/gfed-official/performative-mail/issues/107) |
| U10.2 | [#108](https://github.com/gfed-official/performative-mail/issues/108) |
| U10.3 | [#109](https://github.com/gfed-official/performative-mail/issues/109) |
| U10.4 | [#110](https://github.com/gfed-official/performative-mail/issues/110) |

## Playbook

`figure-it-out` owns the run. Each unit uses Feature discipline inside the loop (named data shape, delegated code, real-artifact verify, small commits). Decision trail: `docs/m1-decisions.tsv`.
