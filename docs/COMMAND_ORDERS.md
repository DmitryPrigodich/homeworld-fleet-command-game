# Supported command orders

**SINGLE PLAYER → COMMAND ORDERS AND EFFECTS** opens a separate tabletop practice battle with `core:available-commands`. Multiplayer has the same option for PvP and shared-fleet co-op. Training ships and their command officers deliberately use synthetic profiles; they are not transcriptions of photographed ship/leader cards.

This deck contains **54 physical copies / 20 supported types**, retaining the published copy counts of those types. This completes the base command deck. The separate 24-card training deck and First Contact retain their legacy reaction timing through an explicitly separate training card definition.

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
| Interrupt Attack | 3 | Attack the acting enemy during Move, Lightning Move or Reinforce |
| Interrupt Move | 2 | One allied unit within 3 hexes of the acting enemy makes a Move |

**Separate expansion work:** Four Heroic command/owner pairs are implemented; see [HEROIC_COMMANDS.md](HEROIC_COMMANDS.md). Remaining Heroic commands, attachments and exact remaining unit/leader profiles are tracked separately. See [CARD_INPUTS.md](CARD_INPUTS.md) for source/revision provenance.

## Playing the orders

After initiative, select a ready unit and a card. MOVE/ATTACK use any card as a basic order; **SPECIAL ORDER** executes its printed sequence. Assign the second ready unit before starting a two-line card. A special order spends one card and one Play, including both assigned units. Actions are optional; **SKIP ACTION** advances one step, and **FINISH ORDER** ends the sequence.

For a Move-or-Attack step, choose MOVE or ATTACK on the right, then the green hex or red target on the field. Choosing an attack does not also grant movement. Power steps force Power Attack without relying on a client checkbox. Ship Assault offers boarding only after an actual completed Move; skipping Move grants no boarding attack. Same-target orders retain the first declared target through responses and saves; if that attack was skipped or the target has disappeared, the second unit has no legal attack target.

Deploy Engineers has a separate repair picker before starting. It removes one whole Focus Fire token (up to two accumulated points) or the selected Crippled token, independently of activation Damage Control. The corrected card does not restore that token at turn end. Buffs apply at activation start, expire on a new order, and are removed by applicable Focus Fire/Crew Damage. Braced does not increase boarding armor; Lockdown explicitly does. Target-system effects require a success before armor reduction, ignore fighters and do not leak into an interrupt, CAP volley or defensive fire.

Definitions, copy counts, descriptions and supported effect parameters are JSON mod data. Text previews and EN/RU catalogs provide the functional presentation. Original card pixels are optional and are not distributed. Saves retain the selected action, current step, first target and completed movement through response windows; ENet peers use protocol `fleet-cards-v10`.

## Ready to Launch

**SINGLE PLAYER → LAUNCH ORDERS** opens a carrier practice battle using the same 54-card supported deck. Multiplayer has the same map option. Select a BAY squadron in the roster, select Ready to Launch and press SPECIAL ORDER. Click a green exit hex. The squadron arrives activated and may complete its remaining Lightning Move and Attack within this one paid order. The carrier retains its previous activation/buffs; launch capacity is shared with normal Reinforce. An activated squadron, crippled bays, exhausted Launch Tubes or blocked exits prevent assignment. Skipping launch ends the squadron's sequence without disembarking it. Activation repair and entry hazards finish before subsequent actions.

## Counterintelligence

When an opponent declares an Interrupt Attack, Move or Repairs, its dice/effect wait for a public counter decision. Use **CANCEL INTERRUPT** with a Counterintelligence card, or **LET INTERRUPT PROCEED**. The cancellation discards both cards immediately; both types still count as played this round (the p.15 limit is one played card per type). It spends no Play and changes no activation. Counterintelligence cannot cancel itself. The paused order then resumes. The opportunity is based on the public deck and used types, not the opponent's hidden hand. Saves preserve the unexecuted command and decision. Interrupt Move also waits at this boundary before any position changes.

## Interrupt Repairs

The right panel offers **INTERRUPT REPAIRS** when your hand has an unused copy and an eligible unit. Choose the target, then REMOVE FOCUS TOKEN or RESTORE ONE FIGHTER. This is available during orders-phase decision boundaries, including the opponent's turn, a suspended move, pending combat/defense, repairs, terrain and Reinforce. It spends no Play, does not activate/reorder the unit and can itself be countered before the effect. One whole Focus Fire token removes up to two accumulated points; it does not restore buffs lost earlier. Restoring a fighter adds one model up to the current profile maximum in a surviving squadron, including embarked squadrons. It does not recreate a fully removed unit or repair non-fighter models/Crippled tokens.

Already rolled dice and RNG remain unchanged. Physical actor snapshots retained for simultaneous CAP resolution receive the same model/Focus update, preserving save validation without rolling extra dice. Repairs uses its own per-round interrupt type; a side can still use Attack separately. When repairing during a response, the original response stays open rather than silently consuming the enemy move.

## Movement and arrival interrupts

The photographed base Interrupt Attack and Interrupt Move trigger during an enemy **Move, Lightning Move or Reinforce**. The purple anchor means Reinforce; it is not an Attack icon. They do not interrupt an ordinary Attack. The separate legacy practice card deliberately retains its older pre-attack fixture timing.

In an **INTERRUPT WINDOW**, choose a reaction card and a friendly unit. Interrupt Move requires the friendly unit to start within three hexes of the acting enemy, measured from the nearest occupied hull hex. Click a green destination; Interrupt Attack clicks the red acting enemy. Ready and already activated units may react. Neither reaction spends Play, activates the unit, clears its existing buffs, or runs activation Damage Control. Normal movement, terrain, CAP and attack rules still apply. A reachable friendly bay also appears green; entering it costs one movement and preserves the reacting squadron's activation. Each side may use each interrupt type once per round, including a cancelled card. A Move card grants no attack.

Both sides may interrupt movement, including movement caused by Interrupt Move; the two nested continuations are saved. After a movement reaction changes the field, the original mover's owner chooses a green destination with only the **remaining** movement allowance, or **STOP HERE**. Weighted terrain costs, spent turning, separate bow/stern budgets and the original straight-course/Lightning restrictions are retained. No extra card or Play is spent. Other eligible interrupt types may be played at the same pause before that route choice.

Reinforce windows open after each unit arrives and its activation/entry terrain resolves, before a further launch or order step. The arriving unit is the acting enemy and target; its carrier retains its activation state. Reserve deployment, factory output and Ready to Launch use the same arrival boundary.
