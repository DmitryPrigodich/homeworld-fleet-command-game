# Card inputs and mechanics priorities

Priority agreed on 2026-11-12: finish shared mechanics before additional published scenarios or a map editor. This audit distinguishes missing implementations from missing source text. A card absent from code is not necessarily absent from every book illustration; use readable printed examples before requesting redundant input.

## Confirmed base-deck inventory

The supplied `Compete-Component-Catalogue-for-Homeworld.pdf`, command-card section, lists **20 unique types / 54 physical cards**. As of the final command-sheet submission on 2026-11-16, all 20 types have photographed faces. Copy counts come from the catalogue, not photographed stack thickness. The table below tracks implementation, separately from source availability. Replacement catalogues and previously supplied corrected faces still determine applicable revisions.

| Type | Copies | Implementation |
| --- | ---: | --- |
| All Ahead Full | 2 | Implemented |
| Stay In Formation | 3 | Implemented |
| Pincer Attack | 3 | Pending |
| Intercept | 3 | Pending |
| Hit and Run | 3 | Pending |
| Ship Assault | 2 | Pending |
| Ready to Launch | 2 | Pending |
| Deploy Engineers | 2 | Pending; corrected face supplied in photo batch 2026-11-12 |
| Weapons to Maximum | 6 | Pending; weapon power parameters exist, granting order does not |
| Hammer and Anvil | 2 | Pending |
| Brace For Impact | 3 | Effect state exists; granting card pending |
| Evasive Pattern Delta | 3 | Effect state exists; granting card pending |
| Repel Boarders | 2 | Effect state exists; granting card pending |
| Target Engines | 3 | Crippled state exists; granting card pending |
| Target Weapons | 3 | Crippled state exists; corrected granting card supplied in photo batch 2026-11-12 |
| Target Bays | 2 | Crippled state exists; granting card pending |
| Interrupt Attack | 3 | Implemented |
| Interrupt Move | 2 | Pending |
| Interrupt Repairs | 3 | Pending |
| Interrupt Counterintelligence | 2 | Pending |

The playable training deck still has 24 instances. The inventory above is not a claim that the full 54-card deck or its effects already works. Card types, deck copies, action rows and explicit constraints now come from `content/base/content.json`; neither `SpecialOrders` nor `InterruptRules` interprets instance number ranges. Exact transcription, revision reconciliation and additional effect handlers are still needed before populating the full published deck. Artwork remains optional and independent of mechanics.

## Corrected-card photos received on 2026-11-12

The user supplied two sheet photographs, identified as the latest gameplay-fix packs. Use these faces in preference to older versions of the corresponding cards. This does not establish that every card in earlier packs has been replaced. The visible pack C/D notices specify replacement scopes; retain faction/version distinctions. Original images are private local reference inputs, not distributable game assets.

Readability: titles, main prose, costs, Hand/Play and colored assignment tabs are legible after rotating. Tiny weapon/ability symbols need individual crop inspection and comparison with the rulebook legend before committing exact numeric profiles. No complete-sheet retake is currently needed. Cards behind the exposed faces cannot be inventoried.

- Ordinary command faces: **Deploy Engineers**, **Target Weapons**. Source availability does not mean their mechanics are already implemented.
- Heroic command faces: **Supreme Pilot**, **Power and Precision**, **Prepare to Be Boarded**, **Fire and Maneuver**, **Best of the Best**.
- Visible leader names include Fighter Ace, Recon Commander, Lord Commander, Station Overseer, Strategist, Hot Shot Pilot, Chief of Staff, Orbital Co-ordinator/Coordinator, Intelligence Officer and Point Defender. Blue/red counterparts are separate profiles to verify, not assumed duplicates.
- Visible upgrades include Advanced Point Defense, Upgraded Thrusters, Thicker Armor, Hangar Bay, Large Hangar Bay, Hull Defense Guns, Raiders and Beast Mode (with an Exhausted section).
- Planet modules: Orbital Defense Control, Star Port, Ion Batteries and Missile Defense.

At this point in intake, faces were still missing for **Interrupt Move, Interrupt Repairs, Interrupt Counterintelligence**, plus **Pincer Attack, Intercept, Hit and Run, Ship Assault, Ready to Launch, Weapons to Maximum, Hammer and Anvil, Brace For Impact, Evasive Pattern Delta, Repel Boarders, Target Engines, Target Bays**. The final command sheet below supplies all of these; their implementations remain pending. Full ship unit profiles and unpictured leaders/expansion cards remain separate inputs.

## Second small-card photo batch received on 2026-11-16

Six additional photographs cover the same corrected small-card families in separate groups. The user calls these photos "2.0" and says originals will follow; this is a photo-batch label, not evidence of a new numbered rules edition. Retain both batches and compare corresponding faces before changing a gameplay profile. The files, an SHA-256 source manifest and upright inspection copies are retained only in the ignored local reference directory.

| Photo | Visible fronts |
| --- | --- |
| 1 / Upgrades | Red and blue Large Hangar Bay and Hangar Bay; red Beast Mode; blue Advanced Point Defense, Hull Defense Guns, Raiders, Upgraded Thrusters and Thicker Armor |
| 2 / Heroic leaders | Hot Shot Pilot, Strategist, Station Overseer, Recon Commander, Fighter Ace |
| 3 / Leaders | Red and blue Orbital Co-ordinator, Point Defender and Chief of Staff; blue Intelligence Officer |
| 4 / Facilities | Star Port, Orbital Defense Control, Ion Batteries, Missile Defense |
| 5 / Heroic commands | Supreme Pilot, Best of the Best, Prepare to Be Boarded, Power and Precision, Fire and Maneuver |
| 6 / Commands and carrier-related leader | Target Weapons, Deploy Engineers, Lord Commander |

The category labels follow the user's grouping. They do not establish extra mechanics absent from the card faces/rules. Only the exposed front of a stack is inventoried; stack thickness does not establish copy counts. The visible SKU card identifies the pack but is not a gameplay card.

Main prose, leader cost/Hand/Play, tabs, action rows and restrictions are usable references. Small inline icons and weapon rows remain subject to close inspection and the expected originals. Preserve the blue/red distinctions, Staff assignment prerequisites, additional hangar capacity/Launch Tubes, Heroic Action ownership and the ready/Exhausted behavior of Beast Mode when implementing these cards. Lord Commander's visible cost/Hand/Play are 4/5/3; it is a leader for a Super Capital Ship, not the Turanic carrier's unit/weapon profile. The user associates this group with that carrier; its full ship profile still needs a separate source.

This batch improves the available references; it does not itself supply the remaining ordinary command/Interrupt faces. Those were subsequently received on the final command sheet below. Source intake is separate from implementing and testing the effects.

## Faction-card batch received later on 2026-11-16

Seven more sheets were supplied with the labels Taiidan leaders/upgrades, Kushan upgrades/leaders, Facilities, Heroic commands and Pirate ships. Preserve these as a separate batch; later receipt does not establish a later printed revision. Original files, SHA-256 provenance and upright inspection copies are kept in the ignored local reference directory. No photo artwork is published.

| Photo | Visible fronts |
| --- | --- |
| 1 / Taiidan leaders | Raid Commander, Ion Controller, Orbital Co-ordinator, Chief of Staff, Point Defender |
| 2 / Taiidan upgrades | Beast Mode, Tritium Injection, Prow Ram, Rammer, Hangar Bay, Large Hangar Bay, Modified Barrels, Automated Repair, Cruise Missiles, Extra Thrusters, Marines |
| 3 / Kushan upgrades | EMP Bombs, Upgraded Thrusters, Large Hangar Bay, Thicker Armor, Precision Focus, Security Stations, Raiders, Med Bays, Advanced Point Defence, Guided Rounds, Hangar Bay, Hull Defence Guns |
| 4 / Kushan leaders | Raid Commander, Ion Controller, Recon Commander, Medical Chief, Chief of Staff, Point Defender, Fighter Ace, Station Overseer, Intelligence Officer, Grizzled Veteran, Strategist, Orbital Co-ordinator, Master of Arms |
| 5 / Facilities | Hardened Defenses, Orbital Defence Control, Minefield, Ion Batteries, Star Port, Missile Defence |
| 6 / Heroic commands | Patch 'Em Up, I Can Fix It, Rapid Reaction, Planetary Overwatch, Best of the Best, Launch All Fighters, Lightning Strike, Lead the Attack, Supreme Pilot, Frigate Attack, Prepare to Be Boarded, Fire and Maneuver, Power and Precision, No Escape |
| 7 / Kadeshi and Turanic leaders | Tactical Adjunct (Kadeshi; Frigate), Cut-throat (Turanic; Corvette Unit), Lord Commander (Turanic; Super Capital Ship) |

The user explicitly corrected the faction assignments: **Tactical Adjunct is Kadeshi; Cut-throat and Lord Commander are Turanic**. These identities override any earlier collective "pirate" shorthand. The final sheet contains leader faces with cost/Hand/Play and assignment classes, not ship movement, hull or weapon profiles. Do not interpret its category label as supplied pirate ship statistics. Only exposed fronts are inventoried; partly visible facilities beside the Heroic sheet and cards under stacks are not additional verified faces/copy counts.

Version differences requiring explicit reconciliation with the corrected packs:

- Recon Commander here has **Ace Pilot 2**; the previously supplied corrected face has **Cool Under Pressure 2**. This is a mechanical difference, not just wording. Use the known corrected face for that profile.
- Staff text here requires an existing **Crew Card**, whereas the corrected Chief of Staff/Intelligence Officer faces say **Leader Card**. Do not silently treat these as separate new requirements or merge the wordings.
- Hangar Bay here permits a Super Capital Ship **or Frigate**, and its capacity prose lacks the corrected face's explicit additional-capacity wording. Use the corrected assignment/capacity profile where its replacement scope applies.
- Facilities also differ: Orbital Defence Control on this sheet lacks the explicit Launch Tubes line shown on the corrected face. Corrected text remains the reference for the replaced profile.
- Evasion/Evasive terminology and weapon/action symbols vary between faces. Reconcile with the rulebook legend and applicable corrected card rather than choosing a spelling or symbol by photo date.

The 14 Heroic faces include two **Heroic Interrupts**: **Rapid Reaction** (Recon Commander) and **Planetary Overwatch** (Battery Coordinator). These are separate from ordinary **Interrupt Move**, **Interrupt Repairs** and **Interrupt Counterintelligence**, whose faces were subsequently received on the final command sheet. Other owner bindings visible on the sheet include Medical Chief / Patch 'Em Up, Master Engineer / I Can Fix It, Launch Commander / Launch All Fighters, Grizzled Veteran / Lightning Strike, Fighter Ace / Lead the Attack, Veteran Captain / Frigate Attack and Hunter / No Escape. Do not assume unpictured owner profiles from their command titles.

Titles and main prose are readable. Small inline weapon/ability icons and the denser Heroic text need individual close inspection before exact transcription. The new names expand the reference inventory, not implemented gameplay coverage.

## Complete base-command face sheet received on 2026-11-16

One final sheet supplies **all 20 base command types**, including all four ordinary Interrupt faces. The original image, an upright inspection copy and SHA-256 provenance manifest are retained locally. Inventory in photographed row order:

- Row 1: Interrupt Counterintelligence, Interrupt Move, Interrupt Attack, Interrupt Repairs, Ship Assault, Intercept, Stay In Formation.
- Row 2: Target Bays, Repel Boarders, Hammer and Anvil, Deploy Engineers, Ready to Launch, Pincer Attack, Hit and Run.
- Row 3: Weapons to Maximum, Target Weapons, Target Engines, Evasive Pattern Delta, Brace For Impact, All Ahead Full, plus a card back/basic-order legend.

This closes the missing-face request for the base command deck. The published 54-copy inventory is already known separately. It does not make the remaining effects implemented, nor establish a later card revision by receipt date. Preserve existing training content until published actions, choices, buffs/debuffs and interrupt continuations are implemented and tested against the actual faces.

Apply known source precedence: the previously supplied corrected **Deploy Engineers** and **Target Weapons** faces replace corresponding older faces where applicable. **All Ahead Full** on this sheet still prints a +1 ramming-die bonus; the supplied **Rulebook Errata 1.7**, All Ahead Full entry, explicitly removes it. Do not reintroduce that bonus from the photograph. Small inline icons, per-card choice/timing text and distinctions between a model, unit and unit type need close transcription before implementation. Blank prose panels on icon-only cards are not missing photographed text.

## Taiidan ship originals and latest update received on 2026-11-17

Four ship-profile photographs were supplied as Taiidan originals followed by the latest update. Treat sheets 1-3 as original references and sheet 4 as the latest replacement faces for its seven pictured types. Keep both revisions; do not apply the replacement label to unpictured ship types or infer that every printed ability is already implemented. Original images, upright inspection copies and SHA-256 provenance are retained locally, outside Git.

| Sheet | Visible ship profiles |
| --- | --- |
| 1 / Originals | Scout, Attack Bomber, Cloaked Fighter, Salvage Corvette, Interceptor, Defence Fighter, Repair Corvette, Light Corvette; partially cropped Heavy Corvette and Multi-gun Corvette at the right edge |
| 2 / Originals | Heavy Corvette, Minelayer Corvette, Ion Cannon Frigate, Carrier, Multi-gun Corvette, Assault Frigate, Field Frigate, Destroyer |
| 3 / Originals | Missile Destroyer, Heavy Cruiser, Imperial Flagship |
| 4 / Latest update | Multi-gun Corvette, Assault Frigate, Heavy Cruiser, Minelayer Corvette, Destroyer, Missile Destroyer, Imperial Flagship |

The batch exposes **19 distinct Taiidan ship types**. The two cropped original profiles on sheet 1 appear fully on sheet 2; no repeat of that whole sheet is currently needed. This count describes the received sources, not proof that every Taiidan ship/expansion profile has been supplied.

Confirmed revision checks to preserve when transcribing:

- Minelayer Corvette changes from **2 to 3 models**. The latest mine-placement text also restricts the adjacent hex to one without a unit or terrain feature.
- Assault Frigate's printed Large Projectile Cannon War Dice change from **1 to 3**. Keep its other weapon row and revised symbols as a coherent profile; do not combine old and new rows.
- Multi-gun Corvette's latest **Defender 1** text applies to adjacent allied units, whereas the original limits the bonus to up to two adjacent allied units. Preserve the explicit corrected wording when implementing target/recipient selection.
- Heavy Cruiser's latest Twin Turrets text explicitly limits the additional paid attack to **once per turn**.
- Missile Destroyer's latest Target Lock text explicitly covers both the Evasive unit ability and buff.
- Imperial Flagship's updated dense factory/launch/assigned-unit wording needs an individual close inspection before exact transcription. Its original, larger face remains useful for comparison, but does not override the latest face.

Costs, model counts and main statistic columns are readable; small weapon symbols and dense prose still need close transcription against the legend/errata. Ship art is reference material, not a distributed game asset. Availability of these profiles does not replace remaining Kushan, Kadeshi or Turanic ship inputs.

## Additional Taiidan Heroic leader sheet received on 2026-11-17

The user initially asks whether this set was already photographed and tentatively suggests an update, then confirms these are the missing original cards after comparison. Among previously received faces, **Hot Shot Pilot** was already supplied. The other six leader profiles are newly supplied: some names previously appeared only as owners printed on Heroic command cards, which did not establish their statistics.

| Leader | Assignment on this face | Cost | Hand | Play | Prior face availability |
| --- | --- | ---: | ---: | ---: | --- |
| Battery Coordinator | Planet | 3 | 3 | 2 | New |
| Veteran Captain | Frigate | 3 | 3 | 2 | New |
| Hunter | Super Capital Ship | 4 | 6 | 3 | New |
| Veteran Commander | Corvette | 2 | 2 | 1 | New |
| Launch Commander | Carrier | 3 | 2 | 1 | New |
| Hot Shot Pilot | Fighter Unit | 3 | 2 | 1 | Previously supplied; compare revision |
| Master Engineer | Station or Frigate | 2 | 2 | 1 | New |

Retain the image and SHA-256 provenance locally. The printed cost/Hand/Play and prose are readable; exact ability/icon transcription is still implementation work. Newly available owner profiles cover Planetary Overwatch, Frigate Attack, No Escape, Supreme Pilot, Launch All Fighters and I Can Fix It, respectively; Hot Shot Pilot / Best of the Best was already represented.

**Revision status: originals, as subsequently classified by the user.** The earlier tentative update label is superseded. This Hot Shot Pilot face says **Evasion**, whereas the previously supplied corrected face says **Evasive**; Master Engineer also uses the **Crew Card** Staff wording seen on other differing faces. Keep all seven as original references, with six newly supplied profiles and one repeated leader. Applicable corrected cards/errata retain precedence; receiving this original sheet later does not override them.

## Useful photo batches

1. **Remaining ships, per faction:** full statistics, weapon rows/arcs, ability values, costs and colored tabs, including free assigned units. The 19 Taiidan types and seven replacement faces above are now available; do not request them again as missing. Kushan, Kadeshi and Turanic ship profiles remain separate inputs; the pirate-related sheet supplies leaders, not ship profiles. Interceptor examples on US Digital p.32 are already transcribed. Photographed profiles still require exact transcription/validation before implementation.
2. **Unpictured leaders and Heroic owners:** full cost/Hand/Play, restrictions and abilities. Use the existing inventory to avoid requesting repeated faces; Wing/Squadron Leader book examples and photographed leaders are already available.
3. **Higher-quality originals or targeted crops:** only where an inline icon or dense text cannot be resolved from the supplied photos and legend. No repeated full base-command deck photo is currently needed.
4. **Other expansion faces:** only cards not present in the received upgrade/leader/facility/Heroic batches. Distinguish missing source profiles from pending implementations.

Full faces with legible text and symbols are more useful than cropped prose alone. Use the errata and replacement catalogue to reconcile versions; do not infer a missing ability or an action sequence solely from a title. Card photographs are private reference inputs; no artwork is needed to make textual cards playable.

## Work that can continue from the books

- Completed: data-defined card types, deck counts, action rows and explicit supported effect handlers. Extend the existing actions when verified texts require it, without a speculative general scripting system.
- Complete shared timing and continuation: multiple order lines, optional actions, self/enemy-turn interrupt opportunities, per-type limits and saved response state. Full card-specific triggers still require their actual wording.
- Audit and implement neutral-unit/object interactions and artifact/objective handling as shared mechanics, independently of published map layouts.
- Implement Campaign Expansion Cloaking/Scanning, planets/facilities/orbital attacks and campaign purchasing/attachment lifecycles from the supplied rules, then attach verified card profiles. Individual hero/upgrade effects require card text.
- Implement the official solo/co-op command-bank and threat/selection procedures from the supplied guide. The current heuristic AI and shared-fleet networking do not implement those procedures.

For each implemented mechanic, retain command validation, previews where relevant, immutable save/replay, private multiplayer views, usable UI and EN/RU coverage. The editor and additional scenario packs follow this mechanics pass.
