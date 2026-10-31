# Automated regression runs

Run `Test.cmd` on Windows, or `./scripts/Test.ps1`. The full suite builds Core/Game, runs the 56 existing rule checks, replays JSON battle scenarios, drives four Godot UI checks and starts real ENet host/client pairs for PvP, co-op and the card mode. Processes have a 45-second timeout. Test games, profiles and saves use isolated directories under `tmp/test-results`; production saves are not overwritten.

For fast checks without Godot: `./scripts/Test.ps1 -CoreOnly`.
For an engine elsewhere: `./scripts/Test.ps1 -Godot 'C:\Tools\Godot_console.exe'` (the .NET build is required).

Results: `tmp/test-results/summary.json`, `scenarios.json`, per-scenario `*.trace.json`, and engine stdout/stderr logs. A nonzero exit code means failure. Traces are written after each command, including rejected commands, so a failed run retains the failing input. The report contains command order, side, seed, round, sequence and a SHA-256 state hash. Run the same JSON specification to reproduce it.

Scenario specifications live in `tests/scenarios/`. Fields are `name`, `map`, `seed`, optional `maxCommands` and `closeContact`. The close-contact fixture keeps one original interceptor squadron per side and places them adjacent; no dice or combat statistics are changed. The deterministic test policy prefers legal attacks, otherwise approaches an enemy, otherwise passes. Card initiative uses zero-card commitments in these long battle runs.

Each battle runs twice: continuously and with save/load after every command. The states must match at every step. Every run must terminate within its command budget, reject commands after completion and preserve prior snapshots. Card runs additionally check deck conservation through validation, private player views, hidden deck/RNG, rejection of opponent-card bids and repeated commitments. A close-contact run requires a decisive victory; longer patrols may end in a round-limit draw.

GitHub Actions runs Core checks and scenarios on each push/PR and uploads scenario traces. Full Godot UI and ENet tests currently run through the Windows script; CI does not claim those engine tests ran.

# Card practice scope

Open Single player > Card practice, or enable the card checkbox when hosting a skirmish. This is the rulebook's basic-order training approach, not the printed special-card ruleset. It uses 24 anonymous practice cards, a shared shuffled deck, Hand 2 / Play 1 (faction-only practice), retention of unused cards, replenishment, discard recycling, and an explicit initiative phase. Each player may commit zero or one card for +1 (roll capped at 6); ties consume that attempt's bids and require fresh commitments. Any hand card pays for a Move or Attack. The UI falls back to the first hand card unless one is selected.

Source basis: user-supplied `HWFC_Rulebook_v020LW.pdf`, pp. 10, 12-14, read alongside Errata 1.7. Confirmed: faction-only Hand 2/Play 1, draw-to-hand, initiative with capped card bonuses and fresh bids after ties, basic orders using any command card, and the suggested basic-order practice mode. Implementation limits: one-card bid cap, no optional pre-draw discard, no leaders, printed special effects, interrupts, Valor, reinforcement/harvest, or complete scenario medal objectives. These remain tracked work; patrol victory still uses prototype elimination/round limits.

Card hands, pending opposing bids, draw-pile order and authoritative RNG are removed from remote snapshots. Co-op shares the host-side hand. Full host saves preserve the complete deck and pending commitments; client views cannot execute authoritative commands.
