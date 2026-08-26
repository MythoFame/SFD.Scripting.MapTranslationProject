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
}
