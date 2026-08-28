module Program

open System
open System.IO

let private usage =
    "Usage: dotnet run --project Generator -- <generate|validate> [--repo-dir <path>] [--db <dir>] [--out <file>]"

/// Walks up from the current directory looking for the repository root
/// (identified by the presence of a `db/` directory).
let private findRepoDir () =
    let rec go (dir: string) =
        if Directory.Exists(Path.Combine(dir, "db")) then Some dir
        else
            match Option.ofObj (Directory.GetParent dir) with
            | None -> None
            | Some parent -> go parent.FullName

    go (Directory.GetCurrentDirectory())

[<EntryPoint>]
let main args =
    let mutable command = ""
    let mutable repoDirOverride = None
    let mutable dbDirOverride = None
    let mutable outPathOverride = None
    let mutable argumentsOk = true

    let mutable i = 0

    while i < args.Length && argumentsOk do
        match args.[i] with
        | "generate" | "validate" as value ->
            command <- value
            i <- i + 1
        | "--repo-dir" when i + 1 < args.Length ->
            repoDirOverride <- Some args.[i + 1]
            i <- i + 2
        | "--db" when i + 1 < args.Length ->
            dbDirOverride <- Some args.[i + 1]
            i <- i + 2
        | "--out" when i + 1 < args.Length ->
            outPathOverride <- Some args.[i + 1]
            i <- i + 2
        | unexpected ->
            eprintfn $"Unexpected argument '{unexpected}'."
            eprintfn $"{usage}"
            argumentsOk <- false

    if not argumentsOk then 1
    elif command = "" then
        eprintfn "No command provided. Expected 'generate' or 'validate'."
        eprintfn $"{usage}"
        1
    else
        let repoDir =
            match repoDirOverride with
            | Some dir -> Some dir
            | None -> findRepoDir ()

        match repoDir with
        | None ->
            eprintfn
                "Couldn't locate the repository root (no 'db' directory found). Pass --repo-dir <path>."

            1
        | Some repoDir ->
            let dbDir =
                match dbDirOverride with
                | Some dir -> dir
                | None -> Path.Combine(repoDir, "db")

            try
                let database = Db.load dbDir

                match command with
                | "generate" ->
                    let outPath =
                        match outPathOverride with
                        | Some path -> path
                        | None -> Path.Combine(repoDir, "Translation", "Data.generated.cs")

                    Emit.writeGenerated outPath (Emit.generateTranslations database)

                    printfn
                        $"Generated {outPath} ({database.Maps.Length} maps, {database.Languages.Length} languages)."

                    0
                | "validate" ->
                    let info, warnings, errors = Validate.run database

                    info |> List.iter (printfn "%s")
                    warnings |> List.iter (eprintfn "warning: %s")
                    errors |> List.iter (eprintfn "error: %s")

                    if not (List.isEmpty errors) then
                        eprintfn $"Validation failed with {List.length errors} error(s)."
                        1
                    else
                        if List.isEmpty warnings then
                            printfn "Validation passed."
                        else
                            printfn $"Validation passed with {List.length warnings} warning(s)."

                        0
                | _ ->
                    // Unreachable: the argument parser only accepts known commands.
                    1
            with
            | Db.DbError message ->
                eprintfn $"{message}"
                1
            | exn ->
                eprintfn $"{exn.Message}"
                1
