# Heroes and Heroic commands

**SINGLE PLAYER → HEROES AND HEROIC COMMANDS** opens `practice:heroes`. **MORE HEROIC COMMANDS** opens `practice:advanced-heroes` with six additional command/owner pairs. The network menu offers both maps for PvP and shared-fleet co-op. Ships in this drill have synthetic training profiles. The following leaders and command faces are transcribed from supplied references; they do not establish that the remaining expansion is complete.

| Command | Owner | Implemented special sequence |
| --- | --- | --- |
| Best of the Best | Taiidan Hot Shot Pilot | Evading at activation start → Move → Power Attack |
| Fire and Maneuver | Kushan Strategist | Braced at activation start → Move → Power Attack |
| Prepare to Be Boarded | Kushan Station Overseer | Lockdown at activation start → Power Attack |
| I Can Fix It | Taiidan Master Engineer | Remove one chosen Crippled/Focus Fire token at activation start → Move → Attack |
| Patch 'Em Up | Kushan Medical Chief | Remove one Crew Damage token at activation start → Move → Attack |
| Power and Precision | Kushan Master of Arms | Move → Power Attack; a non-fighter success before armor cripples weapons |
| No Escape | Taiidan Hunter | Move → Power Attack; a non-fighter success before armor cripples engines |
| Lead the Attack | Kushan Fighter Ace | Hero: Move → Power Attack; second fighter: Lightning Move → Attack against the same target |
| Frigate Attack | Taiidan Veteran Captain | Hero: Move → Power Attack; second frigate: Move → Attack |
| Lightning Strike | Kushan Grizzled Veteran | Move → optional boarding +2 dice → Lightning Move → optional boarding +2 dice |

## Lifecycle and controls

Campaign Expansion p.7 supplies the shared lifecycle: each unique purchased Hero grants its own command at scenario start. Heroic cards do not count towards Hand and do not enter the shared draw/discard piles. They are spent once per scenario, including when paying for a basic order or initiative. Round replenishment never recreates them. The matching Hero must still be in play; this implementation requires a surviving non-hulk on-board owner. A Hero in reserve or a bay becomes usable on entering play. The prototype AI retains its Heroic cards without playing them, following the expansion's automated-enemy restriction.

Heroic commands have a separate heading in the ordinary hand panel. Select the card and the matching Hero's ready unit, then **SPECIAL ORDER**. A basic use can instead command another ready friendly unit and grants none of the special effect. Special orders spend one card and one Play, activate the assigned unit and retain the usual optional actions, Damage Control, responses and terrain. The right panel displays active effects.

Used cards remain in a public once-per-scenario ledger. Opponent hands, normal draw-pile order and authoritative RNG remain hidden. Public Hero identities and their granted command registry are visible, as their presence is already known from fleet setup. Mid-order and post-roll saves retain the Heroic card, effect, spending and continuation. Protocol `fleet-cards-v9` rejects older clients.

## Published leader profiles

| Leader | Classes | Stars | Hand | Play | Supported abilities and tabs |
| --- | --- | ---: | ---: | ---: | --- |
| Hot Shot Pilot | Fighter | 3 | 2 | 1 | Ace Pilot 3: +3 close-attack dice; Lead by Example: +1 Evasive level |
| Station Overseer | Station | 2 | 3 | 2 | Cool Under Pressure 1; Overlapping Arcs: self/adjacent allies +1 defensive die; mandatory station tab |
| Strategist | Super Capital Ship (`capital`) | 5 | 4 | 2 | Cool Under Pressure 1; same defensive aura; mandatory capital and optional capital/frigate tabs |
| Master Engineer | Station or Frigate | 2 | 2 | 1 | Cool Under Pressure 1; Staff |
| Medical Chief | Frigate, Station or Super Capital Ship | 2 | 1 | 1 | Staff; Medic: roll once per Crew Damage token on activation, each 5+ removes one |
| Master of Arms | Frigate | 3 | 3 | 2 | Cool Under Pressure 1; Capital Killer: self/adjacent allies +1 attack die against frigates/capital ships/stations; mandatory frigate tab |
| Hunter | Super Capital Ship | 4 | 6 | 3 | Cool Under Pressure 1; Fleet Command: self/adjacent allies +1 attack die against any target; mandatory capital and optional capital/frigate tabs |
| Fighter Ace | Fighter | 3 | 2 | 1 | Ace Pilot 3; Lead by Example: +1 Evasive level |
| Veteran Captain | Frigate | 3 | 3 | 2 | Cool Under Pressure 1; Capital Killer as above; mandatory frigate tab |
| Grizzled Veteran | Frigate | 3 | 2 | 1 | Cool Under Pressure 1; Marines 2: +2 boarding dice; mandatory frigate tab |

These printed Hand/Play values contribute through the existing faction-plus-leader budget rules. No statistics are mirrored to an unpictured faction counterpart. Multiple eligible defensive auras add independently; they grant dice only to defensive fire, not ordinary attacks. Lead by Example and Best of the Best add separate Evasive levels.

Staff requires an existing non-Staff leader on the same compatible ship. A Hero is unique per fleet and does not grant permission to attach extra ordinary leaders. Force preparation and saves validate these constraints. Crew/leader transfer now lets the player select which attached leader moves; the source and destination must both remain valid. Moving a Hero changes its command's eligible unit without generating another card.

## Additional timing and assignment rules

Two-line commands spend one card/Play and activate both selected units at the start. The first line belongs to the Hero; the second requires the printed fighter/frigate class. The picker filters incompatible units. Either line may be omitted; the owner must still be in play to spend the card. Lead the Attack retains its first declared target across saves and responses; omitting/skipping that attack supplies no target for the second attack. Frigate Attack permits independent targets.

Patch removes its guaranteed Crew token before Medic rolls the remaining tokens, and before optional normal Damage Control. Medic uses its printed 5+ threshold independently of the ship's Success value. It triggers once on activation, even if optional Damage Control is skipped. Reactions and later actions/repair attempts in the same order do not trigger it again.

Lightning Strike requires completed movement immediately before its optional boarding step. Skipping Move/Lightning grants no boarding; actual Afterburners and engine conditions still govern Lightning. It retains the normal once-per-order boarding limit. The card's +2 dice stack with the owner's Marines 2 bonus. Attack auras stack for eligible living friendly sources using nearest-hull distance and target class. They do not use a missile launcher's class to boost defensive fire against the missile. Leader ability summaries appear in unit details and fleet preparation.

## Source priority and limits

Use the corrected five-card leader/command photos, the Taiidan original Master Engineer face, Campaign Expansion p.7, Errata 1.7 and the supplied mini-Euro replacement catalogue. Corrected Best of the Best lasts until a **new order**, retaining Evading through Focus Fire/Crew Damage. Its marker is distinct from the normal Evasive buff. Corrected Prepare to Be Boarded ends on a new order, Crew Damage or Focus Fire. Brace/Lockdown use their existing normal removal rules. Errata 1.7 places the relevant Heroic start effects before the action sequence. I Can Fix It removes one complete selected token permanently and does not replace normal activation Damage Control. Staff's original “Crew Card” wording means “Leader Card.”

The remaining four photographed Heroic command types are separate implementation work: Supreme Pilot, Launch All Fighters, Rapid Reaction and Planetary Overwatch. They need movement immunity, multiple launch sequences or specialized reaction continuations; they are received sources, not missing photographs. Hunter does not receive the unrelated Hunter unit ability (Errata 1.7). Their received faces are recorded in [CARD_INPUTS.md](CARD_INPUTS.md). Upgrades, facilities, remaining Heroic profiles, campaign purchases and the full official solo command-bank policy are not supplied by these sets. Artwork remains optional and no source photographs are distributed.
