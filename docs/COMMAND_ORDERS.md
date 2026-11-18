# Supported command orders

**SINGLE PLAYER → COMMAND ORDERS AND EFFECTS** opens a separate tabletop practice battle with `core:available-commands`. Multiplayer has the same option for PvP and shared-fleet co-op. Training ships and their command officers deliberately use synthetic profiles; they are not transcriptions of photographed ship/leader cards.

This deck contains **52 physical copies / 19 supported types**, retaining the published copy counts of those types. It is a partial implementation of the 54-card base deck. The old 24-card training deck and First Contact remain unchanged.

| Type | Copies | Special order |
| --- | ---: | --- |
| All Ahead Full | 2 | Two straight Moves on the same course; obsolete printed ram bonus removed by Errata 1.7 |
| Stay in Formation | 3 | First unit Move → Attack; second Lightning Move → Attack, ending Lightning adjacent to the first |
| Intercept | 3 | Move → Lightning Move → Attack |
| Hit and Run | 3 | Move → Attack → Lightning Move |
| Pincer Attack | 3 | Two units, each Lightning Move → Attack; second attacks the first unit's target |
| Hammer and Anvil | 2 | First Power Attack; second Lightning Move → Attack at the first target |
| Weapons to Maximum | 6 | Move → Power Attack, including eligible ranged, ram or boarding attacks |
| Ready to Launch | 2 | Launch assigned embarked squadron → Lightning Move → Attack |
| Ship Assault | 2 | Complete Move → optional adjacent boarding attack with +2 dice |
| Deploy Engineers | 2 | Remove one chosen Crippled/Focus Fire token, then choose Move or Attack |
| Brace for Impact | 3 | Braced, then choose Move or Attack; excludes fighters |
| Repel Boarders | 2 | Lockdown, then choose Move or Attack; excludes fighters |
| Evasive Pattern Delta | 3 | Evasive level +1 then Move; requires Afterburners |
| Target Engines | 3 | Move → Attack; one success before armor cripples engines on a non-fighter |
| Target Weapons | 3 | Move → Attack; one success before armor cripples weapons on a non-fighter |
| Target Bays | 2 | Move → Attack; one success before armor cripples bays on a non-fighter |
| Interrupt Repairs | 3 | Remove one Focus Fire token or restore one lost fighter model |
| Interrupt Counterintelligence | 2 | Cancel a declared interrupt before its effect; discard both cards |
| Interrupt Attack | 3 | Existing response fire before attack dice or after entering a free hex |

**Not yet implemented:** Interrupt Move (2). Its face has been supplied; this is an implementation gap, not an outstanding photo request. Heroic commands, attachments and exact remaining unit/leader profiles are separate subsequent work. See [CARD_INPUTS.md](CARD_INPUTS.md) for source/revision provenance.

## Playing the orders

After initiative, select a ready unit and a card. MOVE/ATTACK use any card as a basic order; **SPECIAL ORDER** executes its printed sequence. Assign the second ready unit before starting a two-line card. A special order spends one card and one Play, including both assigned units. Actions are optional; **SKIP ACTION** advances one step, and **FINISH ORDER** ends the sequence.

For a Move-or-Attack step, choose MOVE or ATTACK on the right, then the green hex or red target on the field. Choosing an attack does not also grant movement. Power steps force Power Attack without relying on a client checkbox. Ship Assault offers boarding only after an actual completed Move; skipping Move grants no boarding attack. Same-target orders retain the first declared target through responses and saves; if that attack was skipped or the target has disappeared, the second unit has no legal attack target.

Deploy Engineers has a separate repair picker before starting. It removes one whole Focus Fire token (up to two accumulated points) or the selected Crippled token, independently of activation Damage Control. The corrected card does not restore that token at turn end. Buffs apply at activation start, expire on a new order, and are removed by applicable Focus Fire/Crew Damage. Braced does not increase boarding armor; Lockdown explicitly does. Target-system effects require a success before armor reduction, ignore fighters and do not leak into an interrupt, CAP volley or defensive fire.

Definitions, copy counts, descriptions and supported effect parameters are JSON mod data. Text previews and EN/RU catalogs provide the functional presentation. Original card pixels are optional and are not distributed. Saves retain the selected action, current step, first target and completed movement through response windows; ENet peers use protocol `fleet-cards-v6`.

## Ready to Launch

**SINGLE PLAYER → LAUNCH ORDERS** opens a carrier practice battle using the same 52-card supported deck. Multiplayer has the same map option. Select a BAY squadron in the roster, select Ready to Launch and press SPECIAL ORDER. Click a green exit hex. The squadron arrives activated and may complete its remaining Lightning Move and Attack within this one paid order. The carrier retains its previous activation/buffs; launch capacity is shared with normal Reinforce. An activated squadron, crippled bays, exhausted Launch Tubes or blocked exits prevent assignment. Skipping launch ends the squadron's sequence without disembarking it. Activation repair and entry hazards finish before subsequent actions.

## Counterintelligence

When an opponent declares an Interrupt Attack or Repairs, its dice/effect wait for a public counter decision. Use **CANCEL INTERRUPT** with a Counterintelligence card, or **LET INTERRUPT PROCEED**. The cancellation discards both cards immediately; both types still count as played this round (the p.15 limit is one played card per type). It spends no Play and changes no activation. Counterintelligence cannot cancel itself. The paused order then resumes. The opportunity is based on the public deck and used types, not the opponent's hidden hand. Saves preserve the unexecuted command and decision. Interrupt Move is not implemented yet; its future proposal should use the same pre-effect boundary.

## Interrupt Repairs

The right panel offers **INTERRUPT REPAIRS** when your hand has an unused copy and an eligible unit. Choose the target, then REMOVE FOCUS TOKEN or RESTORE ONE FIGHTER. This is available during orders-phase decision boundaries, including the opponent's turn, a suspended move, pending combat/defense, repairs, terrain and Reinforce. It spends no Play, does not activate/reorder the unit and can itself be countered before the effect. One whole Focus Fire token removes up to two accumulated points; it does not restore buffs lost earlier. Restoring a fighter adds one model up to the current profile maximum in a surviving squadron, including embarked squadrons. It does not recreate a fully removed unit or repair non-fighter models/Crippled tokens.

Already rolled dice and RNG remain unchanged. Physical actor snapshots retained for simultaneous CAP resolution receive the same model/Focus update, preserving save validation without rolling extra dice. Repairs uses its own per-round interrupt type; a side can still use Attack separately. When repairing during a response, the original response stays open rather than silently consuming the enemy move.
