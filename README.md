# Fleet Command

Standalone, moddable fleet tactics prototype built with **Godot 4.7.2 .NET / C#**.

The current version uses differently sized blue and red cubes for ships, with grey and other colored cubes for map objects. No Homeworld models, textures, audio, fonts, rulebook scans or background art are distributed.

## Current playable slice

- Russian main menu: tutorial, single player, multiplayer, options, mods, exit.
- A seven-step interactive tutorial with highlighted selection, movement, round reset and shooting. It uses a passive opponent and keeps existing saves untouched.
- Skirmish with a simple computer opponent or local hotseat.
- Hex movement, terrain movement costs, shortest-path cover, dice attacks, armor, model losses, focus fire and three configurable ability handlers.
- Random skirmish setup: width, height, force counts, object count, seed and procedural background color. Friendly and enemy forces deploy at opposite edges.
- Two-mission example campaign, branching mission definitions, mid-battle save and campaign progress.
- Direct ENet host/join, PvP or cooperative shared control of the blue side against AI. The host validates commands and owns saves. Joining peers receive the current battle and campaign snapshot.
- JSON content mods, dependencies, explicit overrides, versioned manifests and a content fingerprint checked by network peers.
- Theme colors supplied by mods, including an optional amber example theme.
- Ship/object names while holding Alt, object-effect inspection and always-visible Homeworld-style tactical symbols/emblems.
- A scrollable bottom battle log: movement, dice, losses, passing orders, new rounds and victory. The last 200 entries are saved with the battle and synchronized by the host.
- Player name, color and emblem in **Options → Player**. Flattened cube footprints use the player's color; NPCs and neutral objects use reserved gray. PvP color collisions are resolved by the host.
- Optional local import of Kushan, Taiidan and five kiith emblems from an owned HWRM installation. Only the importer code is distributed.

**This is an early prototype, not a complete implementation of Homeworld: Fleet Command.** Command-card hands, leaders, initiative bidding, interrupts, CAP/defensive-fire timing, boarding, production, full scenario objectives and the supplied book campaigns are not implemented yet. The current combat module is explicitly named `skirmish-v1`.

## Run on Windows

Install the .NET SDK (8 or newer) and extract the official **Godot 4.7.2 .NET** release. The standard non-.NET Godot build will not run C#.

```powershell
.\scripts\Start.ps1 -Godot 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'
```

On the original development machine `Play.cmd` also finds the portable engine in `tmp/tools/godot`. The engine and build cache are not tracked in Git. The launch script uses project-local NuGet and save directories, and can use the package feed bundled with Godot for an offline build.

Start with **ОБУЧЕНИЕ** in the main menu. Controls: left-click a unit from the roster or board, choose **Movement** or **Attack**, then left-click a destination hex or enemy. **End turn** passes the remainder of that side's orders. Activated units darken. Hold **right mouse button** over the battlefield and drag to orbit; the **wheel** zooms and **F** resets the view. Camera orientation and zoom persist through selections and orders. Saves are under `.local/`.

Hold **Alt** to show battlefield names, model counts, owners and object hover details; release it to return to compact symbols/emblems. Click an object symbol or label to read its movement/attack effects. Triangle denotes fighters and diamond denotes frigates; mods can choose other supported tactical shapes. The roster uses readable per-fleet unit numbers rather than internal IDs. Player colors affect ownership projections, while cube hull colors still distinguish the placeholder fleets.

The bottom **battle log** records accepted orders, dice and actual casualties, turn/round transitions and the result. Scroll over it to read earlier events. A status line above the history explains rejected orders and tutorial instructions. History is retained in saves and network snapshots; older saves without a history remain compatible and begin recording at the next order. The previous `showLabels` setting is accepted when loading old settings but no longer overrides the Alt control.

## Multiplayer

The prototype uses direct ENet on **UDP 24560**. LAN peers enter the host's reachable address. Internet play requires reachable hosting or a suitable tunnel; a built-in relay and invitation-code lobby are still planned. No router/firewall settings are changed by the application.

Co-op currently means shared control of one fleet with the host's appearance. Individual ownership of subfleets, authenticated persistent player identities, robust reconnect UX and automatic host migration are future work. Player names/colors/emblem IDs are synchronized separately from game definitions and saved with the match. To continue a co-op campaign, the host loads its campaign save, then other players join again with the same enabled mods. All content mods, including themes, currently participate in the content fingerprint; local player preferences and imported badge pixels do not.

## Mods and design

Read [modding](docs/MODDING.md), [architecture](docs/ARCHITECTURE.md) and [roadmap](docs/ROADMAP.md). The guiding rule is to implement existing game needs first and add extension points where they remain simple.

## Checks

```powershell
dotnet build tests/FleetCommand.Checks/FleetCommand.Checks.csproj -m:1
dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll check .
```

The dependency-free check runner covers turn authorization, immutable state transitions, save compatibility, reproducible combat, mod validation, campaign progress and deterministic map generation.

The engine can also run `-- --check-controls` to exercise the tutorial through menu signals and viewport mouse/key events, including camera persistence and save isolation.

`-- --check-presentation` exercises profile settings, gray-color rejection, battlefield labels, object inspection and appearance serialization. Use an isolated `FLEET_COMMAND_DATA_DIR` for these checks. UI captures and imported badge images are ignored local files.

## Resource importer

**Options → Player** can read seven badge images from `HomeworldRM/Data/Homeworld2.big`, placing them under the ignored local cache. If a standard installation is present, badges are read on first startup. Without the game, procedural placeholder emblems are available.

The separate ship importer can list local HWRM ship archives and convert supported pooled HOD 2 static geometry and DXT textures into a private cache. It was checked against four locally installed ship files. Ship-model import is still deferred from the cube UI. Joints, animation, original shaders, team-color masks, patch-archive precedence and backgrounds are not supported yet. See [importer notes](docs/IMPORTER.md).

The user's current rule/campaign/card reference collection is catalogued in [reference sources](docs/REFERENCE_SOURCES.md). Original PDFs and rendered card art remain local.

## Project status and rights

This is an unofficial fan project. It is not endorsed by the Homeworld rights holders. Importing from a local installation avoids bundling proprietary assets; it does not itself establish permission for every possible use. Do not upload extracted assets or original rulebooks to this repository.
