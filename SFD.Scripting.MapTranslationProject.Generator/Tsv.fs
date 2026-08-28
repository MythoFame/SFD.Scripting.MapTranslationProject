module Tsv

open System
open System.Text

exception TsvError of message: string

/// Unescapes a field value. Recognized escapes are \\, \t, \n and \r; any other
/// backslash sequence is rejected so stray backslashes cannot corrupt data.
let private unescapeField (value: string) : string =
    if not (value.Contains('\\')) then value
    else
        let sb = StringBuilder(value.Length)
        let mutable i = 0

        while i < value.Length do
            let c = value.[i]

            if c <> '\\' then
                sb.Append(c) |> ignore
                i <- i + 1
            else
                if i + 1 >= value.Length then
                    raise (TsvError("field ends with a lone backslash"))

                match value.[i + 1] with
                | '\\' -> sb.Append('\\') |> ignore
                | 't' -> sb.Append('\t') |> ignore
                | 'n' -> sb.Append('\n') |> ignore
                | 'r' -> sb.Append('\r') |> ignore
                | other -> raise (TsvError($"unknown escape '\\{other}'"))

                i <- i + 2

        sb.ToString()

/// Parses tab-separated values into rows of fields.
/// Records are separated by LF (a trailing CR is tolerated); fields by tabs.
/// Field values are unescaped, which lets text contain newlines and tabs
/// without breaking the row structure. Blank lines are skipped and an optional
/// UTF-8 BOM is accepted.
let parse (content: string) : string list list =
    let text =
        if content.Length > 0 && content.[0] = '\uFEFF' then content.Substring(1) else content

    text.Split('\n')
    |> Seq.filter (fun line -> line.Length > 0)
    |> Seq.map (fun line ->
        let line =
            if line.EndsWith('\r') then line.Substring(0, line.Length - 1) else line

        line.Split('\t') |> Array.map unescapeField |> List.ofArray)
    |> List.ofSeq
