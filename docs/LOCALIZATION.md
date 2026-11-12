# Display languages

Open **ОПЦИИ → ЯЗЫК / LANGUAGE** or **OPTIONS → LANGUAGE**. English and Russian are included; the choice is stored in `.local/settings.json` as `language`. Existing settings default to `ru`. Menus, battlefield text, tooltips, choices and the event log use the selected language. Editable player names and filesystem paths retain their entered values.

Language is a local presentation preference. Battle definitions, unit/card IDs, saved progress, RNG, original log entries and content hashes do not change. An English client can join a Russian host; each renders the same authoritative messages in its own language. Default Godot debug/exception diagnostics and names supplied by a mod without translations may retain their original text.

## Adding a language

Place `<code>.json` under `.local/locales`, or a mod's `locales` directory. The included catalogs live in `content/locales`. Use two/three lowercase letters, optionally followed by a region, for example `de`, `fr` or `pt-BR`.

```json
{
  "code": "de",
  "name": "Deutsch",
  "fallback": "en",
  "messages": {
    "ОПЦИИ": "Optionen",
    "ВЫХОД": "Beenden",
    "Бой начался.": "Die Schlacht beginnt."
  }
}
```

Restart or reload content after adding a file; the language then appears in the picker. Missing entries use its fallback. The Russian source text is the final fallback; an unknown saved language also selects Russian. Fallback cycles are rejected. Additional packs can override an existing language by using the same code. Precedence is base catalogs, enabled mods, then local catalogs. Locale files do not require changes to the gameplay mod manifest or content hash.

## Translating messages

Copy source keys from the Russian catalog. Entries include complete messages and the literal fragments used around dynamic numbers/names. Preserve useful spaces, punctuation, symbols and `\n` line breaks in the translations. A full message has priority; composed messages use the longest matching phrase with letter boundaries. Matching is case-insensitive, and uppercase headings stay uppercase. Original source strings are retained per UI control so switching languages does not attempt to reverse-translate already rendered English text.

Supply entries for a mod's names, briefing, objective/event names and any extra visible phrases. A minimal pack may deliberately fall back to English while its translations are completed. Personal names and paths should not be catalog keys. Cosmetic card styles and language packs remain independent.

Core regression checks cover every included Russian catalog entry in English and a third-language fallback. Godot checks exercise the actual picker, persisted settings, menu screens, a Cyrillic player-name field and mid-battle EN → RU log switching. The network scenario uses different host/client languages while preserving card privacy and saved scenario progress.
