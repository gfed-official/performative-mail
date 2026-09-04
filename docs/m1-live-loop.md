# M1 live Arcade loop

A human Host in Godot plays shift 1 on the generated Small Island. Inspect dumps and Sim-only gates stay. This run wires them into `PlaySessionMachine.Host`.

Parent: [#153](https://github.com/gfed-official/performative-mail/issues/153). Do not start M2.

## Definition of done

1. Host Hello offers `worldHash 0x821670054873680E` for seed `0x7F3A9C21`.
2. `Main.Render` on Playing binds HUD from `HudSnapshot`. `_Ready` still does not bind HUD.
3. The 40 m plane is gone. PO, streets, houses, and mailboxes come from `WorldTables`.
4. E near Intake picks up mail. E near a matching mailbox credits the wallet.
5. LAN Join still reaches Playing with two pawns.

## Units

| Unit | Issue | Landable change |
| --- | ---: | --- |
| U11 | [153](https://github.com/gfed-official/performative-mail/issues/153) | Live Arcade loop |
| U11.1 | [154](https://github.com/gfed-official/performative-mail/issues/154) | Host generates Small Island and offers worldHash |
| U11.2 | [155](https://github.com/gfed-official/performative-mail/issues/155) | ShiftClock ticks on the listen server |
| U11.3 | [156](https://github.com/gfed-official/performative-mail/issues/156) | Playing carries HudSnapshot and WorldTables |
| U11.4 | [157](https://github.com/gfed-official/performative-mail/issues/157) | Godot binds HUD from Playing |
| U11.5 | [158](https://github.com/gfed-official/performative-mail/issues/158) | Godot world stage from WorldTables |
| U11.6 | [159](https://github.com/gfed-official/performative-mail/issues/159) | Interact pickup and mailbox deliver |
| U11.7 | [160](https://github.com/gfed-official/performative-mail/issues/160) | Live inventory overlay |
| U11.8 | [161](https://github.com/gfed-official/performative-mail/issues/161) | Human Host play report |

## Out of this run

Shop UI, harvest meshes, wooden wall ghosts, bike mesh, stamp grid, 4-player quota met on shifts 3–5, house variant kit.
