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
|  `TRANSLATION_DUMP` | Dumps every dialogue, dialogue name, text object and popup on the current map into shared script storage. Host only. |

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

### Adding a new map (via dump)

1. Load the map in SFD and run `/TRANSLATION_DUMP` in chat.
2. Locate `Superfighters Deluxe/Cache/ScriptData/Shared/translationdump.txt` and copy it into the repository root as `translationdump.txt`.
3. Run `just process-dump`. The recipe strips the storage-format header, extracts the GUID, creates `db/maps/<GUID>/strings.tsv` if needed and appends only genuinely new rows: deduplicating both by placeholder key and by the pair `(original, kind)` so repeated texts on multiple objects only appear once.

### Adding strings manually or refining placeholders

1. Open `db/maps/<GUID>/strings.tsv`. Columns are `original,key,kind`; invent stable, human-readable keys for each entry and replace the generated `*.placeholder.*` placeholders where you can.
2. Create or edit `<lang>.tsv` files: each row is `key,translation`. Leave `translation` blank for untranslated entries.

Tables are tab-separated: fields never need quoting, so commas and quotes in dialogue text work as-is. Within a field, write `\n` for a line break, `\t` for a tab, `\r` for a carriage return, and `\\` for a literal backslash.

### Build and validation

After editing the database:

```sh
just validate-translations   # lint + coverage, exits non-zero on errors
just generate-translations   # db → Translation/Data.generated.cs

just generate-script         # compiles + welds to SFD.Scripting.MapTranslationProject.txt
```

### Quality checks

The validate step reports overall coverage, hash collisions, placeholder mismatches, and length advisories.
