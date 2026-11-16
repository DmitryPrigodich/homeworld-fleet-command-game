# Card inputs and mechanics priorities

Priority agreed on 2026-11-12: finish shared mechanics before additional published scenarios or a map editor. This audit distinguishes missing implementations from missing source text. A card absent from code is not necessarily absent from every book illustration; use readable printed examples before requesting redundant input.

## Confirmed base-deck inventory

The supplied `Compete-Component-Catalogue-for-Homeworld.pdf`, command-card section, lists **20 unique types / 54 physical cards**. These counts are already available; photographs are needed for full face text and action icons, not to guess deck quantities. Replacement catalogues describe corrections rather than a complete rules face for every card.

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

The playable training deck still has 24 instances. The inventory above is not a claim that the full 54-card deck or its effects already works. Card types, deck copies, action rows and explicit constraints now come from `content/base/content.json`; neither `SpecialOrders` nor `InterruptRules` interprets instance number ranges. Additional effect handlers and verified faces are still needed before populating the full published deck. Artwork remains optional and independent of mechanics.

## Corrected-card photos received on 2026-11-12

The user supplied two sheet photographs, identified as the latest gameplay-fix packs. Use these faces in preference to older versions of the corresponding cards. This does not establish that every card in earlier packs has been replaced. The visible pack C/D notices specify replacement scopes; retain faction/version distinctions. Original images are private local reference inputs, not distributable game assets.

Readability: titles, main prose, costs, Hand/Play and colored assignment tabs are legible after rotating. Tiny weapon/ability symbols need individual crop inspection and comparison with the rulebook legend before committing exact numeric profiles. No complete-sheet retake is currently needed. Cards behind the exposed faces cannot be inventoried.

- Ordinary command faces: **Deploy Engineers**, **Target Weapons**. Source availability does not mean their mechanics are already implemented.
- Heroic command faces: **Supreme Pilot**, **Power and Precision**, **Prepare to Be Boarded**, **Fire and Maneuver**, **Best of the Best**.
- Visible leader names include Fighter Ace, Recon Commander, Lord Commander, Station Overseer, Strategist, Hot Shot Pilot, Chief of Staff, Orbital Co-ordinator/Coordinator, Intelligence Officer and Point Defender. Blue/red counterparts are separate profiles to verify, not assumed duplicates.
- Visible upgrades include Advanced Point Defense, Upgraded Thrusters, Thicker Armor, Hangar Bay, Large Hangar Bay, Hull Defense Guns, Raiders and Beast Mode (with an Exhausted section).
- Planet modules: Orbital Defense Control, Star Port, Ion Batteries and Missile Defense.

Still needed for pending base command mechanics: **Interrupt Move, Interrupt Repairs, Interrupt Counterintelligence**, plus **Pincer Attack, Intercept, Hit and Run, Ship Assault, Ready to Launch, Weapons to Maximum, Hammer and Anvil, Brace For Impact, Evasive Pattern Delta, Repel Boarders, Target Engines, Target Bays**. All Ahead Full, Stay In Formation and Interrupt Attack already have book references; request new faces only if those cards were also revised. Full ship unit profiles and unpictured leaders/expansion cards remain separate inputs.

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

This batch improves the available references; it does not supply the remaining ordinary command faces or any Interrupt faces listed above, and does not by itself complete the published deck. Source intake is separate from implementing and testing the effects.

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
| 7 / Pirate-related leaders | Tactical Adjunct (Frigate), Cut-throat (Corvette Unit), Lord Commander (Super Capital Ship) |

The final sheet contains leader faces with cost/Hand/Play and assignment classes, not ship movement, hull or weapon profiles. Do not interpret its category label as supplied pirate ship statistics. Only exposed fronts are inventoried; partly visible facilities beside the Heroic sheet and cards under stacks are not additional verified faces/copy counts.

Version differences requiring explicit reconciliation with the corrected packs:

- Recon Commander here has **Ace Pilot 2**; the previously supplied corrected face has **Cool Under Pressure 2**. This is a mechanical difference, not just wording. Use the known corrected face for that profile.
- Staff text here requires an existing **Crew Card**, whereas the corrected Chief of Staff/Intelligence Officer faces say **Leader Card**. Do not silently treat these as separate new requirements or merge the wordings.
- Hangar Bay here permits a Super Capital Ship **or Frigate**, and its capacity prose lacks the corrected face's explicit additional-capacity wording. Use the corrected assignment/capacity profile where its replacement scope applies.
- Facilities also differ: Orbital Defence Control on this sheet lacks the explicit Launch Tubes line shown on the corrected face. Corrected text remains the reference for the replaced profile.
- Evasion/Evasive terminology and weapon/action symbols vary between faces. Reconcile with the rulebook legend and applicable corrected card rather than choosing a spelling or symbol by photo date.

The 14 Heroic faces include two **Heroic Interrupts**: **Rapid Reaction** (Recon Commander) and **Planetary Overwatch** (Battery Coordinator). These are newly available reference faces; they do not replace the missing ordinary **Interrupt Move**, **Interrupt Repairs** or **Interrupt Counterintelligence**. Other owner bindings visible on the sheet include Medical Chief / Patch 'Em Up, Master Engineer / I Can Fix It, Launch Commander / Launch All Fighters, Grizzled Veteran / Lightning Strike, Fighter Ace / Lead the Attack, Veteran Captain / Frigate Attack and Hunter / No Escape. Do not assume unpictured owner profiles from their command titles.

Titles and main prose are readable. Small inline weapon/ability icons and the denser Heroic text need individual close inspection before exact transcription. The new names expand the reference inventory, not implemented gameplay coverage. The missing base-command list above remains applicable.

## Useful photo batches

1. **Three remaining Interrupt faces:** Move, Repairs and Counterintelligence. Exact trigger windows, legal actors/targets, cancellation and continuation wording matter.
2. **Remaining special-order, buff and debuff faces:** the 12 pending non-interrupt types not supplied above. Deploy Engineers and Target Weapons have now been photographed. Include the full action rows, icons, restrictions and duration text. One image per distinct face is sufficient; duplicate physical copies are unnecessary. Readable illustrations already supplied in the rulebooks can cover some entries.
3. **Ships and leaders, per faction:** full statistics, weapon rows/arcs, ability values, costs, leader Hand/Play and colored tabs, including free assigned units. Interceptor and Wing/Squadron Leader examples on US Digital p.32 are already transcribed; most other profiles remain unverified.
4. **Expansion cards later:** upgrades, heroes, heroic commands, facilities and added units. These are a separate batch from the 54-card base deck.

Full faces with legible text and symbols are more useful than cropped prose alone. Use the errata and replacement catalogue to reconcile versions; do not infer a missing ability or an action sequence solely from a title. Card photographs are private reference inputs; no artwork is needed to make textual cards playable.

## Work that can continue from the books

- Completed: data-defined card types, deck counts, action rows and explicit supported effect handlers. Extend the existing actions when verified texts require it, without a speculative general scripting system.
- Complete shared timing and continuation: multiple order lines, optional actions, self/enemy-turn interrupt opportunities, per-type limits and saved response state. Full card-specific triggers still require their actual wording.
- Audit and implement neutral-unit/object interactions and artifact/objective handling as shared mechanics, independently of published map layouts.
- Implement Campaign Expansion Cloaking/Scanning, planets/facilities/orbital attacks and campaign purchasing/attachment lifecycles from the supplied rules, then attach verified card profiles. Individual hero/upgrade effects require card text.
- Implement the official solo/co-op command-bank and threat/selection procedures from the supplied guide. The current heuristic AI and shared-fleet networking do not implement those procedures.

For each implemented mechanic, retain command validation, previews where relevant, immutable save/replay, private multiplayer views, usable UI and EN/RU coverage. The editor and additional scenario packs follow this mechanics pass.
