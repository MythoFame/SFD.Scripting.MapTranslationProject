module Model

type StringKind =
    | DialogueText
    | DialogueName
    | Text
    | Popup

module Kind =
    let tag kind =
        match kind with
        | DialogueText -> "dialogue-text"
        | DialogueName -> "dialogue-name"
        | Text -> "text"
        | Popup -> "popup"

    let tryParse value =
        match value with
        | "dialogue-text" -> Some DialogueText
        | "dialogue-name" -> Some DialogueName
        | "text" -> Some Text
        | "popup" -> Some Popup
        | _ -> None

type StringEntry =
    { Original: string
      Key: string
      Kind: StringKind }

type LanguageInfo =
    { Code: string
      DisplayName: string }

type MapDataset =
    { Guid: string
      Entries: StringEntry list
      Translations: Map<string, Map<string, string>> }

type Database =
    { Languages: LanguageInfo list
      Maps: MapDataset list }
