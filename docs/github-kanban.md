# GitHub Projects Kanban

Arcade v1 remaining work is tracked as GitHub Issues. A Projects v2 board
groups them into Todo / In Progress / Done.

## Current backlog (created 2026-09-02)

| Issue | Task |
| --- | --- |
| [#6](https://github.com/gfed-official/performative-mail/issues/6) | M0 U3 Destinations + wallet |
| [#7](https://github.com/gfed-official/performative-mail/issues/7) | M0 U4 Loopback 30 Hz runtime |
| [#8](https://github.com/gfed-official/performative-mail/issues/8) | M0 U5 Movement prediction |
| [#9](https://github.com/gfed-official/performative-mail/issues/9) | M0 U6 Test map + mail spawn |
| [#10](https://github.com/gfed-official/performative-mail/issues/10) | M0 U7 BotClient deliver |
| [#11](https://github.com/gfed-official/performative-mail/issues/11) | M0 U8 Godot + HUD |
| [#12](https://github.com/gfed-official/performative-mail/issues/12) | M0 U9 8-client soak |
| [#13](https://github.com/gfed-official/performative-mail/issues/13) | M1 Vertical slice |
| [#14](https://github.com/gfed-official/performative-mail/issues/14) | M2 Automation |
| [#15](https://github.com/gfed-official/performative-mail/issues/15) | M3 Combat |
| [#16](https://github.com/gfed-official/performative-mail/issues/16) | M4 Rogue-lite depth |
| [#17](https://github.com/gfed-official/performative-mail/issues/17) | M5 Hardening |

Done on `main` already (not filed as open board items): M0 U0 scaffold, U1
GridContainer, U2 InventorySystem.

Close probe issue [#5](https://github.com/gfed-official/performative-mail/issues/5) if it is still open.

## Create the board (one-time, org owner)

The Cursor GitHub App cannot create organization Projects. An org owner or
admin with the `project` scope runs:

```bash
gh auth refresh -s project,read:project
./tools/setup-github-kanban.sh
```

That script:

1. Creates (or reuses) **Performative Mail — Arcade Board** under `gfed-official`
2. Links it to `gfed-official/performative-mail`
3. Adds open issues (skipping #5 by default)
4. Sets Status to **Todo** when that option exists

Open the printed project URL and confirm the Board view columns are Status:
Todo / In Progress / Done.

## After the board exists

If the Cursor App is later granted Projects write access on the org, agents can
add new issues to the board with:

```bash
gh project item-add <number> --owner gfed-official \
  --url https://github.com/gfed-official/performative-mail/issues/<n>
```
