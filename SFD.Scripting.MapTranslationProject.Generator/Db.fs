module Db

open System
open System.IO
open System.Text.Json
open Model

exception DbError of message: string

let private fail (path: string) (message: string) =
    raise (DbError($"{path}: {message}"))

/// Builds a column accessor for a parsed CSV table. Headers are matched after
/// trimming; short rows yield empty strings for missing trailing cells.
let private makeGetter (path: string) (header: string list) (required: string list) =
    let trimmed = header |> List.map (fun h -> h.Trim())
    let missing = required |> List.filter (fun r -> not (List.contains r trimmed))

    if not (List.isEmpty missing) then
        let joined = String.Join(", ", missing)
        fail path $"missing required column(s): {joined}"

    fun (row: string list) (name: string) ->
        let index = List.findIndex ((=) name) trimmed
        if index < List.length row then row.[index] else ""

let private isBlankRow (row: string list) =
    row |> List.forall (fun field -> field.Trim() = "")

let private loadStrings (path: string) : StringEntry list =
    let rows = Csv.parse (File.ReadAllText path)

    match rows with
    | [] -> fail path "file has no header row"
    | header :: dataRows ->
        let getCell = makeGetter path header [ "original"; "key"; "kind" ]

        let entries =
            dataRows
            |> List.filter (fun row -> not (isBlankRow row))
            |> List.mapi (fun i row ->
                // The header occupies the first record of the file.
                let record = i + 2
                let get name = getCell row name
                let original = get "original"
                let key = (get "key").Trim()
                let kindRaw = (get "kind").Trim()

                if original.Trim() = "" || key = "" then
                    fail path $"record {record}: 'original' and 'key' must be non-empty"

                match Kind.tryParse kindRaw with
                | None -> fail path $"record {record}: unknown kind '{kindRaw}'"
                | Some kind -> { Original = original; Key = key; Kind = kind })

        let duplicateKey =
            entries
            |> List.groupBy (fun e -> e.Key)
            |> List.tryFind (fun (_, group) -> List.length group > 1)

        match duplicateKey with
        | Some(key, _) -> fail path $"duplicate key '{key}'"
        | None -> ()

        let duplicateOriginal =
            entries
            |> List.groupBy (fun e -> Kind.tag e.Kind, e.Original)
            |> List.tryFind (fun (_, group) -> List.length group > 1)

        match duplicateOriginal with
        | Some((tag, original), _) ->
            fail path $"'{original}' appears multiple times with kind '{tag}'"
        | None -> ()

        entries

let private loadTranslation (knownKeys: Map<string, unit>) (path: string) : Map<string, string> =
    let rows = Csv.parse (File.ReadAllText path)

    match rows with
    | [] -> fail path "file has no header row"
    | header :: dataRows ->
        let getCell = makeGetter path header [ "key"; "translation" ]

        (Map.empty, dataRows)
        ||> List.fold (fun table row ->
            if isBlankRow row then table
            else
                let key = (getCell row "key").Trim()
                let translation = getCell row "translation"

                if key = "" then fail path "found a record with an empty 'key'"
                elif Map.containsKey key table then
                    fail path $"duplicate key '{key}'"
                elif not (Map.containsKey key knownKeys) then
                    fail path $"'{key}' does not exist in strings.csv"
                else Map.add key translation table)

let private getStringProperty (element: JsonElement) (name: string) =
    let mutable value = Unchecked.defaultof<JsonElement>

    if element.TryGetProperty(name, &value) && value.ValueKind = JsonValueKind.String then
        value.GetString()
    else
        null

let private loadLanguages (path: string) : LanguageInfo list =
    let parsed =
        try Ok(JsonDocument.Parse(File.ReadAllText path))
        with exn -> Error exn.Message

    match parsed with
    | Error message -> fail path $"invalid JSON ({message})"
    | Ok document ->
        use document = document

        if document.RootElement.ValueKind <> JsonValueKind.Array then
            fail path "the root element must be an array"

        ([], document.RootElement.EnumerateArray())
        ||> Seq.fold (fun languages element ->
            if element.ValueKind <> JsonValueKind.Object then
                fail path "every language entry must be an object"

            let code = getStringProperty element "code"

            if String.IsNullOrWhiteSpace code then
                fail path "every language entry needs a non-empty 'code'"

            let code = code.Trim()
            let displayName = getStringProperty element "displayName"

            let language =
                { Code = code
                  DisplayName = if isNull displayName then code else displayName }

            if List.exists (fun l -> l.Code = language.Code) languages then
                fail path $"duplicate language '{language.Code}'"

            language :: languages)
        |> List.rev

/// Loads the whole database from a `db/` directory. Any file other than
/// `languages.json`, `strings.csv`, or well-formed `<lang>.csv` tables inside
/// map directories is ignored.
let load (dbDir: string) : Database =
    let languagesPath = Path.Combine(dbDir, "languages.json")
    let mapsRoot = Path.Combine(dbDir, "maps")

    if not (File.Exists languagesPath) then
        fail languagesPath "not found"

    if not (Directory.Exists mapsRoot) then
        fail mapsRoot "not found"

    let languages = loadLanguages languagesPath

    let maps =
        Directory.GetDirectories(mapsRoot)
        |> Array.sort
        |> Array.filter (fun directory -> File.Exists(Path.Combine(directory, "strings.csv")))
        |> Array.map (fun directory ->
            let guid = Path.GetFileName directory
            let stringsPath = Path.Combine(directory, "strings.csv")
            let entries = loadStrings stringsPath
            let knownKeys = entries |> List.map (fun e -> e.Key, ()) |> Map.ofList

            let translations =
                Directory.GetFiles(directory, "*.csv")
                |> Array.filter (fun file -> Path.GetFileName file <> "strings.csv")
                |> Array.map (fun file ->
                    Path.GetFileNameWithoutExtension file, loadTranslation knownKeys file)
                |> Map.ofArray

            { Guid = guid
              Entries = entries
              Translations = translations })
        |> List.ofArray

    { Languages = languages
      Maps = maps }
