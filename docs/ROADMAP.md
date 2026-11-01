# Delivery stages

## Working prototype

- [x] Independent core, JSON content and checks.
- [x] HW1-menu-inspired configurable palette, without original art/fonts.
- [x] High-level menu structure, cube battlefield and local controls.
- [x] Dedicated interactive tutorial and right-drag orbit camera with zoom/reset.
- [x] Battlefield names, object inspection, tactical class symbols and player identity settings.
- [x] Hold-Alt battlefield names, readable unit numbers and a saved/synchronized bottom event log.
- [x] Green legal movement hexes and red targets on selection, sharing validation with actual orders.
- [x] Ownership footprints, reserved NPC/neutral gray, synchronized PvP appearance and local faction/kiith badges.
- [x] Seeded skirmish generation and configurable force/object counts.
- [x] Mid-battle saves and example campaign transitions.
- [x] Basic AI, hotseat, direct PvP and shared-fleet co-op.
- [x] Host-authoritative commands and matching-mod handshake.
- [x] Separate read-only ship importer development module, outside active UI.

## Rules fidelity

- [x] Separate basic-card practice mode, private views and deterministic whole-battle replay checks.
- [x] First two special orders, preassigned actors, skippable action sequences, mid-order saves, UI guide and network regression scenarios.
- [x] Interrupt Attack, per-hex movement pauses, response before attack dice, per-round reaction limits and a repeatable lesson.
- [ ] Remaining special orders, full initiative options, leader budgets and interrupts (see TESTING.md for current limits).

- [x] Freeze US Digital + Errata 1.7 reference priority and record timing interpretations in RULES_COVERAGE.md.
- [x] Leader/faction budgets, multi-card bids, cleanup, post-roll decisions and repair; published deck/card data still incomplete.
- [ ] Extend saved response windows to other interrupts, terrain, CAP and defensive fire.
- [x] Ship classes, proper turning/arcs, boarding, ramming/collision, crew/leader transfers, hulks and defensive fire.
- [x] CAP entry/exit, off-board targeting, movement with host, simultaneous interception and return after host destruction.
- [ ] Terrain attacks/defense, bays, reinforcements, harvesting and production.
- [ ] Scenario objectives, medals, heroes/upgrades and campaign purchases.
- [ ] Actual book campaigns as separate data packs after card coverage is complete.

## Content and multiplayer

- [ ] Generator fleet selectors, object density/types and map presets exposed in UI.
- [x] Per-player private card views, with authoritative deck/RNG retained on the host.
- [ ] Reconnection UX, stable player identities and optional subfleet ownership in co-op.
- [ ] Relay/invitation rooms for internet play without manual reachable hosting.
- [ ] More capable objective-aware AI.
- [ ] Mod packaging, asset validation, content-hash distinction for cosmetic mods.
- [ ] Release exports and distributable desktop packages.

## Deferred art import

- [x] Built-in emblem importer and source selection.
- [ ] Built-in ship/background importer UI and ownership/source selection.
- [ ] HWRM patch archive precedence and more HOD variants.
- [ ] Joint transforms, material mapping, team colors and effect reconstruction.
- [ ] HWRM background scenes and their textures through the same local-import workflow.

Per the latest scope decision, all active rendering stays as colored cubes until the game flow is useful.
