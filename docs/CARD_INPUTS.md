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

Costs, model counts and main statistic columns are readable; small weapon symbols and dense prose still need close transcription against the legend/errata. Ship art is reference material, not a distributed game asset. These Taiidan faces do not establish corresponding Kushan, Kadeshi or Turanic statistics; see subsequent batches for separately supplied profiles.

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

## Kushan ship originals and latest update received on 2026-11-17

Four further photographs were explicitly supplied as Kushan originals plus the latest update. Retain sheets 1-3 as the original profiles; sheet 4 supersedes the nine pictured ship types only. The files, upright inspection copies and SHA-256 provenance are retained privately. Do not derive one faction's values or revision changes by mirroring the other faction.

| Sheet | Visible ship profiles |
| --- | --- |
| 1 / Originals | Scout, Attack Bomber, Defender, Salvage Corvette, Interceptor, Cloaked Fighter, Repair Corvette, Light Corvette; cropped Heavy Corvette and Multi-gun Corvette at the right edge |
| 2 / Originals | Heavy Corvette, Minelayer Corvette, Ion Cannon Frigate, Carrier, Multi-gun Corvette, Assault Frigate, Drone Frigate, Destroyer; fragments of preceding profiles at the left edge |
| 3 / Originals | Missile Destroyer, Heavy Cruiser, Mothership |
| 4 / Latest update | Attack Bomber, Minelayer Corvette, Drone Frigate, Missile Destroyer, Carrier, Multi-gun Corvette, Assault Frigate, Destroyer, Heavy Cruiser |

This supplies **19 distinct Kushan ship types and nine replacement faces**. Fully exposed profiles on sheet 2 cover the right-edge crops on sheet 1. Partly visible cards under stacks are not additional verified types/copy counts. This is an intake count, not a claim that every faction/expansion ship is present or implemented.

Revision checks and faction distinctions:

- Minelayer Corvette changes from **2 to 3 models**; latest mine placement excludes hexes containing a unit or terrain feature.
- Attack Bomber's printed bonus against the indicated larger target classes changes from **+2 to +1 War Dice**. Confirm target-class symbols against the legend when transcribing; retain the entire latest weapon row.
- Kushan Assault Frigate's original Large Projectile Cannon already prints **3 War Dice**; the latest also prints 3. Do not copy the Taiidan original-to-update 1-to-3 change into the Kushan revision history.
- Kushan Multi-gun Corvette's original Defender already refers to adjacent allied units without the Taiidan original's explicit two-recipient limit. Preserve each faction's actual text.
- Heavy Cruiser's latest Twin Turrets explicitly restricts the paid additional attack to **once per turn**; Missile Destroyer's latest Target Lock explicitly excludes both the Evasive unit ability and buff.
- Drone Frigate's latest text/symbol arrangement around its Evasive weapon ability and incoming drone defense needs close interpretation against the legend. Do not turn a conditional weapon ability into an unconditional ship-wide statistic.
- Carrier's latest factory, assigned Interceptor and Launch Tubes wording comes from its own latest face. Kushan **Mothership** is on original sheet 3 and is not replaced by the Taiidan **Imperial Flagship**, nor by an unpictured supposed replacement.

Main statistics/costs, model counts, tabs and prose are readable. Small weapon/action symbols and the denser capital-ship text still need close transcription/validation before gameplay data is changed. This submission is a source intake, not completion of the corresponding mechanics.

## Neutral objects, Kadeshi/Turanic units and faction cards received on 2026-11-17

Eight photographs were supplied; the user explicitly identifies **the last two sheets as updates**. Preserve sheets 1-6 separately from the replacements on sheets 7-8. Source images, upright inspection copies and SHA-256 provenance are retained privately. The faction assignments confirmed earlier remain in force: Kadeshi and Turanic are distinct, not one combined pirate faction.

| Sheet | Visible fronts |
| --- | --- |
| 1 / Neutral originals | Weapons Platform, Research Facility, Space Station, Cryo Tray, Shipyard, Military Outpost, Gun Platform, Minefield; cropped Asteroid Field and Industrial World |
| 2 / Neutral originals | Asteroid Field, Established Colony, New Settlement, Prison Station, Industrial World, Civilised World, Field Generator, Nebula; fragments of preceding cards |
| 3 / Kadeshi originals | Swarmer, Advanced Swarmer, Multi-beam Frigate, Mothership, Fuel Pod |
| 4 / Turanic originals | Bandit Interceptor, Thief Heavy Corvette, Lord Carrier, Brigand Missile Corvette, Dagger Ion Array Frigate |
| 5 / Faction Ready faces and Bentusi | Kadesh, Taiidan, Turanic Raiders, Kushan faction abilities; Bentusi Ship unit profile |
| 6 / Faction Used faces | Ability Used reverse faces for all four faction cards |
| 7 / Neutral update | Space Station, Gun Platform, Field Generator, Asteroid Field, Shipyard, Prison Station, Minefield, Nebula |
| 8 / Kadeshi/Turanic update | Kadeshi Mothership, Multi-beam Frigate, Advanced Swarmer, Fuel Pod, Swarmer; Turanic Lord Carrier and Brigand Missile Corvette |

The originals expose **17 distinct neutral/object/Bentusi profiles**, **five Kadeshi ship types**, **five Turanic ship types**, and all four faction ability cards on both Ready/Used sides. Sheet 7 supplies eight replacement faces; sheet 8 supplies seven. These are source counts, not a claim of implemented coverage or a complete inventory of every expansion.

Preserve these distinctions during transcription:

- Weapons Platform and Gun Platform are separate profiles, not two names for the same weapons/price. The latest Gun Platform explicitly describes its two-platform/one-platform token sides.
- Field Generator's latest hyperspace restriction explicitly affects **enemy** capital ships. Do not copy the original unrestricted wording into the active profile.
- Latest Minefield text explicitly allows defensive fire and includes causing a unit to take Focus Fire in its reduction trigger. Keep the complete reduction/removal timing alongside the printed 5/3 token sides.
- Updated stations carry their own factory, purchased-unit deployment and Launch Tubes text. Cryo Tray/Weapons Platform damage flips are separate from ordinary ship model/crew losses.
- The latest Kadeshi Swarmer/Advanced Swarmer Limited Fuel checks whether the unit starts **an action** within three hexes of a Fuel Pod; the original says the unit starts within three hexes. Resolve exact timing from the latest face/rules rather than substituting round or order timing.
- Latest Kadeshi Multi-beam Frigate/Fuel Pod/Mothership explicitly show crew values 2/2/6. Preserve these updated values instead of treating missing/dash original entries as perpetual boarding immunity. Kadeshi Mothership is distinct from the Kushan Mothership and Taiidan Imperial Flagship.
- Latest Lord Carrier Power Drain and Brigand Target Lock must be transcribed from their updated faces. Brigand's latest exclusion explicitly covers the Evasive unit ability and buff.
- Faction cards have printed cost 0, Hand 2 and Play 1. Their ability text and Ready/Used lifecycle are distinct from leader attachment rules. The four Ready abilities are Fuelled Up, Ferocious Attack, Ferocious Raiders and Redundant Systems. Preserve the Taiidan Valor interaction, Kushan token-removal options, Kadeshi order modification and Turanic boarding reroll as separate mechanics.

## Intake completeness check after the final batch

The photographed command deck, 19 Taiidan and 19 Kushan types, Kadeshi/Turanic ships, neutral profiles, faction cards, facilities, upgrades and Heroic families now provide substantial rule/profile sources. The **clear remaining photo gap is the ordinary Core Box leader cards for both Kushan and Taiidan**, as listed in the supplied component catalogue:

**Squadron Leader, Wing Commander, Marine Commander, Marine Captain, Commander, Captain, Commodore, Carrier Captain, Vice Admiral, Admiral, Station Commander.**

These are eleven distinct names per faction. Book examples already supply some profiles (including Wing Commander and Squadron Leader), but the ordinary leader faces have not appeared among the received photo batches; most remain unverified for complete cost/Hand/Play, compatible unit classes, tabs and effects. Do not confuse Captain with Veteran Captain, Commander with Veteran Commander, or these base leaders with the expansion Heroic/Staff faces. One exposed face per distinct name and faction is sufficient; duplicate physical copies are unnecessary. This is a request for source verification, not a claim that all 22 profiles are absent from every book example.

Do not declare every expansion complete solely from this check: the available component catalogue provides the Core Box list, but its Campaign Expansion section ends before a complete card inventory. Missing artwork/backs are not blockers. Ask for targeted crops only if a particular symbol or line remains unresolved; do not request wholesale rephotographing of already supplied sets. Implementing/transcribing all received effects is now the main outstanding work, independently of this specific leader-photo gap.

## Useful photo batches

1. **Ordinary Core Box leaders:** the eleven names per faction listed above. Verify full cost/Hand/Play, assignment restrictions, tabs and abilities against the actual faces; retain book examples already transcribed.
2. **Other specifically identified missing profiles:** only names absent from the received batches. The 19 Taiidan, 19 Kushan, five Kadeshi and five Turanic ship types above are available, as are the photographed Heroic owners; do not keep requesting them as missing. Any additional expansion inventory must be checked separately.
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
