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
| Deploy Engineers | 2 | Pending |
| Weapons to Maximum | 6 | Pending; weapon power parameters exist, granting order does not |
| Hammer and Anvil | 2 | Pending |
| Brace For Impact | 3 | Effect state exists; granting card pending |
| Evasive Pattern Delta | 3 | Effect state exists; granting card pending |
| Repel Boarders | 2 | Effect state exists; granting card pending |
| Target Engines | 3 | Crippled state exists; granting card pending |
| Target Weapons | 3 | Crippled state exists; granting card pending |
| Target Bays | 2 | Crippled state exists; granting card pending |
| Interrupt Attack | 3 | Implemented |
| Interrupt Move | 2 | Pending |
| Interrupt Repairs | 3 | Pending |
| Interrupt Counterintelligence | 2 | Pending |

The playable training deck still has 24 instances. The inventory above is not a claim that the full 54-card deck or its effects already works. `SpecialOrders` and `InterruptRules` currently recognize training instance ranges; replace this coupling with explicit card definitions and known action/effect handlers before populating the full published deck. Artwork remains optional and independent of mechanics.

## Useful photo batches

1. **Three remaining Interrupt faces:** Move, Repairs and Counterintelligence. Exact trigger windows, legal actors/targets, cancellation and continuation wording matter.
2. **Remaining special-order, buff and debuff faces:** the 14 pending non-interrupt types above. Include the full action rows, icons, restrictions and duration text. One image per distinct face is sufficient; duplicate physical copies are unnecessary. Readable illustrations already supplied in the rulebooks can cover some entries.
3. **Ships and leaders, per faction:** full statistics, weapon rows/arcs, ability values, costs, leader Hand/Play and colored tabs, including free assigned units. Interceptor and Wing/Squadron Leader examples on US Digital p.32 are already transcribed; most other profiles remain unverified.
4. **Expansion cards later:** upgrades, heroes, heroic commands, facilities and added units. These are a separate batch from the 54-card base deck.

Full faces with legible text and symbols are more useful than cropped prose alone. Use the errata and replacement catalogue to reconcile versions; do not infer a missing ability or an action sequence solely from a title. Card photographs are private reference inputs; no artwork is needed to make textual cards playable.

## Work that can continue from the books

- Replace hard-coded training card ID ranges with data-defined card types, deck counts, action rows and explicit supported effect handlers. Build on the already supported actions rather than a speculative general scripting system.
- Complete shared timing and continuation: multiple order lines, optional actions, self/enemy-turn interrupt opportunities, per-type limits and saved response state. Full card-specific triggers still require their actual wording.
- Audit and implement neutral-unit/object interactions and artifact/objective handling as shared mechanics, independently of published map layouts.
- Implement Campaign Expansion Cloaking/Scanning, planets/facilities/orbital attacks and campaign purchasing/attachment lifecycles from the supplied rules, then attach verified card profiles. Individual hero/upgrade effects require card text.
- Implement the official solo/co-op command-bank and threat/selection procedures from the supplied guide. The current heuristic AI and shared-fleet networking do not implement those procedures.

For each implemented mechanic, retain command validation, previews where relevant, immutable save/replay, private multiplayer views, usable UI and EN/RU coverage. The editor and additional scenario packs follow this mechanics pass.
