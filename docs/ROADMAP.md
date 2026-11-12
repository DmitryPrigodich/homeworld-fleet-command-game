# Delivery stages

## Current priority: mechanics before scenario content

Finish the PC game in this order: shared mechanics, campaign systems/content, then map, scenario and campaign editors. Scenario-specific content and editors should consume the shared mechanisms. The next mechanics work is remaining order/interrupt effects, neutral objective interactions and the expansion systems documented below. Do not treat all of this as blocked by missing photographs: implement sufficiently specified book rules, and use exact card faces for card-specific timing, conditions and profiles. See [CARD_INPUTS.md](CARD_INPUTS.md) for the card audit and minimal photo list.

Keep future native tablet and browser clients possible through plain rule/data/command boundaries. No engine migration, tablet export or web client is part of the current implementation stage. Touch interaction, platform storage/import and browser transport belong to client adapters. Ship editing/addition UX is deferred for a separate discussion; existing data mods remain supported.

## Working prototype

- [x] Independent core, JSON content and checks.
- [x] HW1-menu-inspired configurable palette, without original art/fonts.
- [x] English source/default language, EN/RU catalogs, live wording reloads, persisted choice and local/mod language packs with English fallback.
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
- [x] Data-defined command card types, action lines, explicit movement restrictions and per-ruleset decks; custom deck save/replay, UI and network checks.
- [ ] Remaining special orders, full initiative options, leader budgets and interrupts (see TESTING.md for current limits).

- [x] Freeze US Digital + Errata 1.7 reference priority and record timing interpretations in RULES_COVERAGE.md.
- [x] Leader/faction budgets, multi-card bids, cleanup, post-roll decisions and repair; published deck/card data still incomplete.
- [x] Extend saved response windows to terrain, CAP and defensive fire.
- [ ] Remaining interrupt types once exact card text is available.
- [x] Ship classes, proper turning/arcs, boarding, ramming/collision, crew/leader transfers, hulks and defensive fire.
- [x] Two-hex capital hulls: bow/stern travel budgets, pivots, footprint geometry/rendering, hazards and rear-shot immunity.
- [x] CAP entry/exit, off-board targeting, movement with host, simultaneous interception and return after host destruction.
- [x] Terrain activation/entry attacks, owned/neutral mines, reductions, CAP/defense and resumed paths.
- [x] Hidden asteroid bags, private Survey, free-move Harvest, cargo/loss and bidirectional resource transfers.
- [x] Bays, saved embark paths, reserves, Reinforce placement and factory production from cargo/reserve funds.
- [x] Free-form fleet editor: budgets, leaders, colored tabs, assigned free units, reserve/bay placement, personal rosters and saved/networked preparation.
- [ ] Exact published tab/card profiles, reserve exceptions and official scenario setup.
- [x] Data-defined capture/recapture, destruction and harvest medals, immediate/round-limit results and actual campaign medal history.
- [x] Data-defined end-order/round/end-battle events, carried markers, delivery, optional escape and scheduled reserve reinforcements.
- [x] First Contact: printed map/profile setup and seven-round scan/delivery/destruction/defense objectives, with training command deck clearly identified.
- [ ] Remaining base scenarios, escort/artifact interactions, published scenario exceptions, heroes/upgrades and campaign purchases.
- [ ] Actual book campaigns as separate data packs after card coverage is complete.

## Content and multiplayer

- [x] Prepared-skirmish fleet selectors, dimensions, object count, seed and background color exposed in UI.
- [ ] Object type/density presets and ready-room roster negotiation.
- [x] Per-player private card views, with authoritative deck/RNG retained on the host.
- [ ] Reconnection UX, stable player identities and optional subfleet ownership in co-op.
- [ ] Relay/invitation rooms for internet play without manual reachable hosting.
- [ ] More capable objective-aware AI.
- [ ] Mod packaging, asset validation, content-hash distinction for cosmetic mods.
- [ ] Release exports and distributable desktop packages.

## Editors after shared mechanics and campaigns

- [ ] Map editor: playable cells/dimensions, background, objects/ownership/effects and deployment zones, plus seeded random generation as a starting point.
- [ ] Scenario editor: fleets/reserves, rules/deck selection, objectives, medals, events, briefing and timing conditions using existing validated definitions.
- [ ] Campaign editor: mission graph, victory/defeat/draw branches, progression/rewards/purchases and persistent fleet settings once those campaign mechanics exist.
- [ ] Shared editor workflow: new/open/save, validation with actionable errors, undo/redo, playtest and export as a self-contained data mod. Do not overwrite base content.
- [ ] Playtest the same exported data in single player, PvP and cooperative campaigns; save progress on the host.

## Deferred art import

- [x] Built-in emblem importer and source selection.
- [ ] Built-in ship/background importer UI and ownership/source selection.
- [ ] HWRM patch archive precedence and more HOD variants.
- [ ] Joint transforms, material mapping, team colors and effect reconstruction.
- [ ] HWRM background scenes and their textures through the same local-import workflow.

Per the latest scope decision, all active rendering stays as colored cubes until the game flow is useful.
