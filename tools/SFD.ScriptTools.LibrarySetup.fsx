#!/usr/bin/env -S dotnet fsi --

open System
open System.IO
open System.Diagnostics
open System.Text.RegularExpressions

// Constants

[<Literal>]
let DllName = "SFD.GameScriptInterface.dll"

let defaultOutputPath = Path.Combine(".", "lib", DllName)

let homeDir = Environment.GetFolderPath Environment.SpecialFolder.UserProfile

/// Default locations to probe for the DLL, in order of preference.
let defaultSearchPaths: string list =
    [
      // Linux Flatpak Steam
      Path.Combine(
          homeDir,
          ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Superfighters Deluxe",
          DllName
      )
      // Linux native Steam
      Path.Combine(homeDir, ".local/share/Steam/steamapps/common/Superfighters Deluxe", DllName)
      // Windows Steam
      @"C:\Program Files (x86)\Steam\steamapps\common\Superfighters Deluxe\" + DllName ]

// Argument parsing

let printUsage () =
    printfn "Usage: SFD.ScriptTools.LibrarySetup.fsx [-f|--file <path>] [-o|--output <path>] [-F|--force] [--dry-run] [-h|--help]"
    printfn ""
    printfn "  -f, --file    Explicit path to %s (skips auto-search)" DllName
    printfn "  -o, --output  Output path for the symlink/copy (default: %s)" defaultOutputPath
    printfn "  -F, --force   On version mismatch, update <RequiredGameSdkVersion> to the installed DLL version"
    printfn "      --dry-run Run checks without touching the filesystem; exit 1 if action is needed"
    printfn "  -h, --help    Show this help message and exit"

let scriptArgs = fsi.CommandLineArgs |> List.ofArray

type ParsedArgs =
    { File: string option
      Output: string option
      Help: bool
      DryRun: bool
      Force: bool }

let rec parseArgs (args: string list) (acc: ParsedArgs) =
    match args with
    | [] -> acc
    | ("-f" | "--file") :: value :: rest -> parseArgs rest { acc with File = Some value }
    | ("-o" | "--output") :: value :: rest -> parseArgs rest { acc with Output = Some value }
    | "--dry-run" :: rest -> parseArgs rest { acc with DryRun = true }
    | ("-F" | "--force") :: rest -> parseArgs rest { acc with Force = true }
    | ("-h" | "--help") :: rest -> parseArgs rest { acc with Help = true }
    | unknown :: rest ->
        eprintfn "Warning: Unknown argument '%s' (ignored)." unknown
        parseArgs rest acc

let parsed =
    parseArgs
        scriptArgs
        { File = None
          Output = None
          Help = false
          DryRun = false
          Force = false }

if parsed.Help then
    printUsage ()
    exit 0

let dryRun = parsed.DryRun
let force = parsed.Force
let explicitFile = parsed.File
let outputPath = defaultArg parsed.Output defaultOutputPath

let currentDir = Directory.GetCurrentDirectory()

// Resolve which DLL to use: prefer an existing copy nested in the current
// directory (linking is then unnecessary), otherwise locate a source on disk.

let existingNestedCopies =
    Directory.GetFiles(currentDir, DllName, SearchOption.AllDirectories)

let resolveSourcePath () : string option =
    match explicitFile with
    | Some path ->
        if File.Exists(path) then
            Some(Path.GetFullPath path)
        else
            eprintfn "Specified file via -f does not exist: %s" path
            None
    | None -> defaultSearchPaths |> List.tryFind File.Exists |> Option.map Path.GetFullPath

let dllPath, skipLinking =
    if existingNestedCopies.Length > 0 then
        printfn "'%s' already exists within the current directory:" DllName

        for p in existingNestedCopies do
            printfn "  %s" p

        printfn "Skipping link/copy step."
        existingNestedCopies.[0], true
    else
        match resolveSourcePath () with
        | Some p -> p, false
        | None ->
            eprintfn "Could not find '%s' in any of the default locations:" DllName

            for p in defaultSearchPaths do
                eprintfn "  %s" p

            eprintfn ""
            eprintfn "Please specify its location manually with -f <path>."
            exit 1

// Create output folder(s) and link/copy the DLL

let fullOutputPath = Path.GetFullPath outputPath
let outputDir = Path.GetDirectoryName fullOutputPath

let didLink =
    if skipLinking then
        false
    else
        if not (String.IsNullOrEmpty outputDir) && not (Directory.Exists outputDir) then
            if dryRun then
                printfn "[dry-run] Would create directory: %s" outputDir
            else
                Directory.CreateDirectory outputDir |> ignore
                printfn "Created directory: %s" outputDir

        if File.Exists fullOutputPath then
            printfn "Output file already exists at: %s" fullOutputPath
            printfn "Nothing to link/copy."
            false
        else
            printfn "Found source DLL at: %s" dllPath
            printfn "Linking to:          %s" fullOutputPath

            if dryRun then
                printfn "[dry-run] Would create symlink (falling back to a copy)."
                false
            else
                try
                    File.CreateSymbolicLink(fullOutputPath, dllPath) |> ignore
                    printfn "Successfully created symlink."
                    true
                with ex ->
                    printfn "Symlink creation failed (%s)." ex.Message
                    printfn "Falling back to copying the file instead..."

                    try
                        File.Copy(dllPath, fullOutputPath, overwrite = false)
                        printfn "Successfully copied the DLL."
                        true
                    with copyEx ->
                        eprintfn "Copy also failed: %s" copyEx.Message
                        exit 1

// Ensure the linked/copied DLL is gitignored (only when we actually linked it)

if didLink && not dryRun then
    let gitignorePath = Path.Combine(currentDir, ".gitignore")

    let gitignoreEntry =
        Path.GetRelativePath(currentDir, fullOutputPath).Replace('\\', '/')

    let existingGitignoreLines =
        if File.Exists gitignorePath then
            File.ReadAllLines gitignorePath
        else
            [||]

    let alreadyIgnored =
        existingGitignoreLines
        |> Array.exists (fun line -> line.Trim() = gitignoreEntry)

    if alreadyIgnored then
        printfn "'.gitignore' already contains an entry for '%s'." gitignoreEntry
    else if File.Exists gitignorePath then
        let needsLeadingNewline =
            existingGitignoreLines.Length > 0
            && not (String.IsNullOrEmpty(Array.last existingGitignoreLines))

        let textToAppend =
            (if needsLeadingNewline then Environment.NewLine else "")
            + gitignoreEntry
            + Environment.NewLine

        File.AppendAllText(gitignorePath, textToAppend)
        printfn "Added '%s' to existing .gitignore." gitignoreEntry
    else
        File.WriteAllText(gitignorePath, gitignoreEntry + Environment.NewLine)
        printfn "Created .gitignore with entry '%s'." gitignoreEntry

// Version tracking: read the DLL's product version and keep it in sync with the
// <RequiredGameSdkVersion> field of the nearest .csproj.

/// ProductVersion from .NET's FileVersionInfo sometimes carries a trailing
/// NUL byte on Linux; strip it so the stored value is clean and comparable.
let sanitizeVersion (s: string) =
    s.Replace("\u0000", "").Trim()

let assemblyVersion =
    try
        let fvi = FileVersionInfo.GetVersionInfo(dllPath)
        let v = sanitizeVersion fvi.ProductVersion

        if String.IsNullOrEmpty v then
            eprintfn "ProductVersion of '%s' is empty; cannot determine version." dllPath
            None
        else
            Some v
    with ex ->
        eprintfn "Failed to read version from '%s': %s" dllPath ex.Message
        None

match assemblyVersion with
| None ->
    eprintfn "Cannot determine the assembly version. Cannot continue."
    exit 1
| Some v ->
    printfn "DLL version: %s" v

    let csprojFiles =
        Directory.GetFiles(currentDir, "*.csproj", SearchOption.TopDirectoryOnly)

    if csprojFiles.Length = 0 then
        printfn "No .csproj found in '%s'; skipping version tracking." currentDir
        printfn "Done."
        exit 0

    let csprojPath = csprojFiles.[0]
    let csprojName = Path.GetFileName csprojPath
    printfn "Project file: %s" csprojPath

    let csprojText = File.ReadAllText csprojPath

    let fieldRegex =
        Regex(
            @"<RequiredGameSdkVersion>\s*(.*?)\s*</RequiredGameSdkVersion>",
            RegexOptions.Singleline
        )

    match fieldRegex.Match(csprojText) with
    | m when m.Success ->
        let fieldText = m.Groups.[1].Value.Trim()

        if String.Equals(fieldText, v, StringComparison.Ordinal) then
            printfn "RequiredGameSdkVersion (%s) matches the installed DLL." v
            printfn "Done."
            exit 0
        else
            eprintfn "Version mismatch!"
            eprintfn "  Project requires:  %s" fieldText
            eprintfn "  Installed DLL:     %s" v

            if force && not dryRun then
                let replacement = sprintf "<RequiredGameSdkVersion>%s</RequiredGameSdkVersion>" v
                let newCsprojText = fieldRegex.Replace(csprojText, replacement, 1)
                File.WriteAllText(csprojPath, newCsprojText)
                printfn "Updated <RequiredGameSdkVersion> from '%s' to '%s' in '%s'." fieldText v csprojName
                printfn "Done."
                exit 0
            elif force && dryRun then
                eprintfn "[dry-run] Would update <RequiredGameSdkVersion> from '%s' to '%s'." fieldText v
                eprintfn "Cannot continue without writing changes; exiting with status 1."
                exit 1
            else
                eprintfn "Install the required SFD SDK version or update '%s' manually." csprojName
                eprintfn "Pass --force to update the project to the installed version automatically."
                exit 1
    | _ ->
        printfn "No <RequiredGameSdkVersion> field found in '%s'." csprojName

        if dryRun then
            eprintfn "[dry-run] Would add <RequiredGameSdkVersion>%s</RequiredGameSdkVersion>." v
            eprintfn "Cannot continue without writing changes; exiting with status 1."
            exit 1

        // Insert the field right after the opening tag of the first <PropertyGroup>,
        // inheriting its indentation (plus one level) so the file's formatting is
        // preserved instead of having the whole document re-flowed by an XML parser.
        let pgRegex = Regex(@"([ \t]*)<PropertyGroup[^>]*>\r?\n")

        match pgRegex.Match(csprojText) with
        | pgm when pgm.Success ->
            let indent = pgm.Groups.[1].Value + "    "
            let entry = sprintf "%s<RequiredGameSdkVersion>%s</RequiredGameSdkVersion>%s" indent v Environment.NewLine
            let newCsprojText = csprojText.Insert(pgm.Index + pgm.Length, entry)

            File.WriteAllText(csprojPath, newCsprojText)
            printfn "Added <RequiredGameSdkVersion>%s</RequiredGameSdkVersion> to '%s'." v csprojName
            printfn "Done."
            exit 0
        | _ ->
            eprintfn "No <PropertyGroup> found in '%s'; cannot add the field." csprojName
            eprintfn "Add <RequiredGameSdkVersion>%s</RequiredGameSdkVersion> manually." v
            exit 1
