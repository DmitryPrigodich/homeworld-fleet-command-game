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
