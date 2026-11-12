# Fleet Command

Standalone, moddable fleet tactics prototype built with **Godot 4.7.2 .NET / C#**.

The current version uses differently sized blue and red cubes for ships, with grey and other colored cubes for map objects. No Homeworld models, textures, audio, fonts, rulebook scans or background art are distributed.

## Current playable slice

- Card practice: a shared deck, two-card hand, initiative commitments, card spending and replenishment, two special orders (**All Ahead Full**, **Stay in Formation**) and **Interrupt Attack**. Pending sequences and response windows survive saves and network synchronization.
- A separate **ПРАВИЛА / РЕЖИМ РАЗРАБОТКИ** mode adds leader-defined Hand/Play, multi-card initiative bids, cleanup discards, class-specific movement/turns, weapon choice/arcs, rapid/evasive cancellation, prow/rear armor, post-roll Valor/Taiidan decisions, activation repair and Kushan faction repair. Its deck and ship roster are still training content, not the full published game. See [rules coverage](docs/RULES_COVERAGE.md).
- This mode also supports ramming and return collisions, boarding/crew damage, drifting hulks, scenario-authorized capture, optional post-combat movement and crew/leader transfers. Defensive fire happens before the incoming roll and changes its pool after casualties; adjacent support, projectiles, powered-drone Evasive and Barrage are covered. Rapid Fire/Evasive owners select the actual dice to reroll.
- Fighter CAP can be assigned after movement, follows its host, can be targeted separately, exits into a chosen adjacent hex before a new order and returns activated when its host is destroyed. Interception spends a chosen card without Play; fighter exchanges resolve simultaneous casualties before the original attack and hull defense continue.
- **МИНЫ И АСТЕРОИДЫ** adds attacks on activation/entry, mine ownership and 5 → 3 → removed tokens, CAP/defensive fire and saved movement continuation. Removed fields disappear from rendering and geometry.
- **РАЗВЕДКА И ДОБЫЧА** adds privately surveyed asteroid values, a Harvest order with optional free movement, cargo, depleted fields and resource transfers in either direction. The sample collectors are explicitly training profiles. Economy state survives saves and redacted multiplayer snapshots.
- **АНГАРЫ И ПОДКРЕПЛЕНИЯ** adds movement into bays, a separate off-board reserve roster, round-wide Launch Tubes limits, reserve deployment and factories paid from cargo/reserve stars. Placement has green hex previews. Carrier destruction also removes and scores embarked units/leaders; the launch itself leaves the carrier unactivated.
- **ЦЕЛИ И МЕДАЛИ** adds one-time capture/recapture, destroyed-star, class-filtered, target-unit and harvest objectives. Three medals end the battle immediately; the round limit compares medals, then destroyed stars. Campaigns retain both fleets' actual medals and per-mission results. A co-op host can choose a campaign from the network menu.
- **СЦЕНАРИИ ИЗ КНИГИ / RULEBOOK SCENARIOS** starts Scenario 1, First Contact: the printed deployment/terrain layout, seven-round limit, intelligence collection/delivery and two-unit destruction objectives. The Interceptor and leader profiles illustrated in the book are included; the command deck remains the explicitly labelled training deck. The scenario can also be hosted in PvP or shared-fleet co-op.
- **ДВУХГЕКСНЫЕ КОРАБЛИ / TWO-HEX SHIPS** provides a training battle with two occupied hexes per large hull, separate bow/stern movement limits, pivots, full-body collision/terrain checks and rear-shot immunity.
- **ПОДГОТОВКА ФЛОТОВ** adds two independently editable faction rosters, star budgets and reserve funds, leader assignment, compulsory/optional blue and yellow tabs, derived free assigned units, deployment and carrier bays. Rosters save as local JSON; validated setup travels in battle saves and ENet snapshots. Available costs/tabs are explicitly synthetic training profiles until the actual cards arrive.
- Optional card artwork packs selected in **Options → Card appearance**. Text mode is the default; removing an art pack does not affect match fingerprints or saves. Original blank frames can be rendered locally with `scripts/Import-CardFrames.ps1` and are never distributed.

- **Options → Language / Опции → Язык** switches between English and Russian and retains the choice. English is the source/default language and also has its own editable catalog; **Reload languages** applies JSON wording changes without recompilation. Additional JSON language packs can live in mods or `.local/locales`; language does not affect saves or multiplayer content hashes. See [localization](docs/LOCALIZATION.md).
- Main menu: tutorial, single player, multiplayer, options, mods, exit.
- A seven-step interactive tutorial with highlighted selection, movement, round reset and shooting. It uses a passive opponent and keeps existing saves untouched.
- Skirmish with a simple computer opponent or local hotseat.
- Hex movement, terrain movement costs, shortest-path cover, dice attacks, armor, model losses, focus fire and three configurable ability handlers.
- Random skirmish setup: width, height, force counts, object count, seed and procedural background color. Friendly and enemy forces deploy at opposite edges.
- Two-mission example campaign, branching mission definitions, mid-battle save and campaign progress.
- Direct ENet host/join, PvP or cooperative shared control of the blue side against AI. The host validates commands and owns saves. Joining peers receive the current battle and campaign snapshot.
- JSON content mods, dependencies, explicit overrides, versioned manifests and a content fingerprint checked by network peers.
- Theme colors supplied by mods, including an optional amber example theme.
- Ship/object names while holding Alt, object-effect inspection and always-visible Homeworld-style tactical symbols/emblems.
- Selected ready units show reachable destinations in green and legal enemy targets in red, from their current position. Both are shown regardless of the selected order button.
- A scrollable bottom battle log: movement, dice, losses, passing orders, new rounds and victory. The last 200 entries are saved with the battle and synchronized by the host.
- Player name, color and emblem in **Options → Player**. Flattened cube footprints use the player's color; NPCs and neutral objects use reserved gray. PvP color collisions are resolved by the host.
- Optional local import of Kushan, Taiidan and five kiith emblems from an owned HWRM installation. Only the importer code is distributed.

**This is an early prototype, not a complete implementation of Homeworld: Fleet Command.** Other printed cards/leader abilities, exact published force profiles, scenario packs and supplied book campaigns remain in progress. The simplified combat module is `skirmish-v1`; card modes use `cards-basic-v1`, with the new mechanics enabled by the rules definition's `tabletop` flag.

## Run on Windows

Install the .NET SDK (8 or newer) and extract the official **Godot 4.7.2 .NET** release. The standard non-.NET Godot build will not run C#.

```powershell
.\scripts\Start.ps1 -Godot 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'
```

On the original development machine `Play.cmd` also finds the portable engine in `tmp/tools/godot`. The engine and build cache are not tracked in Git. The launch script uses project-local NuGet and save directories, and can use the package feed bundled with Godot for an offline build.

`Play.cmd` preserves a separate launch transcript and engine log for each run under `.local/logs`. Handled startup/menu errors also write individual exception reports there; the error screen shows the report path. Closing the window or running tests does not overwrite those reports. A failed build or nonzero engine exit keeps the launch window open through `Play.cmd`.

Start with **ОБУЧЕНИЕ** in the main menu. Controls: left-click a unit from the roster or board, choose **Movement** or **Attack**, then left-click a destination hex or enemy. **End turn** passes the remainder of that side's orders. Activated units darken. Hold **right mouse button** over the battlefield and drag to orbit; the **wheel** zooms and **F** resets the view. Camera orientation and zoom persist through selections and orders. Saves are under `.local/`.

Hold **Alt** to show battlefield names, model counts, owners and object hover details; release it to return to compact symbols/emblems. Click an object symbol or label to read its movement/attack effects. Triangle denotes fighters and diamond denotes frigates; mods can choose other supported tactical shapes. The roster uses readable per-fleet unit numbers rather than internal IDs. Player colors affect ownership projections, while cube hull colors still distinguish the placeholder fleets.

**ОБУЧЕНИЕ → КАРТЫ И СПЕЦПРИКАЗЫ** explains initiative and opens a card battle. After initiative, select a ready unit and a card, then press **СПЕЦПРИКАЗ**. Stay in Formation also requires selecting the second ready unit before starting. The interface selects the actor for each step and highlights its legal actions. **ПРОПУСТИТЬ ДЕЙСТВИЕ** skips one step; **ЗАВЕРШИТЬ ПРИКАЗ** skips the remainder. A multi-step order spends one card and one Play. The current training deck distribution and movement limitations are documented in [testing](docs/TESTING.md).

The same guide has **ТРЕНИРОВКА: ПРЕРЫВАНИЕ**, a repeatable response-fire lesson that preserves your match save. In a response window, select an allied unit and click the red target to use Interrupt Attack, or press **ПРОПУСТИТЬ ОКНО**. Activated units can respond; the reaction spends a card, uses no Play and leaves their activation unchanged. Only one Interrupt Attack per side per round is allowed. During movement the window opens after entering an empty hex; during an attack it opens before dice. The enemy order then resumes or cancels the destroyed actor's remaining actions. AI also responds to these windows.

Selecting a unit ready for your current turn highlights **green movement hexes** and **red enemy target hexes** together. Movement includes terrain costs, obstacles, occupied cells and ability bonuses; attacks use current range and line of sight. Use Movement or Attack to issue the corresponding order. Spent activations, other players' units and finished battles have no actionable highlights. Tutorial highlights also respect its current task. Calculating the preview does not roll dice, spend orders or add history entries.

The bottom **battle log** records accepted orders, dice and actual casualties, turn/round transitions and the result. Scroll over it to read earlier events. A status line above the history explains rejected orders and tutorial instructions. History is retained in saves and network snapshots; older saves without a history remain compatible and begin recording at the next order. The previous `showLabels` setting is accepted when loading old settings but no longer overrides the Alt control.

**ОДИНОЧНАЯ ИГРА → РАЗВЕДКА И ДОБЫЧА** opens the economy training map. Select a ready collector and press **ДОБЫЧА РЕСУРСОВ**. Move normally for free or skip that step on the left, then select a field with a **ДОБЫТЬ** button or on the map. Survey reveals adjacent fields only after movement finishes, for your fleet alone. Harvest flips the field publicly and puts its stars aboard the collector. To move cargo, select a ready adjacent allied ship, choose direction/amount and press **ПЕРЕДАТЬ РЕСУРСЫ**. The neighboring ship may already be activated. These resources are distinct from destruction-score stars; factories can spend them through the logistics controls below.

**ОДИНОЧНАЯ ИГРА → АНГАРЫ И ПОДКРЕПЛЕНИЯ** opens a separate training map. Select a carrier and use **ВЫПУСТИТЬ ИЗ АНГАРА** or **ПРОИЗВОДСТВО**. Select the squadron/ship and a green arrival hex, or build strike craft directly into an available bay. Set the amount paid from the reserve; the rest comes from factory cargo. Select an off-board frigate in the roster to **ВВЕСТИ ИЗ РЕЗЕРВА**. Ready strike craft can use **В АНГАР** to move to a carrier and pay one movement for the final embark step. Launches share a per-round limit; released units count as activated, while the carrier does not. All sample carrier/wing profiles remain clearly labeled training data.

**ОДИНОЧНАЯ ИГРА → ЦЕЛИ И МЕДАЛИ** opens an asymmetric training scenario with a station, harvesting and destruction goals. The left panel shows both fleets' progress and medals; **СКРЫТЬ ЦЕЛИ / ЦЕЛИ СЦЕНАРИЯ** toggles details. Capture is resolved through a successful boarding result. The result panel and event log explain medal victory or the round-limit comparison. **Кампании → Медали — учебная кампания** uses two test missions with real medal accounting; this is not a published campaign reproduction.

## Multiplayer

The prototype uses direct ENet on **UDP 24560**. LAN peers enter the host's reachable address. Internet play requires reachable hosting or a suitable tunnel; a built-in relay and invitation-code lobby are still planned. No router/firewall settings are changed by the application.

Co-op currently means shared control of one fleet with the host's appearance. Individual ownership of subfleets, authenticated persistent player identities, robust reconnect UX and automatic host migration are future work. Player names/colors/emblem IDs are synchronized separately from game definitions and saved with the match. To continue a co-op campaign, the host loads its campaign save, then other players join again with the same enabled mods. All content mods, including themes, currently participate in the content fingerprint; local player preferences and imported badge pixels do not.

## Mods and design

Read [modding](docs/MODDING.md), [architecture](docs/ARCHITECTURE.md) and [roadmap](docs/ROADMAP.md). The guiding rule is to implement existing game needs first and add extension points where they remain simple.

## Checks

Run **`Test.cmd`** for the complete Windows regression suite. It runs rule checks, deterministic whole-battle scenarios with save/resume, Godot UI checks, and real ENet PvP/co-op/card-mode pairs. Reports and reproducible traces go to `tmp/test-results`. For a fast engine-free run use `./scripts/Test.ps1 -CoreOnly`. See [testing and card-practice scope](docs/TESTING.md).

```powershell
dotnet build tests/FleetCommand.Checks/FleetCommand.Checks.csproj -m:1
dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll check .
```

The dependency-free check runner covers turn authorization, immutable state transitions, save compatibility, reproducible combat, mod validation, campaign progress and deterministic map generation.

The engine can also run `-- --check-controls` to exercise the tutorial through menu signals and viewport mouse/key events, including camera persistence and save isolation.

`-- --check-presentation` exercises profile settings, gray-color rejection, battlefield labels, object inspection and appearance serialization. Use an isolated `FLEET_COMMAND_DATA_DIR` for these checks. UI captures and imported badge images are ignored local files.

`-- --check-actions` verifies that both highlight meshes appear on selection, remain consistent across order modes and clear after activation. Core checks cover terrain costs, friendly pass-through, movement bonuses, line-of-sight blocking and agreement between previews and command validation.

## Resource importer

**Options → Player** can read seven badge images from `HomeworldRM/Data/Homeworld2.big`, placing them under the ignored local cache. If a standard installation is present, badges are read on first startup. Without the game, procedural placeholder emblems are available.

The separate ship importer can list local HWRM ship archives and convert supported pooled HOD 2 static geometry and DXT textures into a private cache. It was checked against four locally installed ship files. Ship-model import is still deferred from the cube UI. Joints, animation, original shaders, team-color masks, patch-archive precedence and backgrounds are not supported yet. See [importer notes](docs/IMPORTER.md).

The user's current rule/campaign/card reference collection is catalogued in [reference sources](docs/REFERENCE_SOURCES.md). Original PDFs and rendered card art remain local.

## Project status and rights

This is an unofficial fan project. It is not endorsed by the Homeworld rights holders. Importing from a local installation avoids bundling proprietary assets; it does not itself establish permission for every possible use. Do not upload extracted assets or original rulebooks to this repository.

## Preparing custom forces

Open **ОДИНОЧНАЯ ИГРА → ПОДГОТОВКА ФЛОТОВ**. Select our fleet or the opponent, choose a faction, buy units and leaders, then assign each leader to a compatible unit. Full tabs are compulsory, half-filled tabs optional, and grey tabs already belong to free assigned units. Each purchased unit uses one matching tab. Craft bought against carrier tabs must start in that carrier's bay. Multiple distinct leaders may share a compatible unit; their bonuses stack.

The budget includes units, leaders and the explicitly reserved stars. Unspent budget is not added to match funds. Place units in either of your two edge columns, keep eligible frigates/capital ships in reserve, or embark strike craft in available bays. The editor reports both fleets' errors and enables launch once both are valid. Disabling tab checks allows free composition while retaining cost, faction, leader and placement validation. You may save invalid drafts, then fix them later. Personal rosters are stored under `.local/fleets/<id>.json`; modded ship/leader definitions are rechecked when a roster is loaded.

Field controls set dimensions, object count, seed and background color. Custom battles use minor/major destruction objectives at 40%/60% of the opposing committed budget (purchased units, leaders and reserved funds), awarding one/two medals. These are generated skirmish defaults, not a published scenario. For direct PvP or shared-fleet co-op use **СОВМЕСТНАЯ ИГРА → ПОДГОТОВИТЬ ФЛОТЫ ДЛЯ СЕТИ**; the host configures both fleets. Peers receive the setup and private battle view. The host can resume a saved skirmish from that menu. Opponents do not yet negotiate individual rosters in a ready room.
