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

`CardRound.Pending` stores a played special card, its side, preassigned actors, action index and optional straight-movement direction. Starting an order atomically spends one card and one Play and activates all assigned actors. `SkirmishRules.FinishOrder` shares end-of-order cleanup between basic and special orders. Cleanup/draw waits until the final step or early completion; battle victory clears the sequence immediately. Skipped actions also advance sequence numbers and synchronize normally. Pending order data is public, while the remaining hands stay private.

`CardRound.Response` is a separate suspended action: original command, owning side, basic/special origin and remaining hex path. `ActiveSide` retains the original order owner; only `InterruptRules.Responder` may answer. Basic orders reserve their card, activation and Play before pausing. Movement traverses a deterministic minimum-cost route, offering public response windows at empty hexes; attack pauses before dice. Window eligibility ignores hidden hands. Intermediate attacks reuse skirmish resolution in an isolated snapshot and copy only combat changes/RNG back, preserving real activation flags, budgets and turn ownership. A dead actor cancels its remaining actions; a surviving second formation actor continues. A dead primary provides no adjacency anchor for Lightning Move.

Played interrupts live in `Interrupts0/1` until round-end discard and participate in deck conservation. Reactions spend no Play and do not change activation. Save validation checks actor ownership, action/step, route continuity/bounds/obstacles and card ownership. The host sends public response data with private card views and rejects stale sequence numbers. This is a single response frame for Interrupt Attack, not a generic nested interrupt stack; other reaction types and timing hooks remain future work.

The initial transport is direct ENet. Relay/lobby integration belongs outside Core. Cooperative players currently share command rights for side zero. Campaign progress belongs to the host and is saved alongside the active battle.

## Tabletop foundations

The `tabletop` flag enables the implemented rulebook foundations while legacy practice remains available. `FleetMovement` searches cell/facing states, includes turns and gravity costs, and records per-step facings in suspended movement. `FleetWeapons` validates weapon selection, target classes and arcs. `FleetCombat` holds public rolled dice until the owning side accepts or uses Valor/Taiidan, preserving private RNG on the authority. `FleetRepairs` queues post-roll success distribution for preassigned actors, without advancing their order. Both decisions survive save/load and have explicit command-side ownership.

Round Hand/Play limits are frozen in `CardRound` and recalculated at cleanup from surviving data-defined leaders. Multi-card initiative commitments are redacted per viewer. Cleanup allows unused-card discards and then draws; recycling occurs only when the draw pile is empty. These are foundations, with remaining coverage tracked explicitly in RULES_COVERAGE.md.

`CardStyleCatalog` is intentionally outside gameplay `ContentCatalog`. Optional frame/color packs are local presentation data selected by `AppSettings.CardStyle`; disabling/removing them cannot invalidate a match fingerprint or save. The renderer falls back to text. Original template frames are imported only into the ignored `.local` cache.

`FleetClose` and `FleetTransfers` implement close-quarters effects, hulks and crew/leader transfer. `PendingCombat.Ghost` preserves the rammed unit for return collision even after destruction. `PendingOverrun` retains the original response until advancement is accepted or declined. Combat tracks destroyed stars separately from the still-pending scenario victory system.

`PendingDefense` retains the declared incoming attack before any incoming dice are rolled. The defender chooses a weapon, and its roll uses the same selected Rapid/Evasive and Valor decisions as other combat; Ferocious does not apply. Projectile defense reduces incoming dice, while fighter/ram/boarding defense removes models before the incoming pool is built. Defense may occur inside Interrupt Attack and then resumes the existing response frame. Neither defensive fire nor collision stamps a normal ordered attack. The network protocol version rejects binaries predating these decisions.

`FleetCap` represents an attached patrol using its existing unit plus a host ID, excluding it from board occupancy and terrain cover. Accepted transitions synchronize its position with its host; destruction releases it into the vacated cell. `PendingCapEntry` pauses the completed move before order advancement. A new order carries explicit `UnitDeployment` choices and atomically exits CAP before spending/activation. `PendingDefense` also holds the CAP stage and both simultaneous raw dogfight pools. Confirmed volleys preserve actor snapshots and apply casualties only after both sides finish reroll decisions. CAP then hands off to hull defense and the original incoming attack. All these data are public; cards and RNG remain private.

## Campaign progression

A campaign is a graph of mission IDs. Each mission chooses a map and victory/defeat transitions. Loops are allowed for reversals. Progress stores completed missions, medals, unlock IDs, participants and an optional in-progress battle. The example campaign awards one prototype medal for a victory; it does not implement the board game's full reward economy.

## Extension policy

Data comes first: stats, force definitions, terrain properties, setup, palette and campaign transitions. New mechanics are ordinary C# implementations of `IRuleModule`, registered with `BattleEngine`; loading arbitrary external assemblies is not enabled. There is no new scripting language, universal visual rule editor or general-purpose plugin bus in the initial implementation.

Rendering uses placeholder cubes. Ship visual size and optional color are data, independent of stats and future asset IDs. The interface palette comes from a selected theme; action hints use semantic green/red. Current map backgrounds are procedural and can override the clear color with a hex color string.

## Terrain continuations

`FleetTerrain` queues public `HazardJob` entries for paid activation or movement entry. Repairs finish first; `PendingHazard` then uses a generated terrain combat profile and the existing defense, CAP and post-roll machinery. These profiles are derived at runtime from object data and are never physical units. `Mines` stores only changed token counts by map-object index; an absent entry means five mines, zero means removed. Rendering, cover and movement all enumerate surviving objects.

The queue retains the exact continuation (`order`, `response-start`, `movement`), so multiple actors and terrain features finish before one original action resumes. Defensive-fire mine reductions are deferred until the pending terrain attack resolves. Simultaneous CAP rolls keep their original pools. `PendingResponse.Transit` records an occupied allied cell separately from the unit's last vacant cell. `PendingOrder.Deferred` retains crew/leader transfers until repair and terrain complete. All these stages are public save/network state; hands and authoritative RNG remain redacted. The terrain protocol signature rejects earlier binaries.

## Resource economy

`FleetEconomy` initializes an authority-only shuffled `AsteroidDeposit` dictionary from a map's public star multiset. It reuses the saved RNG. Deposits retain their original star value, harvested flag and Survey bitmask; `BasicCardRules.View` redacts each unknown value and the opponent's knowledge before snapshots or campaign battles leave the host. Local object inspection also calls `KnownStars` rather than reading the authority's raw value.

A basic Harvest order has two steps: optional normal movement, then one deposit choice. The movement uses the existing response/terrain continuation and reveals Survey knowledge only after it finishes. Previews expose legal object indices without revealing their values. Harvest marks a deposit publicly depleted, adds unit cargo and updates cumulative harvest totals without touching destruction stars. Resource transfers use the existing deferred-activation path so repairs and hazards resolve before any cargo moves. The command's `Receive` flag chooses whether the activated actor sends or receives from its adjacent partner. Off-board reserves, factory spending and bays form the logistics layer below.

## Logistics

`FleetLogistics` keeps off-board units in `BattleState.Reserves`, preserving ordinary unit state, cargo and leaders without participating in board occupancy, shooting or Hand/Play budgets. `Embarked` links one level of strike craft to a parent. Setup assigns IDs before partitioning so initial public identities do not shift. A monotonic `NextUnit` produces new identities. Reserve funds have separate public `Funds0/1` counters.

A paid `PendingReinforce` owns placement decisions independently of normal unit orders: its carrier does not activate merely by launching. Deploy/build finish after one placement; a launch order remains open for further embarked units and explicit finish. Arrivals use the repair queue and terrain continuation before that frame resumes. `HazardJob.Resume = reinforce` avoids inventing a movement interrupt window for deployment. Placement previews use the same free-cell/setup-zone checks as commands. Launch counts refresh only at round cleanup.

Embark commands convert to ordinary saved movement with a destination bay ID. Pathfinding reserves one movement for the final entry, observes straight/Lightning constraints and offers interrupts along the approach. The last entry moves the unit off board before order advancement, skipping any later actions assigned to it. Destruction removes embarked children with the parent's score award; the neutral-terrain branch removes them without awarding stars. Prototype force survival and turn availability include reserves so a deployable force is not eliminated solely because it is off board.
