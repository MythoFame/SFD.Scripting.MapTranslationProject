using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript : GameScriptInterfaceExtended
{
    public static void SetLanguageKey(UserMessageCallbackArgs args)
    {
        int uid = args.User.UserIdentifier;
        string msg = args.CommandArguments.Trim();

        if (string.IsNullOrWhiteSpace(msg))
        {
            Game.ShowChatMessage($"Available languages: {string.Join(", ", TranslationsDatabase.LanguageCodes)}", Color.Yellow, uid);

            return;
        }

        if (msg.Equals("original", StringComparison.OrdinalIgnoreCase))
        {
            LanguageKey = null;

            Game.ShowChatMessage("Language reset to original.", Color.Green, uid);

            return;
        }

        int index = Array.FindIndex(
            TranslationsDatabase.LanguageCodes,
            code => code.Equals(msg, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            Game.ShowChatMessage($"Unknown language '{msg}'! Available: {string.Join(", ", TranslationsDatabase.LanguageCodes)}", Color.Red, uid);

            return;
        }

        LanguageKey = TranslationsDatabase.LanguageCodes[index];

        Game.ShowChatMessage($"Language set to {TranslationsDatabase.LanguageDisplayNames[index]}! Re-enter the map to apply it.", Color.Green, uid);
    }

    public static void TranslationDump(UserMessageCallbackArgs args)
    {
        IScriptStorage storage = Game.GetSharedStorage("translationdump");

        storage.Clear(); // drop stale rows from a previous, larger dump
        storage.SetItem(nameof(IGame.MapOriginalGUID), Game.MapOriginalGUID.ToString());

        string chapter = Game.CampaignCurrentMapPartIndex != -1
            ? $"{Game.CampaignCurrentMapPartIndex}."
            : string.Empty;

        int keyNum = 0;

        void Dump(string value, string kind, string objKey = "")
        {
            if (string.IsNullOrEmpty(value)) return;

            keyNum++;

            storage.SetItem($"{kind}.{keyNum}", $"{value}\t{chapter}{kind}.{objKey}placeholder.{keyNum}\t{kind}");
        }

        foreach (IObjectDialogueTrigger dialogueTrigger in Game.GetObjects<IObjectDialogueTrigger>())
        {
            Dump(dialogueTrigger.GetDialogueText(), "dialogue-text", ObjectKey(dialogueTrigger.GetDialogueTargetObject()));
            Dump(dialogueTrigger.GetDialogueName(), "dialogue-name");
        }

        foreach (IObjectText objectText in Game.GetObjects<IObjectText>())
        {
            Dump(objectText.GetText(), "text");
        }

        foreach (IObjectPopupMessageTrigger popupTrigger in Game.GetObjects<IObjectPopupMessageTrigger>())
        {
            Dump(popupTrigger.GetPopupMessage(), "popup");
        }

        Game.ShowChatMessage("Created dump!", Color.Green, args.User.UserIdentifier);
    }

    private static string ObjectKey(IObject obj)
    {
        if (obj == null || string.IsNullOrWhiteSpace(obj.Name)) return "";

        return $"{obj.Name.Replace(' ', '_').ToLowerInvariant()}.";
    }
}
