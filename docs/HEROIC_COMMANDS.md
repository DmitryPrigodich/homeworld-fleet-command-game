# Heroes and Heroic commands

**SINGLE PLAYER → HEROES AND HEROIC COMMANDS** opens `practice:heroes`. The network menu offers the same map for PvP and shared-fleet co-op. Ships in this drill have synthetic training profiles. The following leaders and command faces are transcribed from supplied references; they do not establish that the remaining expansion is complete.

| Command | Owner | Implemented special sequence |
| --- | --- | --- |
| Best of the Best | Taiidan Hot Shot Pilot | Evading at activation start → Move → Power Attack |
| Fire and Maneuver | Kushan Strategist | Braced at activation start → Move → Power Attack |
| Prepare to Be Boarded | Kushan Station Overseer | Lockdown at activation start → Power Attack |
| I Can Fix It | Taiidan Master Engineer | Remove one chosen Crippled/Focus Fire token at activation start → Move → Attack |

## Lifecycle and controls

Campaign Expansion p.7 supplies the shared lifecycle: each unique purchased Hero grants its own command at scenario start. Heroic cards do not count towards Hand and do not enter the shared draw/discard piles. They are spent once per scenario, including when paying for a basic order or initiative. Round replenishment never recreates them. The matching Hero must still be in play; this implementation requires a surviving non-hulk on-board owner. A Hero in reserve or a bay becomes usable on entering play. The prototype AI retains its Heroic cards without playing them, following the expansion's automated-enemy restriction.

Heroic commands have a separate heading in the ordinary hand panel. Select the card and the matching Hero's ready unit, then **SPECIAL ORDER**. A basic use can instead command another ready friendly unit and grants none of the special effect. Special orders spend one card and one Play, activate the assigned unit and retain the usual optional actions, Damage Control, responses and terrain. The right panel displays active effects.

Used cards remain in a public once-per-scenario ledger. Opponent hands, normal draw-pile order and authoritative RNG remain hidden. Public Hero identities and their granted command registry are visible, as their presence is already known from fleet setup. Mid-order and post-roll saves retain the Heroic card, effect, spending and continuation. Protocol `fleet-cards-v8` rejects older clients.

## Published leader profiles

| Leader | Classes | Stars | Hand | Play | Supported abilities and tabs |
| --- | --- | ---: | ---: | ---: | --- |
| Hot Shot Pilot | Fighter | 3 | 2 | 1 | Ace Pilot 3: +3 close-attack dice; Lead by Example: +1 Evasive level |
| Station Overseer | Station | 2 | 3 | 2 | Cool Under Pressure 1; Overlapping Arcs: self/adjacent allies +1 defensive die; mandatory station tab |
| Strategist | Super Capital Ship (`capital`) | 5 | 4 | 2 | Cool Under Pressure 1; same defensive aura; mandatory capital and optional capital/frigate tabs |
| Master Engineer | Station or Frigate | 2 | 2 | 1 | Cool Under Pressure 1; Staff |

These printed Hand/Play values contribute through the existing faction-plus-leader budget rules. No statistics are mirrored to an unpictured faction counterpart. Multiple eligible defensive auras add independently; they grant dice only to defensive fire, not ordinary attacks. Lead by Example and Best of the Best add separate Evasive levels.

Staff requires an existing non-Staff leader on the same compatible ship. A Hero is unique per fleet and does not grant permission to attach extra ordinary leaders. Force preparation and saves validate these constraints. Crew/leader transfer now lets the player select which attached leader moves; the source and destination must both remain valid. Moving a Hero changes its command's eligible unit without generating another card.

## Source priority and limits

Use the corrected five-card leader/command photos, the Taiidan original Master Engineer face, Campaign Expansion p.7, Errata 1.7 and the supplied mini-Euro replacement catalogue. Corrected Best of the Best lasts until a **new order**, retaining Evading through Focus Fire/Crew Damage. Its marker is distinct from the normal Evasive buff. Corrected Prepare to Be Boarded ends on a new order, Crew Damage or Focus Fire. Brace/Lockdown use their existing normal removal rules. Errata 1.7 places the relevant Heroic start effects before the action sequence. I Can Fix It removes one complete selected token permanently and does not replace normal activation Damage Control. Staff's original “Crew Card” wording means “Leader Card.”

The other ten photographed Heroic command types remain separate implementation work: Patch 'Em Up, Rapid Reaction, Planetary Overwatch, Launch All Fighters, Lightning Strike, Lead the Attack, Supreme Pilot, Frigate Attack, Power and Precision and No Escape. Their received faces are recorded in [CARD_INPUTS.md](CARD_INPUTS.md). Upgrades, facilities, remaining Heroic profiles, campaign purchases and the full official solo command-bank policy are not supplied by this first set. Artwork remains optional and no source photographs are distributed.
