module Csv

open System
open System.IO
open System.Text

/// Parses RFC 4180 CSV text into rows of fields.
/// Accepts LF and CRLF line endings, an optional UTF-8 BOM, and `""` escapes
/// inside quoted fields. Blank lines are skipped. A quote only starts a quoted
/// field at the beginning of a field; quotes elsewhere are taken literally.
let parse (content: string) : string list list =
    let text =
        // Exact char comparison: a culture-sensitive StartsWith would treat
        // U+FEFF as ignorable and match every input.
        if content.Length > 0 && content.[0] = '\uFEFF' then content.Substring(1) else content

    let n = text.Length
    let mutable i = 0
    let mutable field = StringBuilder()
    let mutable row = []
    let mutable rows = []
    let mutable inQuotes = false

    let endField () =
        row <- field.ToString() :: row
        field <- StringBuilder()

    let endRow () =
        endField ()
        rows <- List.rev row :: rows
        row <- []

    while i < n do
        let c = text.[i]
        if inQuotes then
            if c = '"' then
                if i + 1 < n && text.[i + 1] = '"' then
                    field.Append('"') |> ignore
                    i <- i + 2
                else
                    inQuotes <- false
                    i <- i + 1
            else
                field.Append(c) |> ignore
                i <- i + 1
        elif c = '"' && field.Length = 0 then
            inQuotes <- true
            i <- i + 1
        elif c = ',' then
            endField ()
            i <- i + 1
        elif c = '\n' then
            endRow ()
            i <- i + 1
        elif c = '\r' then
            endRow ()
            i <- if i + 1 < n && text.[i + 1] = '\n' then i + 2 else i + 1
        else
            field.Append(c) |> ignore
            i <- i + 1

    if field.Length > 0 || not (List.isEmpty row) then endRow ()

    rows |> List.rev |> List.filter (fun r -> r <> [ "" ])

let private needsQuote (s: string) =
    s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r')

let private quoteField (s: string) =
    if needsQuote s then "\"" + s.Replace("\"", "\"\"") + "\"" else s

/// Renders rows back to RFC 4180 CSV text with LF line endings.
let render (rows: string list list) : string =
    if List.isEmpty rows then ""
    else
        rows
        |> List.map (fun row -> row |> List.map quoteField |> String.concat ",")
        |> String.concat "\n"
        |> fun t -> t + "\n"

/// Writes rows as UTF-8 CSV with a BOM for spreadsheet interoperability.
let writeFile (path: string) (rows: string list list) : unit =
    File.WriteAllText(path, render rows, Encoding.UTF8)
