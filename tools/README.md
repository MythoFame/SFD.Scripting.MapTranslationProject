<div align="center">

[![Superfighters Deluxe Logo](https://raw.githubusercontent.com/MythoFame/.github/refs/heads/master/assets/SFD_titleLoop.gif)](https://store.steampowered.com/app/855860)

# Superfighters Deluxe Script Tools

Script collection for Superfighters Deluxe development

[![GitHub License](https://img.shields.io/github/license/dsafxP/SFD.ScriptTools)](LICENSE)

</div>

While these tools are designed for use with [SFD.Templates](https://github.com/dsafxP/SFD.Templates), they can also be used as standalone components.

## 🛠️ Tools

### LibrarySetup

Locates the proprietary `SFD.GameScriptInterface.dll` somewhere on the system (checking a few well-known Steam install locations by default), then symlinks it (falling back to a copy if symlinking isn't possible) into the current directory's  `lib` folder.

The linked/copied file is also automatically added to `.gitignore` (creating the file if needed).

The script also keeps the SDK version in sync: it reads the DLL's product version and checks it against a `<RequiredGameSdkVersion>` field in the nearest `.csproj`. If the field is missing it is added with the current version; if the value does not match the installed DLL the script exits with an error. Pass `--force` to auto-update the field to the installed version.

```sh
Usage: SFD.ScriptTools.LibrarySetup.fsx [-f|--file <path-to-dll>] [-o|--output <output-path>] [-F|--force] [--dry-run] [-h|--help]

  -f, --file    Explicit path to SFD.GameScriptInterface.dll (skips auto-search)
  -o, --output  Output path for the symlink/copy (default: ./lib/SFD.GameScriptInterface.dll)
  -F, --force   On version mismatch, update <RequiredGameSdkVersion> to the installed DLL version
      --dry-run Run all checks without touching the filesystem; exit 1 if changes are needed
  -h, --help    Show this help message and exit
```

### ScriptGenerator

Welds together the `GameScript` partial-class bodies from a set of `*.cs` source files into a single, non-compilable `*.txt` "script" file, suitable for usage in Superfighters Deluxe.

For each input file, everything outside of the `GameScript` class (using directives, namespace declaration, and the class declaration itself) is stripped away; only what's declared *inside* the class body survives.

Since the welded output cannot contain `using` directives, the script checks each file that has a `GameScript` class for them and exits with an error listing any found. The only allowed using is `SFDGameScriptInterface`, which is already implicit in the output.

The remaining bodies are concatenated (each preceded by a comment noting its source file) and whitespace-normalized via Roslyn's Formatter before being written to disk.

```sh
Usage: SFD.ScriptTools.ScriptGenerator.fsx <file1.cs> [file2.cs ...] [-o|--output <path>] [-h|--help]

  <file.cs>...  One or more C# source files to weld together
  -o, --output  Path to write the resulting welded .csx file to (required)
  -h, --help    Show this help message and exit
```

### MigrateEvents

Converts event names from the legacy format to the new format.

Example: `Events.PlayerKeyInputCallback.Start` -> `Game.Events.StartPlayerKeyInputCallback`
