<div align="center">

[![Superfighters Deluxe Logo](https://raw.githubusercontent.com/MythoFame/.github/refs/heads/master/assets/SFD_titleLoop.gif)](https://store.steampowered.com/app/855860)

# Superfighters Deluxe Map Translation Project

Community-driven, open-database translations for every Superfighters Deluxe map

[![GitHub License](https://img.shields.io/github/license/MythoFame/SFD.Scripting.MapTranslationProject)](LICENSE)

</div>

A script extension that lets you play any Superfighters Deluxe map in your language. It automatically replaces dialogue lines, speaker names, in-world text objects, and popup messages with translations from an open, versioned database. No map editing required.

Each map is identified by its original GUID, and translations are bundled directly into the script at build time via a code generator. Pick a language once and the script applies it whenever you load a translated map.

Contributions are welcome: anyone can open a map in the editor and add entries to the shared database.

## ⚙️ Commands

| Command | Description |
|---------|-------------|
|  `LANG [lang\|original]` | Chooses the translation language. Provide a [language code](db/languages.json) to translate on the next map load, or `original` to keep the original text. Called without arguments, it lists all available languages. Host only. |

Required options are shown with <>, optional parameters are shown with [].

## 🖊 Translations

All translations live in [`db/`](db/). Each map has its own directory, named after its hyphenated, lowercase original GUID.

```
db/
├── languages.json                  # supported language codes + display names
└── maps/<MapOriginalGUID>/
    ├── strings.tsv                 # original,key,kind  (canonical source texts)
    └── <lang>.tsv                  # key,translation    (one per language, blanks = untranslated)
```

Supported `kind` values are `dialogue-text`, `dialogue-name`, `text`, and `popup`. Any other file in `db/maps/<GUID>/` (e.g. README.md) is silently ignored.

### Adding a new map or string

1. Open the map in the map editor.
2. Copy the exact in-game strings into `strings.tsv` under columns `original,key,kind`. Invent a stable, human-readable key for each entry.
3. Create or edit `<lang>.tsv` files; each row is `key,translation`. Leave `translation` blank for untranslated entries.

Tables are tab-separated: fields never need quoting, so commas and quotes in dialogue text work as-is. Within a field, write `\n` for a line break, `\t` for a tab, `\r` for a carriage return, and `\\` for a literal backslash.
4. Run validation, regenerate the script data, and build:

```sh
just validate-translations   # lint + coverage, exits non-zero on errors
just generate-translations   # db → Translation/Data.generated.cs

just generate-script         # compile + welds to SFD.Scripting.MapTranslationProject.txt
```

### Quality checks

The validate step reports overall coverage, hash collisions, placeholder mismatches, and length advisories.
