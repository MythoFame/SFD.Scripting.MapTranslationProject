using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript
{
    /// <summary>
    /// IDs of dialogues created by this handler, so the dialogue callback never
    /// re-translates them (which would recurse forever).
    /// </summary>
    private static readonly HashSet<int> _createdDialogueIds = [];

    private static void OnExternalDialogue(IDialogue dialogue)
    {
        if (_createdDialogueIds.Contains(dialogue.ID))
        {
            return;
        }

        // The table is frozen at map load; language changes only apply to the
        // next map. Dialogues from triggers are already replaced at load and no
        // longer hash-match the original database entries, so they drop out here.
        Dictionary<ulong, string> table = ActiveTranslationTable;

        if (table == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(dialogue.Text)
            || !TranslationHashing.TryTranslate(table, TranslationHashing.DialogueTextKind, dialogue.Text, out string translatedText))
        {
            return;
        }

        // The name is passed through as-is: with showInChat disabled the name is
        // irrelevant to the displayed message.
        // Dialogues must be re-created to change their text; the original is
        // closed afterwards. Prefer the anchored overload, falling back to the
        // last known world position if the target object is already gone.
        IObject targetObject = dialogue.TargetObjectID != 0
            ? Game.GetObject(dialogue.TargetObjectID)
            : null;

        IDialogue newDialogue = targetObject != null
            ? Game.CreateDialogue(translatedText, dialogue.TextColor, targetObject, dialogue.Name, dialogue.DisplayDuration, false)
            : Game.CreateDialogue(translatedText, dialogue.TextColor, dialogue.TargetWorldPosition, dialogue.Name, dialogue.DisplayDuration, false);

        _createdDialogueIds.Add(newDialogue.ID);

        dialogue.Close();
    }
}
