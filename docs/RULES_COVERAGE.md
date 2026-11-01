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
| Close quarters (pp. 17, 20–26) | Ram, collision, boarding, crew damage, capture, overrun, drifting hulks | Not implemented |
| Defense (pp. 24–25) | Defensive fire, adjacent support, incoming weapon type, CAP assignment/exit/targeting/interception | Not implemented |
| Unit states (pp. 10, 15) | Damage control, lasting buffs, crippled engines/weapons/bays, faction repair | Effect storage, move/weapon penalties, buffs clearing, chosen activation repair and Kushan faction repair implemented; remaining interactions pending |
| Economy (pp. 18–19) | Hidden asteroid values/survey, harvest/free move, carried stars, transfers, bays, embark/disembark, reserves, factory builds | Not implemented |
| Terrain (p. 28) | Asteroid activation/entry attacks, mines/ownership/reduction, gravity wells | Only generic cost/cover/blocking |
| Victory/setup (pp. 13, 29–30) | Medals, destroyed stars, capture objectives, ties, force limits/tabs, deployment | Prototype elimination/round limit |
| Scenarios | Ten base scenarios, bonus scenarios, Burning Spear maps/events/objectives | Not implemented as published |
| Campaign expansion | Medals/purchases, planetary facilities/bombardment, upgrades, heroes and unique commands, support/heavy units | Example campaign graph only |
| Solo/co-op rules | Threat/selection tables, command bank and priorities, reinforcements, campaign progress | Basic AI/shared-control transport only |
| RPG crossover | Supplied alternative fleet/terrain/maneuver rules as optional pack | Not implemented |

## Source gap

The three `HOMEWORLD-*` card PDFs in the supplied G: directory are blank design templates. Component catalogues list names and counts but do not provide the actual card effects/stat blocks. The errata links the official downloads page, which is protected by an access code. The user will provide photographs later and explicitly requested continuing with the written rules now. Exact missing card/leader/unit/hero data must not be guessed from a title or an AI behavior summary. This does not block independent rulebook mechanics.

Card artwork is a separate optional presentation pack, selected in Options. It is excluded from gameplay fingerprints and can be removed without invalidating saves. The supplied blank frames were rendered into the ignored local cache; the repository contains only the importer and a color-only example pack.

## Delivery rule

Every completed row needs authoritative command validation, legal-action previews where applicable, immutable save/resume coverage, network/privacy checks for new hidden data and a usable UI action. Engine-only support is recorded separately from a playable feature. Cosmetic cube rendering remains in scope until the rules are complete.
