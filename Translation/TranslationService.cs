using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript
{
    /// <summary>
    /// Applies the stored language's translations to every dialogue trigger and
    /// text object of the loaded map. Does nothing when no language is stored or
    /// when the map has no entry in the generated database.
    /// </summary>
    private static void ApplyTranslations()
    {
        Dictionary<ulong, string> table = ResolveTranslationTable();

        if (table == null)
        {
            return;
        }

        int dialogueTexts = 0;
        int dialogueNames = 0;
        int texts = 0;
        int popups = 0;
        int unmatched = 0;

        foreach (IObjectDialogueTrigger dialogueTrigger in Game.GetObjects<IObjectDialogueTrigger>())
        {
            string text = dialogueTrigger.GetDialogueText();

            if (!string.IsNullOrEmpty(text))
            {
                if (TranslationHashing.TryTranslate(table, TranslationHashing.DialogueTextKind, text, out string translatedText))
                {
                    dialogueTrigger.SetDialogueText(translatedText);
                    dialogueTexts++;
                }
                else
                {
                    unmatched++;
                }
            }

            string name = dialogueTrigger.GetDialogueName();

            if (!string.IsNullOrEmpty(name)
                && TranslationHashing.TryTranslate(table, TranslationHashing.DialogueNameKind, name, out string translatedName))
            {
                dialogueTrigger.SetDialogueName(translatedName);
                dialogueNames++;
            }
        }

        foreach (IObjectText objectText in Game.GetObjects<IObjectText>())
        {
            string text = objectText.GetText();

            if (!string.IsNullOrEmpty(text))
            {
                if (TranslationHashing.TryTranslate(table, TranslationHashing.TextKind, text, out string translated))
                {
                    objectText.SetText(translated);
                    texts++;
                }
                else
                {
                    unmatched++;
                }
            }
        }

        foreach (IObjectPopupMessageTrigger popupTrigger in Game.GetObjects<IObjectPopupMessageTrigger>())
        {
            string message = popupTrigger.GetPopupMessage();

            if (!string.IsNullOrEmpty(message))
            {
                if (TranslationHashing.TryTranslate(table, TranslationHashing.PopupKind, message, out string translated))
                {
                    popupTrigger.SetPopupMessage(translated);
                    popups++;
                }
                else
                {
                    unmatched++;
                }
            }
        }

        Game.WriteToConsoleF(
            "[MapTranslations] {0}: {1} dialogue texts, {2} dialogue names, {3} texts, {4} popups applied ({5} unmatched).",
            Game.MapOriginalGUID, LanguageKey, dialogueTexts, dialogueNames, texts, popups, unmatched);
    }

    /// <summary>
    /// Resolves the translation table for the loaded map and the stored language.
    /// Returns null when no language is set, the map is unknown, or the language
    /// has no table for the map.
    /// </summary>
    private static Dictionary<ulong, string> ResolveTranslationTable()
    {
        string language = LanguageKey;

        if (string.IsNullOrEmpty(language))
        {
            return null;
        }

        // The game reports MapOriginalGUID as hyphenated lowercase, which is
        // exactly how map directories in db/maps are named.
        if (!TranslationsDatabase.Maps.TryGetValue(Game.MapOriginalGUID.ToString(), out var dataset))
        {
            return null;
        }

        return dataset.TryGetValue(language, out var table) ? table : null;
    }

    /// <summary>
    /// Runtime port of the generator's key derivation: FNV-1a 64-bit over the
    /// UTF-8 bytes of the kind tag, a unit separator, and the raw object text.
    /// Must stay byte-for-byte identical to Generator/Hash.fs.
    /// </summary>
    private static class TranslationHashing
    {
        public const string DialogueTextKind = "dialogue-text";
        public const string DialogueNameKind = "dialogue-name";
        public const string TextKind = "text";
        public const string PopupKind = "popup";

        public static ulong Compute(string kindTag, string text)
        {
            const ulong prime = 0x100000001b3;
            const ulong offsetBasis = 0xcbf29ce484222325;

            ulong hash = offsetBasis;

            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(kindTag + "\u001F" + text))
            {
                hash = (hash ^ b) * prime;
            }

            return hash;
        }

        public static bool TryTranslate(
            Dictionary<ulong, string> table,
            string kindTag,
            string text,
            out string translation)
        {
            return table.TryGetValue(Compute(kindTag, text), out translation);
        }
    }
}
