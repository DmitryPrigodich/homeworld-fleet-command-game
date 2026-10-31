# Architecture

`BattleState.Log` holds the last 200 public battle messages. `BattleEngine.Apply` appends an accepted command's message and turn/round/result transitions to a new list, preserving prior snapshots. Rule modules supply their result message; the current module includes final dice and actual model losses. History travels with saves and authoritative network snapshots. Rejected commands leave history/state unchanged and return immediate UI feedback. Presentation numbers units from their original deployment, keeping labels stable after casualties; internal command IDs stay out of normal labels.

## Boundaries

- `src/FleetCommand.Core`: plain .NET definitions, catalog loading, validation, battle state, rule-module interface, campaign progression and map generation. No Godot dependency.
- `game`: Godot presentation, cube rendering, menu flow, local saves and ENet session adapter.
- `src/FleetCommand.Import`: isolated archive/model readers. The active prototype uses only the badge importer; ship visuals remain cubes.
- `content/base`: baseline definitions and initial theme.
- `mods/example`: optional data-only example theme.
- `.local`: user saves, settings, additional mods, private imported assets and development packages. Always ignored by Git.

## Menu outline

```text
Tutorial
  Basic interactive lesson / cards and special orders guide
Single player
  Campaigns: new / continue
  Skirmish: generated setup / continue
  Card practice
Multiplayer
  Skirmish: PvP or cooperative shared fleet
  Cooperative campaign: new / continue / join
Options
  Theme selection
Mods
  Enable folders, validate, reload
Exit
```

## Battle state and networking

Presentation submits commands to `BattleEngine`. The selected `IRuleModule` returns a new snapshot, without changing the previous snapshot. Rejected commands retain the original state. Battle RNG is explicit save state. Generated map seeds describe public terrain/deployment only.

`BattleEngine.GetActions` asks the rule module for a read-only `UnitActions` preview. `skirmish-v1` shares weighted movement and attack-cover geometry between that query and command validation, without simulating attacks or touching RNG/history. The game renders green destination hexes and red legal target hexes for the local command side; the tutorial filters these to its allowed task. Custom rule modules may implement `IRuleModule.GetActions`; its default returns no highlights.

During multiplayer, only the host executes commands. Remote requests include an expected sequence number; stale requests trigger resynchronization. A peer must register matching content before sending commands. `BasicCardRules.View` removes the opponent's hand/bid, deck order and private RNG from remote snapshots, including the battle nested in campaign progress. Views cannot execute authoritative commands. Host saves retain the complete state.

`CardRound.Pending` stores a played special card, its side, preassigned actors, action index and optional straight-movement direction. Starting an order atomically spends one card and one Play and activates all assigned actors. Intermediate actions reuse skirmish resolution in an isolated snapshot, copying combat changes and RNG back while preserving real activation flags, budgets and turn ownership. `SkirmishRules.FinishOrder` shares end-of-order cleanup between basic and special orders. Cleanup/draw waits until the final step or early completion; battle victory clears the sequence immediately. Skipped actions also advance sequence numbers and synchronize normally. Pending order data is public, while the remaining hands stay private. Adding interrupts will need a separate suspended-response mechanism; it is not implied by this sequential order model.

The initial transport is direct ENet. Relay/lobby integration belongs outside Core. Cooperative players currently share command rights for side zero. Campaign progress belongs to the host and is saved alongside the active battle.

## Campaigns

A campaign is a graph of mission IDs. Each mission chooses a map and victory/defeat transitions. Loops are allowed for reversals. Progress stores completed missions, medals, unlock IDs, participants and an optional in-progress battle. The example campaign awards one prototype medal for a victory; it does not implement the board game's full reward economy.

## Extension policy

Data comes first: stats, force definitions, terrain properties, setup, palette and campaign transitions. New mechanics are ordinary C# implementations of `IRuleModule`, registered with `BattleEngine`; loading arbitrary external assemblies is not enabled. There is no new scripting language, universal visual rule editor or general-purpose plugin bus in the initial implementation.

Rendering uses placeholder cubes. Ship visual size and optional color are data, independent of stats and future asset IDs. The interface palette comes from a selected theme; action hints use semantic green/red. Current map backgrounds are procedural and can override the clear color with a hex color string.
