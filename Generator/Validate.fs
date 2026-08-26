module Validate

open System
open System.Text.RegularExpressions
open Hash
open Model

/// Renders a value for diagnostics with quotes around it.
let private quoteish (value: string) = $"'{value}'"

let private placeholderTokens (value: string) =
    Regex.Matches(value, @"\{\d+\}")
    |> Seq.map (fun m -> m.Value)
    |> Seq.sort
    |> List.ofSeq

/// Dialogue name boxes can only display so much text.
let private maxDialogueNameLength = 16

/// Runs all advisory checks over a successfully loaded database.
/// Returns coverage info lines, warnings, and errors.
let run (db: Database) : string list * string list * string list =
    let info = ResizeArray<string>()
    let warnings = ResizeArray<string>()
    let errors = ResizeArray<string>()

    for map in db.Maps do
        // Distinct entries colliding on the same 64-bit hash would silently
        // overwrite each other in the generated tables.
        let collisions =
            map.Entries
            |> List.map (fun e -> compute (Kind.tag e.Kind) e.Original, e.Key)
            |> List.groupBy fst
            |> List.filter (fun (_, group) ->
                group |> List.map snd |> List.distinct |> List.length > 1)

        for hash, keys in collisions do
            let joined = String.Join(", ", keys |> List.map snd)
            errors.Add($"{map.Guid}: hash collision {toHex hash} between [{joined}]")

        let total = List.length map.Entries

        for language, table in Map.toList map.Translations do
            let translated =
                table |> Seq.filter (fun kv -> kv.Value <> "") |> Seq.length

            let percent = if total = 0 then 100 else translated * 100 / total
            info.Add(sprintf "%s %s: %d/%d (%d%%)" map.Guid language translated total percent)

        let entryByKey = map.Entries |> List.map (fun e -> e.Key, e) |> Map.ofList

        for language, table in Map.toList map.Translations do
            for kv in table do
                if kv.Value <> "" then
                    match Map.tryFind kv.Key entryByKey with
                    | None -> () // unreachable: the loader rejects unknown keys
                    | Some entry ->
                        let original = entry.Original
                        let translation = kv.Value

                        if placeholderTokens original <> placeholderTokens translation then
                            warnings.Add(
                                $"{map.Guid} {language} '{kv.Key}': "
                                + $"placeholder mismatch ({quoteish original} vs {quoteish translation})"
                            )

                        let originalLength = String.length original
                        let translationLength = String.length translation

                        if entry.Kind = DialogueName && translationLength > maxDialogueNameLength then
                            errors.Add(
                                $"{map.Guid} {language} '{kv.Key}': "
                                + $"dialogue name exceeds {maxDialogueNameLength} characters ({translationLength})"
                            )

                        if float translationLength > float originalLength * 1.5
                           && translationLength - originalLength > 10 then
                            warnings.Add(
                                $"{map.Guid} {language} '{kv.Key}': "
                                + $"translation is much longer than the original ({translationLength} vs {originalLength} chars)"
                            )

    List.ofSeq info, List.ofSeq warnings, List.ofSeq errors
