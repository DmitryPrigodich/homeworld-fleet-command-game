# Fleet Command

Standalone, moddable fleet tactics prototype built with **Godot 4.7.2 .NET / C#**.

The current version uses differently sized blue and red cubes for ships, with grey and other colored cubes for map objects. No Homeworld models, textures, audio, fonts, rulebook scans or background art are distributed.

## Current playable slice

- Russian main menu: single player, multiplayer, options, mods, exit.
- Skirmish with a simple computer opponent or local hotseat.
- Hex movement, terrain movement costs, shortest-path cover, dice attacks, armor, model losses, focus fire and three configurable ability handlers.
- Random skirmish setup: width, height, force counts, object count, seed and procedural background color. Friendly and enemy forces deploy at opposite edges.
- Two-mission example campaign, branching mission definitions, mid-battle save and campaign progress.
- Direct ENet host/join, PvP or cooperative shared control of the blue side against AI. The host validates commands and owns saves. Joining peers receive the current battle and campaign snapshot.
- JSON content mods, dependencies, explicit overrides, versioned manifests and a content fingerprint checked by network peers.
- Theme colors supplied by mods, including an optional amber example theme.

**This is an early prototype, not a complete implementation of Homeworld: Fleet Command.** Command-card hands, leaders, initiative bidding, interrupts, CAP/defensive-fire timing, boarding, production, full scenario objectives and the supplied book campaigns are not implemented yet. The current combat module is explicitly named `skirmish-v1`.

## Run on Windows

Install the .NET SDK (8 or newer) and extract the official **Godot 4.7.2 .NET** release. The standard non-.NET Godot build will not run C#.

```powershell
.\scripts\Start.ps1 -Godot 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'
```

On the original development machine `Play.cmd` also finds the portable engine in `tmp/tools/godot`. The engine and build cache are not tracked in Git. The launch script uses project-local NuGet and save directories, and can use the package feed bundled with Godot for an offline build.

Controls: select a unit from the roster or board, choose **Movement** or **Attack**, then click a destination hex or enemy. **End turn** passes the remainder of that side's orders. Activated units darken. Saves are under `.local/`.

## Multiplayer

The prototype uses direct ENet on **UDP 24560**. LAN peers enter the host's reachable address. Internet play requires reachable hosting or a suitable tunnel; a built-in relay and invitation-code lobby are still planned. No router/firewall settings are changed by the application.

Co-op currently means shared control of the blue fleet. Individual ownership of subfleets, named persistent player identities, robust reconnect UX and automatic host migration are future work. To continue a co-op campaign, the host loads its campaign save, then other players join again with the same enabled mods. All mods, including themes, currently participate in the content fingerprint.

## Mods and design

Read [modding](docs/MODDING.md), [architecture](docs/ARCHITECTURE.md) and [roadmap](docs/ROADMAP.md). The guiding rule is to implement existing game needs first and add extension points where they remain simple.

## Checks

```powershell
dotnet build tests/FleetCommand.Checks/FleetCommand.Checks.csproj -m:1
dotnet tests/FleetCommand.Checks/bin/Debug/net8.0/FleetCommand.Checks.dll check .
```

The dependency-free check runner covers turn authorization, immutable state transitions, save compatibility, reproducible combat, mod validation, campaign progress and deterministic map generation.

## Resource importer (deferred from the current UI)

An isolated, read-only importer module can list local HWRM ship archives and convert supported pooled HOD 2 static geometry and DXT textures into a private cache. It was checked against four locally installed ship files. It is not used by the cube prototype and has no in-game import screen yet. Joints, animation, original shaders, team-color masks, patch-archive precedence and backgrounds are not supported yet. See [importer notes](docs/IMPORTER.md).

## Project status and rights

This is an unofficial fan project. It is not endorsed by the Homeworld rights holders. Importing from a local installation avoids bundling proprietary assets; it does not itself establish permission for every possible use. Do not upload extracted assets or original rulebooks to this repository.
