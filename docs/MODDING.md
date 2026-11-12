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
| `ships` | id, name, faction, model, models, move, dicePerModel, success, armor, range, abilities, visualSize, color, tacticalIcon, label, afterburners, class, weapons, evasive, crew, ram, stars, prowArmor, capSuccess, harvest, survey, bays, launchTubes, factory |
| `abilities` | id, handler, value |
| `fleets` | id, name, units: ship, q, r, facing, leader, cargo, reserve, embarkedOn |
| `objects` | id, name, model, blocksMovement, attackPenalty, moveCost, color, label, kind, hazard |
| `maps` | id, name, width, height, background, objects, fleets, rules, captureTargets, asteroidStars, reserveStars, setupZones, reserveFromRound, objectives, victoryMedals |
| `rules` | id, name, handler, roundLimit, ordersPerSide, alternatingOrders, friendlyPassThrough, rearShots, focusFire, tabletop |
| `leaders` | id, name, faction, classes, hand, play, stars, coolUnderPressure, attackDice |
| `themes` | id, name, background, panel, text, muted, accent, highlight, enemy, grid |
| `campaigns` | id, name, start, missions: id, name, map, onVictory, onDefeat, onDraw |

The C# records in `src/FleetCommand.Core/Definitions.cs` are the canonical schema. All content colors use `#RRGGBB` or `#RRGGBBAA`. A ship's empty `color` selects its placeholder hull color from the current theme. Ownership footprints are separate: their color comes from the player profile, or reserved gray for NPCs/neutral objects. `visualSize` changes cube size without changing combat stats. `model` is a future visual asset reference and is not loaded by the cube renderer.

`tabletop: true` currently enables the implemented rulebook foundations on `cards-basic-v1` maps; it does not imply every rule is complete. Use the coverage audit. Ship `class` is one of `fighter`, `corvette`, `platform`, `frigate`, `capital`, `station` and supplies a default tactical symbol. A fleet placement's optional `leader` must match faction and allowed classes. Round Hand/Play budgets freeze until cleanup, then are recomputed from surviving leaders. Existing definitions remain compatible.

Optional weapon listings contain `id`, `name`, `dicePerModel`, `range`, `kind`, `arc`, `targets`, `penetrating`, `rapidFire`, `rapidTargets`, `powerDice`, `powerPenetrating`, `defensiveFire`, `barrage`. Arcs: `all`, `turret`, `spinal`. Target arrays contain ship classes; omitted `targets` permits all classes. `rapidTargets` conditions only rerolls. `defensiveFire: true` enables free pre-attack defense, including a defense-only `range: 0` weapon. Incoming `fighter`, `close`, `missile`, `torpedo`, `bomb` and `drone` kinds supply threat handling, along with fighter units/ram/boarding. A positive `barrage` (2–6) supplies that weapon's success threshold and target-count pool; zero disables it. A basic attack cannot grant itself a Power Attack. Omitting `weapons` preserves old main-weapon stats; an empty array gives no ranged weapons.

Map `captureTargets` is an optional list of battle unit IDs or ship definition IDs that a successful boarding action may capture. It does not itself define medal objectives. Ship `stars` and a destroyed leader's `stars` feed the saved destroyed-star ledger; prototype victory conditions remain separate.

CAP applies to `fighter` units and hosts of class `frigate` or `capital`, with one patrol per host. Patrols stay in saved `units` with a `patrol` host ID but do not occupy a second board cell or provide adjacent defensive support. `capSuccess` (2–6, default zero) overrides the fighter's ordinary success after CAP activation while it remains on patrol; it is available for the Defence Fighter erratum. Barrage retains its own threshold. No guessed special ability is assigned to the base roster.

Terrain `kind` can identify `asteroid` or `planet` for rulebook movement costs. Gravity costs extra when leaving the well; an asteroid costs one move in tabletop mode. Entry/activation attacks and minefields are described below.

## Independent card artwork

Cosmetic packs are loaded from `card-styles/<pack>/style.json` or `.local/card-styles/<pack>/style.json`, independently of gameplay mods. Example:

```json
{"id":"my-art","name":"My card artwork","background":"#102436","accent":"#e2b361","text":"#b4d9e7","orderFrame":"order.png","interruptFrame":"interrupt.png"}
```

Both image fields are optional and must point to PNG/WebP/JPEG files inside that pack. Select a pack in Options or disable it with Text mode. Missing/invalid packs fall back to text; artwork never participates in gameplay content hashes and does not invalidate saves. `card-styles/amber` is a color-only example. The local PDF-frame importer writes only under `.local`; no source PDFs or original frames are published.

`label` optionally supplies a short battlefield name, while `name` remains available in detailed tooltips. `tacticalIcon` accepts `triangle`, `square`, `diamond`, `parallelogram`, `hexagon`, `circle` or `unknown`. For example, add `"tacticalIcon": "square", "label": "Корветы"` to a corvette definition. Legacy interceptor/frigate definitions receive their known symbols without modifying their JSON files or invalidating saves.

Local player profiles support faction defaults (`auto`, Kushan/Taiidan) and built-in kiith selections. Original badge pixels come from the user's installation into `.local/imported/badges`, never from content packs in this repository. The network sends validated emblem IDs, not arbitrary filesystem paths or image data. Co-op uses the host's appearance for the shared fleet.

Initial ability handlers are `penetrating`, `rapid-fire`, and `move-bonus`. Combine abilities on a ship to reuse these mechanics. A wholly new ability mechanic needs a small code change; arbitrary strings are not executed.

`afterburners` supplies the distance for Lightning Move in Stay in Formation: `0` disables it, `1`–`50` gives the movement allowance without ordinary move bonuses. Its default `-1` preserves legacy definitions: the two core interceptor IDs receive 2; other ships receive 0. New mod ships should set the value explicitly. The training card distribution and action sequences currently live in `BasicCardRules.cs`, `SpecialOrders.cs` and `InterruptRules.cs`; adding new card mechanics requires C#, rather than a separate rules scripting language. Interrupt Attack reuses each ship's existing attack stats/abilities and geometry, including modded definitions.

Maps use axial `(q,r)` hex coordinates inside a rectangular coordinate domain. `facing` is 0-5. Map objects may block movement, increase movement cost and penalize attacks. A blocking object also blocks line of sight in `skirmish-v1`. A background hex color changes the procedural sector palette. Texture/scene backgrounds are deferred.

Use `mods/example` as a minimal working theme mod. A campaign transition set to null ends the campaign; otherwise it names another mission in that campaign. Cycles are valid. The prototype treats a blue victory as campaign success and a draw as defeat.

For code-based rule extensions, implement `IRuleModule` and pass the instance into `BattleEngine` from the game bootstrap. Implement its optional `GetActions` method to supply legal green destination cells and red target IDs for the selected unit. It must not mutate state or advance RNG. The default returns no highlights. Rebuilding is currently required. This keeps the first extension point small and testable.

## Terrain attacks

In tabletop mode, object `kind: "asteroid"` defaults to three dice, and `kind: "mine"` to one die per remaining mine. Both default to success 5 and Penetrating 1. Override with `hazard: { "dice": 3, "success": 5, "penetrating": 1 }`; `dice: 0` disables the attack. The existing `core:asteroids` ID also receives asteroid behavior for compatibility. Other object kinds retain ordinary movement/cover behavior. Hazards are inactive in simplified skirmish mode.

A map object placement accepts `owner: -1` (neutral, default), `0` or `1`. Owned mines spare their own fleet; changed tokens are battle state, not edits to the mod. Each minefield starts at five, reduces to three, then disappears. `attackPenalty`, blocking and movement cost remain independent data. Removing a field removes those geometry effects too. Generated `runtime:terrain/…` profiles are internal derivations, not fleet entries; do not reference them in mods. **МИНЫ И АСТЕРОИДЫ** is a training map rather than a published scenario.

## Survey, harvesting and cargo

In tabletop card rules, `ship.harvest` is the range in hexes (`-1`, default, means no Harvest ability; `0` reaches its own hex). `ship.survey: true` reveals all adjacent resource tokens when that ship finishes a movement action. These fields do not change movement/attack statistics. Use `fleet.units[].cargo` to supply initial resources aboard a unit; this is separate from any future off-board reserve pool and from victory stars.

A map opts into resource tokens with `asteroidStars`, a multiset with exactly one integer for each asteroid object (for example `[0, 2, 3]`). At setup, the authority privately shuffles those values and assigns them to asteroid object indices in map order. The array does not fix values to coordinates. Maps without this field keep terrain-only asteroids. Values currently validate as 0–1000 per token, Harvest range as -1–50 and cargo as 0–1,000,000; these are implementation bounds, not tabletop limits. Economy maps currently require `tabletop: true` and `handler: "cards-basic-v1"`.

`BattleState.Deposits` stores the assigned value, harvested flag and per-side Survey knowledge. Host saves retain all values; private views replace unknown values with `-1` and strip the other side's knowledge. Cargo and revealed harvested values are public. Harvest never removes the object or its cover/attack effects. `Harvested0/1` are cumulative collection totals for future objective rules; `Stars0/1` remain the destruction ledger. The example map uses synthetic, clearly labeled training collectors rather than asserting missing published card statistics.

## Bays, reinforcements and factories

`ship.bays` is the number of embarked units (default 0); `launchTubes` is the number of units it can disembark per round. Both validate as 0–100. `factory` lists producible ship classes, for example `["fighter", "corvette"]`. Factories offer positive-star-cost profiles of their own definition's faction. This is whole-unit production; neither ship models nor costs are guessed from missing card data. A parent with Crippled Bays cannot launch, but may still accept/build strike craft into a free bay.

A fleet placement uses `reserve: true` for an off-board frigate/capital, or `embarkedOn: 0` to start a strike unit aboard placement index 0 in the same fleet. Use `q: -1, r: -1` for readability; off-board coordinates are ignored at setup and normalized to -1. The parent can itself start in reserve. Attach leaders and cargo with the existing fields; parent assignment, capacity and strike class are checked. Nested embarked carriers are rejected. Preset bays have no colored-tab requirement; the separate free-form builder applies the tab system documented below.

Maps may provide `reserveStars: [3, 3]` as public building funds, distinct from unit cargo and destroyed-star scores. `setupZones` optionally supplies two arrays of `{ "q": 0, "r": 0 }` cells; the default is each side's outer two columns. `reserveFromRound` (default 1) gates hyperspace reserve deployment. It is not a universal scenario event script. Specific exception rules, including bypassing Launch Tubes, still belong to the published scenario implementation work.

Off-board state is stored in `Reserves`; an empty `Embarked` means reserve, otherwise it names the parent. `Launches` resets at round cleanup. `PendingReinforce` retains a paid placement, and `NextUnit` prevents produced IDs being reused after loss or reload. Public reserve rosters and placement decisions synchronize over ENet; private cards and resource surveys remain redacted.

## Scenario medals

A tabletop card map opts into scenario scoring with a nonempty `objectives` array (up to 32). `victoryMedals` defaults to 3. Each goal has a map-unique `id`, readable `name`, `side` (0/1), `kind`, `medals` (1–3) and optional `threshold` (default 1), `target`, `reversible` and `classes`.

```json
"objectives": [
  { "id": "harvest", "name": "Collect resources", "side": 0, "kind": "harvest-stars", "medals": 1, "threshold": 2 },
  { "id": "station", "name": "Capture the station", "side": 0, "kind": "capture", "medals": 2, "target": "s1-6", "reversible": true },
  { "id": "fighters", "name": "Destroy strike craft", "side": 1, "kind": "destroy-stars", "medals": 1, "threshold": 4, "classes": ["fighter", "platform"] },
  { "id": "target", "name": "Destroy the carrier", "side": 1, "kind": "destroy-unit", "medals": 2, "target": "s0-0" }
]
```

Targets are initial unit IDs (including initial reserves); count every fleet placement, including embarked units, when determining the global numeric suffix. Capture objectives also authorize boarding capture for that target. They do not turn ordinary terrain objects into boardable units. A station currently uses a `station` ship definition/placement. Capture/destroy-unit thresholds must be 1; class filters are for destroyed-star goals only. Positive numeric thresholds are cumulative; transfer/spending/loss of cargo does not reduce harvested totals.

Every objective awards once. Only capture goals may be reversible: their medals follow the last boarding capturer, without accumulating repeated awards. Medals persist through target destruction until a scenario-specific condition says otherwise; more general hold/escort/artifact/event goals remain future work. At the medal threshold the match stops; the round limit compares medals, then destroyed-unit stars. Maps without `objectives` preserve prototype elimination rules.

Campaign missions may set `onDraw`; omission preserves the prior defeat fallback. Objective mission results retain both fleets' medal totals even after a loss. Expansion purchases and major/minor final-campaign scoring are not included in this interface yet.

## Force-building data

Ships and leaders may supply `tabs`. Each tab has a source-local unique `id` (letters/digits/underscore/hyphen, up to 48), `color` (`blue` or `yellow`), `required` (default false), optional `classes`, and `grantedUnit` (default empty). Blue accepts fighter/corvette/platform; yellow accepts frigate/capital/station. A class filter narrows that group. The builder matches purchases automatically, using one tab per purchase, and will rearrange assignments to satisfy mandatory slots where possible. A mandatory leader slot must match that leader's assigned purchased unit.

Example carrier tabs, referencing ship definitions supplied by the same pack:

```json
"tabs": [
  { "id": "assigned", "color": "blue", "grantedUnit": "example:wing" },
  { "id": "extra", "color": "blue", "classes": ["fighter", "corvette"] }
]
```

Ship tabs are blue and require that unit to start embarked in the source ship. Only ships can supply `grantedUnit`: the builder derives it as `<purchased-entry-id>~<tab-id>`, includes it in bay capacity, charges no extra purchase cost and marks the occupied tab grey. Derived units cannot themselves grant another unit or satisfy a purchased-unit tab. The source and granted unit must share a faction. `ship.tabExempt: true` exempts that purchased unit from needing a slot, while it may still satisfy one mandatory slot. Without this flag, a missing tab profile does not silently permit an unrepresented purchase. Profiles with `stars: 0` are not offered for purchase; explicitly granted units may be free.

Personal roster files are separate from mod-defined fixed `fleets`. A `ForcePlan` stores `id`, `name`, `faction`, `budget`, `reserveStars`, `checkTabs`, purchased `units` and `leaders`. Unit entries use a unique safe `id`, `ship`, `placement` (`board`, `reserve`, `bay`), local `column` (0–1), `row` (0-based), `parent` (carrier entry ID), and `facing` (-1 = towards opponent, otherwise 0–5). Leader choices use `leader` (definition ID) and `unit` (entry ID, including a derived free unit where compatible). The roster is validated against currently enabled content, so changing a mod can invalidate a saved draft. Turning off `checkTabs` bypasses only slot coverage/compulsory purchases.

Multiple different leader cards may attach to one unit and sum their bonuses. Fixed fleet definitions keep the backward-compatible `leader` field and add `additionalLeaders: ["example:second-leader"]`. Card budgets count board leaders only and remain frozen for the round. The current transfer action moves the first/named leader and preserves all other assignments; selecting arbitrary simultaneous leader groups is not implemented.

Optional theme fields `tabBlue`, `tabYellow` and `tabGrey` control the editor's slot palette independently of player ownership colors. They default to `#36a9de`, `#d8ad37` and `#87929c`.

The core `PreparedBattle` envelope stores two plans, field dimensions, objects, seed/background and optional two-sided `ForceLimit` records. A limit can cap `budget` and `reserveStars`, whitelist `ships`/`leaders`, or disable `allowReserve`. These restrictions are a core interface for future scenario packs; the editor currently exposes custom skirmishes. This envelope is stored with saves and sent by the host, then validated and registered before battle state loads. Cards, private deposits and RNG still follow normal private-view filtering. The network signature is `fleet-scenarios-v1` to reject older binaries.

## Two-hex ships and board masks

Set a capital/station profile's `footprint` to `2` (default `1`) to occupy bow Q/R plus the hex opposite its facing. Such profiles must have one model and use tabletop rules. Both cells must fit on the board, avoid blocked/occupied cells, and fit the deployment zone when preparing forces or deploying reserves. The large-hull movement handler enforces a maximum of two points per end per Move action, or one with Crippled Engines. Give the definition `move: 2` for the normal rulebook profile. Explicit synthetic large ships are provided in the training mode; they do not claim missing flagship card statistics.

A map may supply `cells: [{"q": 4, "r": 0}, ...]` to restrict the playable area within its width/height bounding box. Omission preserves a rectangular axial board. All fixed placements must lie inside the mask. The first scenario uses this field to preserve the printed offset-grid topology. `leader.closeDice` adds dice only to ordered ram/boarding attacks, unlike general `attackDice`; the readable Wing/Squadron Leader examples use this field.

## Declarative scenario events

A map may add a readable `briefing` and up to 64 `events`. Each event has a map-unique `id`, `trigger` (`end-order`, `round-start`, `round-end`, `battle-end`), `side`, `effect` and readable `name`. Optional `round` selects a round (0 means any); `once` defaults to true; `ifNoEvent` prevents firing when that other event ID has fired. Events process in declared order.

Unit effects are `mark`, `deliver` and `escape`. Optional `class`, object index `object` plus `radius`, and explicit `zone` cells constrain the acting units at order completion. `mark` adds a `token` identifier to each qualifying unit when `once: false`; duplicates on the same unit are prevented. `requiresToken` gates delivery/escape. `deliver` consumes that token and records the event without removing the unit. `escape` explicitly removes qualifying units and their embarked children without destruction stars, retaining an escaped roster. `award` records a flag for an unconditional round/end-battle event. `reinforce` schedules the named `fleet` into reserves once; parent units must be reserve-eligible frigates/capitals, with strike craft optionally embarked by placement index.

An objective with `kind: "event"` refers to the event ID in `target`, with threshold 1 and the desired medal count. `kind: "destroy-count"` counts enemy units recorded in the destruction ledger. These fields join existing objective types; they do not execute arbitrary scripts or constitute a general campaign-event language. The first scenario provides scan → delivery and failed-delivery-at-timeout examples in the base JSON. Additional published exceptions remain separate work.

## Language packs

EN/RU and additional languages are presentation data, loaded outside gameplay fingerprints. Author names and visible source text in English, and use those canonical English strings as keys in every language file. English itself has an editable catalog; missing translations fall back to it. See [LOCALIZATION.md](LOCALIZATION.md) for the catalog format, precedence and fallback. Ship, scenario, event and objective names introduced by a mod should have corresponding language entries in that mod's `locales` directory. Changing languages does not reload game definitions or change saved log records.
