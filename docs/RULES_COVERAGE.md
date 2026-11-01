# Rules implementation audit

Reference order: supplied US Digital rulebook + Errata 1.7, then Campaign Expansion, Burning Spear, bonus scenarios, Solo Campaign Guide and RPG crossover. Earlier v020 is a comparison source when wording differs. Rules text is paraphrased here; no scans/art are published.

The user's current scope is all supplied rules before visual playtesting. This audit is a work queue, not a statement that the prototype already implements the entire game.

| Area | Required coverage | Current status |
| --- | --- | --- |
| Round structure (pp. 10–15) | Leader/faction Hand and Play, any-size hidden initiative bids, tie rebids, pass, cleanup discards, draw/recycle | Implemented in tabletop development mode; published leader/deck data still incomplete |
| Orders | All printed command actions, preassigned units, optional actions, spending, buffs/debuffs | Two special orders implemented |
| Interrupts | Attack, Move, Repairs, Counter-Intelligence; once per type/round; suspended actions | Attack implemented; exact other card text missing |
| Movement (pp. 16–17) | Strike/capital turning, turn in place, facing, Afterburners, friendly transit, two-hex ships | Turning, saved facing paths, gravity and Afterburners implemented; two-hex ships pending |
| Ranged combat (pp. 17, 20–24) | Weapon choice, target classes, arcs, cover, power attacks, rapid/evasive cancellation, armor, prow/rear, focus | Weapon data/choice, arcs, cover, rapid/evasive, armor/prow/rear and focus implemented; power-granting cards missing |
| Combat choices (p. 27) | Post-roll Valor and Taiidan rerolls, ownership/passing/exhaustion | Implemented, saved post-roll decision, UI and ENet restore check |
| Close quarters (pp. 17, 20–26) | Ram, collision, boarding, crew damage, capture, overrun, drifting hulks | Implemented, including return collision after target destruction, fighter sacrifice/crippling, map-authorized capture and crew/leader transfer; objective medals still pending |
| Defense (pp. 24–25) | Defensive fire, adjacent support, incoming weapon type, CAP assignment/exit/targeting/interception | Defensive fire implemented with UI/save/ENet checks; CAP and terrain defense pending |
| Unit states (pp. 10, 15) | Damage control, lasting buffs, crippled engines/weapons/bays, faction repair | Effect storage, move/weapon penalties, buffs clearing, chosen activation repair and Kushan faction repair implemented; remaining interactions pending |
| Economy (pp. 18–19) | Hidden asteroid values/survey, harvest/free move, carried stars, transfers, bays, embark/disembark, reserves, factory builds | Not implemented |
| Terrain (p. 28) | Asteroid activation/entry attacks, mines/ownership/reduction, gravity wells | Gravity and asteroid movement costs, generic cover/blocking; entry/activation attacks and mines pending |
| Victory/setup (pp. 13, 29–30) | Medals, destroyed stars, capture objectives, ties, force limits/tabs, deployment | Destroyed-star ledger and capture-target flags implemented; victory still prototype elimination/round limit |
| Scenarios | Ten base scenarios, bonus scenarios, Burning Spear maps/events/objectives | Not implemented as published |
| Campaign expansion | Medals/purchases, planetary facilities/bombardment, upgrades, heroes and unique commands, support/heavy units | Example campaign graph; Barrage against units/projectiles implemented, planet/facility interactions pending |
| Solo/co-op rules | Threat/selection tables, command bank and priorities, reinforcements, campaign progress | Basic AI/shared-control transport only |
| RPG crossover | Supplied alternative fleet/terrain/maneuver rules as optional pack | Not implemented |

## Source gap

The three `HOMEWORLD-*` card PDFs in the supplied G: directory are blank design templates. Component catalogues list names and counts but do not provide the actual card effects/stat blocks. The errata links the official downloads page, which is protected by an access code. The user will provide photographs later and explicitly requested continuing with the written rules now. Exact missing card/leader/unit/hero data must not be guessed from a title or an AI behavior summary. This does not block independent rulebook mechanics.

Card artwork is a separate optional presentation pack, selected in Options. It is excluded from gameplay fingerprints and can be removed without invalidating saves. The supplied blank frames were rendered into the ignored local cache; the repository contains only the importer and a color-only example pack.

## Delivery rule

Every completed row needs authoritative command validation, legal-action previews where applicable, immutable save/resume coverage, network/privacy checks for new hidden data and a usable UI action. Engine-only support is recorded separately from a playable feature. Cosmetic cube rendering remains in scope until the rules are complete.

## Recorded interpretations and boundaries

- Numeric Focus Fire is represented as canonical two-point tokens plus a possible one-point token. Kushan repair removes an entire token (up to two points), following the FAQ.
- A successful scenario-authorized boarding capture retains the last model, transfers control and preserves its activation. One Crew Damage is paid by the boarding unit. Scenario capture medals are a separate pending objective feature.
- Defensive fire and collision do not consume a normal attack window or replace the unit's last ordered action. The boarding lock checks the defender's last ordered action, opponent and round.
- Barrage uses its own success threshold, ignores Evasive and adds no model/leader/support bonus dice. Ordinary reductions still apply; defensive fire ignores terrain. Planet/facility pools remain pending.
- Missing one Stay in Formation action line is permitted. Without a surviving assigned first unit, the second has no adjacency anchor for Lightning Move, but can skip it and attack.
- Current close/defense tests use synthetic statistics to force timing cases. They do not introduce guessed published cards into the playable roster. Unknown card effects remain waiting for the user's photos.
