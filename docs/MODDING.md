# Data mods, schema version 1

Place a mod folder in `mods/` beside the project or `.local/mods/`, then enable it in **Mods**. Apply changes before starting a new game. Existing saves require exactly the same content fingerprint.

Each folder contains `mod.json` and `content.json`. Unknown JSON properties are rejected to expose spelling mistakes.

```json
{
  "id": "myfleet",
  "version": "0.1.0",
  "schemaVersion": 1,
  "requires": ["core"],
  "overrides": ["core:kushan-interceptor"]
}
```

Definitions have namespaced IDs: `myfleet:ship-name`. A new definition must use its owning mod's namespace. Existing definitions can only be replaced if their IDs appear in `overrides`. Depend on the mod you override. Replacement is of the entire definition, not a deep merge. Missing dependencies and dependency cycles are errors. Version ranges are not implemented; the full content hash guards multiplayer and saves instead.

`content.json` accepts these optional arrays:

| Array | Main fields |
| --- | --- |
| `ships` | id, name, faction, model, models, move, dicePerModel, success, armor, range, abilities, visualSize, color |
| `abilities` | id, handler, value |
| `fleets` | id, name, units: ship, q, r, facing |
| `objects` | id, name, model, blocksMovement, attackPenalty, moveCost, color |
| `maps` | id, name, width, height, background, objects, fleets, rules |
| `rules` | id, name, handler, roundLimit, ordersPerSide, alternatingOrders, friendlyPassThrough, rearShots, focusFire |
| `themes` | id, name, background, panel, text, muted, accent, highlight, enemy, grid |
| `campaigns` | id, name, start, missions: id, name, map, onVictory, onDefeat |

The C# records in `src/FleetCommand.Core/Definitions.cs` are the canonical schema. All colors use `#RRGGBB` or `#RRGGBBAA`. A ship's empty `color` selects the side color from the current theme. `visualSize` changes cube size without changing combat stats. `model` is a future visual asset reference and is not loaded by the cube renderer.

Initial ability handlers are `penetrating`, `rapid-fire`, and `move-bonus`. Combine abilities on a ship to reuse these mechanics. A wholly new ability mechanic needs a small code change; arbitrary strings are not executed.

Maps use axial `(q,r)` hex coordinates inside a rectangular coordinate domain. `facing` is 0-5. Map objects may block movement, increase movement cost and penalize attacks. A blocking object also blocks line of sight in `skirmish-v1`. A background hex color changes the procedural sector palette. Texture/scene backgrounds are deferred.

Use `mods/example` as a minimal working theme mod. A campaign transition set to null ends the campaign; otherwise it names another mission in that campaign. Cycles are valid. The prototype treats a blue victory as campaign success and a draw as defeat.

For code-based rule extensions, implement `IRuleModule` and pass the instance into `BattleEngine` from the game bootstrap. Rebuilding is currently required. This keeps the first extension point small and testable.
