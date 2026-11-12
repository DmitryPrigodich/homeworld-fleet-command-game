# Display languages

English is the source language in C# and shipped content. Its original strings are the shared keys in **every** language catalog, including `en.json`. Russian and additional languages translate those English keys. The English file supplies the default English captions and can override them without changing source code.

Open **OPTIONS → LANGUAGE** (or the corresponding Russian menu). English is the default for new settings; an explicitly saved language choice is retained. The choice is stored in `.local/settings.json` as `language`. Menus, battlefield text, tooltips, choices and the event log use the selected language. Editable player names and filesystem paths retain their entered values.

Language remains a local presentation preference. A language-file edit or selection does not change unit/card IDs, battle progress, RNG, source log records or gameplay hashes. English and Russian peers may share a match. Original log messages are written in English and translated only when displayed. Native language names in catalog metadata and deliberate Unicode test fixtures are independent of the source-language convention.

## Adding a language

Place `<code>.json` under `.local/locales`, or a mod's `locales` directory. Included catalogs live in `content/locales`. Use two/three lowercase letters, optionally followed by a region, for example `de`, `fr` or `pt-BR`.

```json
{
  "code": "de",
  "name": "Deutsch",
  "fallback": "en",
  "messages": {
    "OPTIONS": "Optionen",
    "EXIT": "Beenden",
    "Battle started.": "Die Schlacht beginnt."
  }
}
```

Click **RELOAD LANGUAGES** on the language screen after adding or editing a file. Restarting or reloading content also reloads catalogs. Missing entries use the configured fallback, then the English catalog, then the literal English source text. An unknown saved language selects English. Omitting `fallback` defaults it to `en`; the base English catalog uses an empty fallback. Fallback cycles are rejected.

Packs can override an existing language by using the same code. Precedence is base catalogs, enabled mods, then local catalogs. Files for a code merge rather than replace all other entries. Locale files need no gameplay manifest or hash changes.

## Editing English captions

English goes through the same catalog lookup as any other language. For example, a local `.local/locales/en.json` can contain:

```json
{
  "code": "en",
  "name": "English",
  "fallback": "",
  "messages": {
    "OPTIONS": "SETTINGS",
    "Battle started.": "Engagement started."
  }
}
```

After **RELOAD LANGUAGES**, the existing English source key `OPTIONS` displays as `SETTINGS`. No recompilation is needed. Russian, German and other catalogs still use `OPTIONS`, regardless of the current English display value. A missing translation in another language falls back to the edited English value.

Keep these canonical source keys stable when correcting wording in a catalog. Exact entries honor the supplied value and casing, including identity mappings such as `"OPTIONS": "OPTIONS"`. UI controls retain the original English source separately from their rendered text, so language switches and reloads do not translate an already renamed caption a second time.

## Translating composed messages

Copy source keys from `en.json`. Entries include complete messages and literal fragments around dynamic numbers and names. Preserve useful punctuation, symbols and `\n` line breaks. Exact whole-message entries have priority; otherwise composed messages use the longest matching translated phrases with letter boundaries. Phrase lookup can handle heading case variants. An unknown phrase remains readable English.

Supply English names, briefing and objective/event text in a mod's gameplay data, with translations in its `locales` directory. Personal names and paths should not be catalog keys. Debug/exception diagnostics and untranslated third-party content may retain their English source. Cosmetic card styles and language packs remain independent.

Regression checks verify English keys in both shipped catalogs, English fallback, a third language, exact English caption overrides and reloads without battle-state changes. Godot checks exercise the picker, persistence, a live English JSON rename, menu screens, a Cyrillic name field and mid-battle EN → RU log switching. The network scenario uses different host/client languages while preserving card privacy and scenario progress.
