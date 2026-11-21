# Heroes and Heroic commands

**SINGLE PLAYER → HEROES AND HEROIC COMMANDS** opens `practice:heroes`. **MORE HEROIC COMMANDS** opens `practice:advanced-heroes` with six additional command/owner pairs. **PILOT AND LAUNCH HEROES** adds Supreme Pilot and Launch All Fighters. Its solo variant `practice:pilot-launch-heroes-solo` gives the player both Taiidan Heroes; the network variant `practice:pilot-launch-heroes` is available for PvP/shared co-op. **HEROIC REACTION** opens `practice:heroic-reaction` with Kushan Recon Commander/Rapid Reaction on the human side; a mirrored `practice:heroic-reaction-network` is used by the remote-response check. Ships in these drills have synthetic training profiles. The following leaders and command faces are transcribed from supplied references; they do not establish that the remaining expansion is complete.

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
| Supreme Pilot | Taiidan Veteran Commander | Move → Lightning Move → Attack; enemy transit and asteroid/mine entry immunity during these movements |
| Launch All Fighters | Taiidan Launch Commander | Up to two fighters from Hero's bay, each Launch → Lightning Move → Attack |
| Rapid Reaction | Kushan Recon Commander | During an enemy Move: Hero Move → Attack only that mover; no Play or activation |

## Lifecycle and controls

Campaign Expansion p.7 supplies the shared lifecycle: each unique purchased Hero grants its own command at scenario start. Heroic cards do not count towards Hand and do not enter the shared draw/discard piles. They are spent once per scenario, including when paying for a basic order or initiative. Round replenishment never recreates them. The matching Hero must still be in play; this implementation requires a surviving non-hulk on-board owner. A Hero in reserve or a bay becomes usable on entering play. The prototype AI retains its Heroic cards without playing them, following the expansion's automated-enemy restriction.

Heroic commands have a separate heading in the ordinary hand panel. Select the card and the matching Hero's ready unit, then **SPECIAL ORDER**. A basic use can instead command another ready friendly unit and grants none of the special effect. Special orders spend one card and one Play, activate the assigned unit and retain the usual optional actions, Damage Control, responses and terrain. The right panel displays active effects.

Used cards remain in a public once-per-scenario ledger. Opponent hands, normal draw-pile order and authoritative RNG remain hidden. Public Hero identities and their granted command registry are visible, as their presence is already known from fleet setup. Mid-order and post-roll saves retain the Heroic card, effect, spending and continuation. Protocol `fleet-cards-v11` rejects older clients.

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
| Veteran Commander | Corvette | 2 | 2 | 1 | Cool Under Pressure 1; Strike Command: self/adjacent allies +1 attack die against fighters/corvettes/platforms |
| Launch Commander | Carrier | 3 | 2 | 1 | Cool Under Pressure 1; allied strike craft within 2 hexes +1 attack die; mandatory capital tab |
| Recon Commander | Corvette | 2 | 2 | 1 | Corrected Cool Under Pressure 2; Fastest in the Fleet: +1 Afterburners level; no old Ace Pilot 2 bonus |

These printed Hand/Play values contribute through the existing faction-plus-leader budget rules. No statistics are mirrored to an unpictured faction counterpart. Multiple eligible defensive auras add independently; they grant dice only to defensive fire, not ordinary attacks. Lead by Example and Best of the Best add separate Evasive levels.

Staff requires an existing non-Staff leader on the same compatible ship. A Hero is unique per fleet and does not grant permission to attach extra ordinary leaders. Force preparation and saves validate these constraints. Crew/leader transfer now lets the player select which attached leader moves; the source and destination must both remain valid. Moving a Hero changes its command's eligible unit without generating another card.

## Additional timing and assignment rules

Two-line commands spend one card/Play and activate both selected units at the start. The first line belongs to the Hero; the second requires the printed fighter/frigate class. The picker filters incompatible units. Either line may be omitted; the owner must still be in play to spend the card. Lead the Attack retains its first declared target across saves and responses; omitting/skipping that attack supplies no target for the second attack. Frigate Attack permits independent targets.

Patch removes its guaranteed Crew token before Medic rolls the remaining tokens, and before optional normal Damage Control. Medic uses its printed 5+ threshold independently of the ship's Success value. It triggers once on activation, even if optional Damage Control is skipped. Reactions and later actions/repair attempts in the same order do not trigger it again.

Lightning Strike requires completed movement immediately before its optional boarding step. Skipping Move/Lightning grants no boarding; actual Afterburners and engine conditions still govern Lightning. It retains the normal once-per-order boarding limit. The card's +2 dice stack with the owner's Marines 2 bonus. Attack auras stack for eligible living friendly sources using nearest-hull distance and target class. They do not use a missile launcher's class to boost defensive fire against the missile. Leader ability summaries appear in unit details and fleet preparation.

## Pilot and carrier orders

Supreme Pilot uses the corrected face: asteroids **and minefields** cause no entry attacks during its Move and Lightning Move. This is a scoped order permission, not a permanent effect or blanket damage immunity. It permits transit through live enemy units but still requires a free final hex and respects planets, drifting hulks, movement costs, engine damage and actual Afterburners. Ordinary enemy responses still occur at legal free positions; their saved remaining paths retain the permission. Paying for a basic order with the card grants no special movement. Activating while already in terrain retains the normal activation attack; the exception applies to moving through it.

Launch All Fighters assigns up to two different ready fighter units embarked on the living Hero's carrier. Each assigned fighter is activated when the order starts, and its line resolves sequentially. Either line may be omitted or skipped. Both lines together spend one card/Play; the carrier keeps its activation state and buffs. Each actual launch uses one Launch Tube for the round and requires functioning bays and a free adjacent exit. Assigning two lines does not guarantee a later exit or unused tube. A blocked second launch may be skipped. Terrain and arrival responses finish before that fighter's Lightning Move; death of the first fighter advances to the unlaunched second line. The same engine handles ordinary Ready to Launch. The bay picker filters by fighter class and matching Hero's carrier.

Launch Commander requires a capital profile with bays and Launch Tubes, rather than any capital ship. Strike Coordination filters the **attacking friendly unit's** class (fighter/corvette/platform) within two nearest-hull hexes; it does not boost its capital carrier. Veteran Commander's Strike Command instead filters the **enemy target's** strike-craft class at range one. Both use the existing additive aura calculation.

## Heroic reaction timing

Rapid Reaction is a Heroic interrupt, triggered by the printed enemy **Move**. It does not trigger on Lightning Move, Reinforce or ordinary Attack. Select its card and matching live on-board Recon Commander during a response, then **BEGIN HEROIC REACTION**. Both actions can be skipped; the Attack may target only the interrupted mover. The opponent may Counterintelligence before either action. Cancellation spends the Heroic card for the scenario without putting it into normal discard.

An already activated Hero can react. Neither declaration nor movement runs activation Damage Control, Medic or NewOrder buff removal, spends Play, or changes the Hero's activation state. Replacement repair in place of Attack is not granted. Terrain entry and ordinary enemy response windows still apply. Its once-per-scenario ledger is independent of the four normal per-round interrupt types. It can still pay for an ordinary/basic order or initiative, without this reaction sequence.

The enemy's paid order/response, movement budget and paused pose are saved separately. A reaction Move can require the enemy to select its remaining route afterwards, preserving spent movement. Nested ordinary Move reactions restore the Hero's sequence first, then the original order. Death of the original mover cancels its remaining actions. Public registry/used-ledger data determine whether a Heroic window is offered, independently of hidden hands.

## Source priority and limits

Use the corrected five-card leader/command photos, the Taiidan original Master Engineer face, Campaign Expansion p.7, Errata 1.7 and the supplied mini-Euro replacement catalogue. Corrected Best of the Best lasts until a **new order**, retaining Evading through Focus Fire/Crew Damage. Its marker is distinct from the normal Evasive buff. Corrected Prepare to Be Boarded ends on a new order, Crew Damage or Focus Fire. Brace/Lockdown use their existing normal removal rules. Errata 1.7 places the relevant Heroic start effects before the action sequence. I Can Fix It removes one complete selected token permanently and does not replace normal activation Damage Control. Staff's original “Crew Card” wording means “Leader Card.”

The remaining photographed Heroic type is Planetary Overwatch. Its Power Attack needs real planet/facility ownership and a Battery Coordinator attached to the firing facility. That infrastructure remains implementation work; its face and owner have already been received. A generic ship or station is not substituted for this planet requirement. Hunter does not receive the unrelated Hunter unit ability (Errata 1.7). Their received faces are recorded in [CARD_INPUTS.md](CARD_INPUTS.md). Upgrades, facilities, remaining Heroic profiles, campaign purchases and the full official solo command-bank policy are not supplied by these sets. Artwork remains optional and no source photographs are distributed.
