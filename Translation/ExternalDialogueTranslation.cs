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

        string translatedText = null;
        string translatedName = null;

        bool textChanged = !string.IsNullOrEmpty(dialogue.Text)
            && TranslationHashing.TryTranslate(table, TranslationHashing.DialogueTextKind, dialogue.Text, out translatedText);

        bool nameChanged = !string.IsNullOrEmpty(dialogue.Name)
            && TranslationHashing.TryTranslate(table, TranslationHashing.DialogueNameKind, dialogue.Name, out translatedName);

        if (!textChanged && !nameChanged)
        {
            return;
        }

        string newText = textChanged ? translatedText : dialogue.Text;
        string newName = nameChanged ? translatedName : dialogue.Name;

        // Dialogues must be re-created to change their text; the original is
        // closed afterwards. Prefer the anchored overload, falling back to the
        // last known world position if the target object is already gone.
        IObject targetObject = dialogue.TargetObjectID != 0
            ? Game.GetObject(dialogue.TargetObjectID)
            : null;

        IDialogue newDialogue = targetObject != null
            ? Game.CreateDialogue(newText, dialogue.TextColor, targetObject, newName, dialogue.DisplayDuration, false)
            : Game.CreateDialogue(newText, dialogue.TextColor, dialogue.TargetWorldPosition, newName, dialogue.DisplayDuration, false);

        _createdDialogueIds.Add(newDialogue.ID);

        dialogue.Close();
    }
}
