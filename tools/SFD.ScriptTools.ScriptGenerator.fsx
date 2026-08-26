#!/usr/bin/env -S dotnet fsi --

#r "nuget: Microsoft.CodeAnalysis.CSharp.Workspaces, 4.9.2"

open System
open System.IO
open Microsoft.CodeAnalysis
open Microsoft.CodeAnalysis.CSharp
open Microsoft.CodeAnalysis.CSharp.Syntax
open Microsoft.CodeAnalysis.Formatting

// Constants

[<Literal>]
let GameScriptClassName = "GameScript"

// Name of the throwaway wrapper class used purely so Roslyn has something
// syntactically valid to parse and format around the welded members.
let wrapperClassName = "__SFDScriptGeneratorFormattingWrapper__"

// Argument parsing

let printUsage () =
    printfn "Usage: SFD.ScriptTools.ScriptGenerator.fsx <file1.cs> [file2.cs ...] [-o|--output <path>] [-h|--help]"
    printfn ""
    printfn "  <file.cs>...  One or more C# source files to weld together"
    printfn "  -o, --output  Path to write the resulting welded .csx file to (required)"
    printfn "  -h, --help    Show this help message and exit"

type ParsedArgs =
    { Files: string list
      Output: string option
      Help: bool }

let rec parseArgs (args: string list) (acc: ParsedArgs) =
    match args with
    | [] -> { acc with Files = List.rev acc.Files }
    | ("-o" | "--output") :: value :: rest -> parseArgs rest { acc with Output = Some value }
    | ("-h" | "--help") :: rest -> parseArgs rest { acc with Help = true }
    | token :: rest when token.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ->
        parseArgs rest { acc with Files = token :: acc.Files }
    | token :: rest ->
        eprintfn "Warning: ignoring unrecognized argument '%s'" token
        parseArgs rest acc

let scriptArgs = fsi.CommandLineArgs |> List.ofArray

let parsed =
    parseArgs
        scriptArgs
        { Files = []
          Output = None
          Help = false }

if parsed.Help then
    printUsage ()
    exit 0

if parsed.Files.IsEmpty then
    eprintfn "No input .cs files were provided."
    printUsage ()
    exit 1

let outputPath =
    match parsed.Output with
    | Some path -> path
    | None ->
        eprintfn "Missing required -o/--output argument."
        printUsage ()
        exit 1

// Using-directive validation: the welded script can't contain using
// directives (except SFDGameScriptInterface which is already implicit).

let collectForbiddenUsings (filePath: string) : (string * string) list =
    if not (File.Exists filePath) then
        []
    else
        let tree = CSharpSyntaxTree.ParseText(File.ReadAllText filePath)
        let root = tree.GetRoot()

        // Only check usings in files that contribute to the welded output.
        let hasGameScript =
            root.DescendantNodes()
            |> Seq.exists (fun n ->
                match n with
                | :? ClassDeclarationSyntax as c when c.Identifier.ValueText = GameScriptClassName -> true
                | _ -> false)

        if not hasGameScript then [] else

        root.DescendantNodes()
        |> Seq.choose (fun n ->
            match n with
            | :? UsingDirectiveSyntax as u ->
                let name = u.Name.ToFullString().Trim()
                let label = u.ToString().TrimEnd(';', '\n', '\r').Trim()
                let isGlobal = u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)
                let isStatic = u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword)
                let hasAlias = not (isNull u.Alias)
                let isAllowed = not isGlobal && not isStatic && not hasAlias && name = "SFDGameScriptInterface"

                if isAllowed then None else Some(label, filePath)
            | _ -> None)
        |> Seq.toList

let forbiddenUsings = parsed.Files |> List.collect collectForbiddenUsings

if not forbiddenUsings.IsEmpty then
    eprintfn "The following 'using' directives are not allowed in welded scripts;"
    eprintfn "replace them with fully-qualified references (e.g. System.Collections.Generic.Dictionary)."

    for label, file in forbiddenUsings do
        eprintfn "  %s: %s;" (Path.GetFileName file) label

    eprintfn ""
    eprintfn "'using SFDGameScriptInterface;' is the only allowed using (implicit in the welded output)."
    exit 1

// Formatting helper: dedent by one indentation level. After formatting a
// member block that Roslyn believes lives one level inside a class, every
// line carries that class's indentation. Since the final welded output is
// no longer nested in any class, we strip that common leading whitespace
// back off so the code sits flush with our own header comments.

let dedentOneLevel (text: string) : string =
    let lines = text.Replace("\r\n", "\n").Split('\n')

    let nonBlankLines = lines |> Array.filter (fun l -> l.Trim() <> "")

    if nonBlankLines.Length = 0 then
        text
    else
        let minIndent =
            nonBlankLines
            |> Array.map (fun l -> l.Length - l.TrimStart(' ').Length)
            |> Array.min

        if minIndent = 0 then
            lines |> String.concat "\n"
        else
            lines
            |> Array.map (fun l ->
                if l.Trim() = "" then ""
                elif l.Length >= minIndent then l.Substring(minIndent)
                else l.TrimStart(' '))
            |> String.concat "\n"

let workspace = new AdhocWorkspace()

/// Wraps a raw member block in a throwaway class purely so Roslyn has valid
/// syntax to parse, runs the Formatter over it to fix up whitespace, unwraps
/// the formatted members back out, then dedents them by the one level of
/// indentation the wrapper class introduced.
let formatMemberBlock (rawBody: string) : string =
    let wrapperSource = sprintf "class %s\n{\n%s\n}" wrapperClassName rawBody
    let wrapperTree = CSharpSyntaxTree.ParseText(wrapperSource)
    let wrapperRoot = wrapperTree.GetRoot()
    let formattedRoot = Formatter.Format(wrapperRoot, workspace)

    let formattedWrapperClass =
        formattedRoot.DescendantNodes()
        |> Seq.tryPick (fun n ->
            match n with
            | :? ClassDeclarationSyntax as c when c.Identifier.ValueText = wrapperClassName -> Some c
            | _ -> None)

    match formattedWrapperClass with
    | None ->
        eprintfn "Warning: formatting step failed unexpectedly for one file; using unformatted output."
        rawBody
    | Some c ->
        c.Members
        |> Seq.map (fun m -> m.ToFullString())
        |> String.concat ""
        |> dedentOneLevel

// Extraction: pull everything inside the GameScript class out of each file,
// then format and dedent it, and finally prepend a flush-left header comment
// naming the source file.

let tryExtractGameScriptBody (filePath: string) : string option =
    if not (File.Exists filePath) then
        eprintfn "Warning: file not found, skipping: %s" filePath
        None
    else
        let source = File.ReadAllText filePath
        let tree = CSharpSyntaxTree.ParseText(source)
        let root = tree.GetRoot()

        let classDecl =
            root.DescendantNodes()
            |> Seq.tryPick (fun n ->
                match n with
                | :? ClassDeclarationSyntax as c when c.Identifier.ValueText = GameScriptClassName -> Some c
                | _ -> None)

        match classDecl with
        | None ->
            eprintfn "Warning: no '%s' class found in %s, skipping." GameScriptClassName filePath
            None
        | Some c ->
            let body = c.Members |> Seq.map (fun m -> m.ToFullString()) |> String.concat ""
            Some body

let weldedSections =
    parsed.Files
    |> List.choose (fun f ->
        tryExtractGameScriptBody f
        |> Option.map (fun rawBody ->
            let formattedBody = (formatMemberBlock rawBody).Trim('\n', '\r')
            sprintf "// %s\n%s" (Path.GetFileName f) formattedBody))

if weldedSections.IsEmpty then
    eprintfn "None of the provided files contained a '%s' class. Nothing to generate." GameScriptClassName
    exit 1

workspace.Dispose()

let autoGeneratedHeader =
    [ "// <auto-generated>"
      "//     This file was generated by SFD.ScriptTools.ScriptGenerator.fsx."
      "//     Do not modify it by hand as your changes will be lost on regeneration."
      "// </auto-generated>" ]
    |> String.concat "\n"

let finalCode =
    autoGeneratedHeader + "\n\n" + (weldedSections |> String.concat "\n\n")

// Write output

let fullOutputPath = Path.GetFullPath outputPath
let outputDir = Path.GetDirectoryName fullOutputPath

if not (String.IsNullOrEmpty outputDir) && not (Directory.Exists outputDir) then
    Directory.CreateDirectory outputDir |> ignore

File.WriteAllText(fullOutputPath, finalCode.Trim() + Environment.NewLine)

printfn "Wrote welded script (%d file(s)) to: %s" weldedSections.Length fullOutputPath
