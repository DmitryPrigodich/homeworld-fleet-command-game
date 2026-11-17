# Supported command orders

**SINGLE PLAYER → COMMAND ORDERS AND EFFECTS** opens a separate tabletop practice battle with `core:available-commands`. Multiplayer has the same option for PvP and shared-fleet co-op. Training ships and their command officers deliberately use synthetic profiles; they are not transcriptions of photographed ship/leader cards.

This deck contains **45 physical copies / 16 supported types**, retaining the published copy counts of those types. It is a partial implementation of the 54-card base deck. The old 24-card training deck and First Contact remain unchanged.

| Type | Copies | Special order |
| --- | ---: | --- |
| All Ahead Full | 2 | Two straight Moves on the same course; obsolete printed ram bonus removed by Errata 1.7 |
| Stay in Formation | 3 | First unit Move → Attack; second Lightning Move → Attack, ending Lightning adjacent to the first |
| Intercept | 3 | Move → Lightning Move → Attack |
| Hit and Run | 3 | Move → Attack → Lightning Move |
| Pincer Attack | 3 | Two units, each Lightning Move → Attack; second attacks the first unit's target |
| Hammer and Anvil | 2 | First Power Attack; second Lightning Move → Attack at the first target |
| Weapons to Maximum | 6 | Move → Power Attack, including eligible ranged, ram or boarding attacks |
| Ship Assault | 2 | Complete Move → optional adjacent boarding attack with +2 dice |
| Deploy Engineers | 2 | Remove one chosen Crippled/Focus Fire token, then choose Move or Attack |
| Brace for Impact | 3 | Braced, then choose Move or Attack; excludes fighters |
| Repel Boarders | 2 | Lockdown, then choose Move or Attack; excludes fighters |
| Evasive Pattern Delta | 3 | Evasive level +1 then Move; requires Afterburners |
| Target Engines | 3 | Move → Attack; one success before armor cripples engines on a non-fighter |
| Target Weapons | 3 | Move → Attack; one success before armor cripples weapons on a non-fighter |
| Target Bays | 2 | Move → Attack; one success before armor cripples bays on a non-fighter |
| Interrupt Attack | 3 | Existing response fire before attack dice or after entering a free hex |

**Not yet implemented:** Ready to Launch (2), Interrupt Move (2), Interrupt Repairs (3), Interrupt Counterintelligence (2). Their faces have been supplied; these are implementation gaps, not outstanding photo requests. Heroic commands, attachments and exact remaining unit/leader profiles are separate subsequent work. See [CARD_INPUTS.md](CARD_INPUTS.md) for source/revision provenance.

## Playing the orders

After initiative, select a ready unit and a card. MOVE/ATTACK use any card as a basic order; **SPECIAL ORDER** executes its printed sequence. Assign the second ready unit before starting a two-line card. A special order spends one card and one Play, including both assigned units. Actions are optional; **SKIP ACTION** advances one step, and **FINISH ORDER** ends the sequence.

For a Move-or-Attack step, choose MOVE or ATTACK on the right, then the green hex or red target on the field. Choosing an attack does not also grant movement. Power steps force Power Attack without relying on a client checkbox. Ship Assault offers boarding only after an actual completed Move; skipping Move grants no boarding attack. Same-target orders retain the first declared target through responses and saves; if that attack was skipped or the target has disappeared, the second unit has no legal attack target.

Deploy Engineers has a separate repair picker before starting. It removes one whole Focus Fire token (up to two accumulated points) or the selected Crippled token, independently of activation Damage Control. The corrected card does not restore that token at turn end. Buffs apply at activation start, expire on a new order, and are removed by applicable Focus Fire/Crew Damage. Braced does not increase boarding armor; Lockdown explicitly does. Target-system effects require a success before armor reduction, ignore fighters and do not leak into an interrupt, CAP volley or defensive fire.

Definitions, copy counts, descriptions and supported effect parameters are JSON mod data. Text previews and EN/RU catalogs provide the functional presentation. Original card pixels are optional and are not distributed. Saves retain the selected action, current step, first target and completed movement through response windows; ENet peers use protocol `fleet-cards-v3`.
