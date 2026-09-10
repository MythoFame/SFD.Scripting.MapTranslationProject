using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public abstract class GameScriptInterfaceExtended : GameScriptInterface
{
    protected static readonly IGame Game;
}

public partial class GameScript : GameScriptInterfaceExtended
{
    public static void OnStartup()
    {
        CommandHandler.ActiveCommands.Add(new("LANG", SetLanguageKey)
        {
            HostOnly = true
        });

        CommandHandler.ActiveCommands.Add(new("TRANSLATION_DUMP", TranslationDump)
        {
            HostOnly = true
        });

        // External dialogues (created by other scripts) are translated lazily per
        // event, so the poller runs regardless of the chosen language.
        OnDialogueCallback.Start(OnExternalDialogue);

        if (string.IsNullOrEmpty(LanguageKey) || !Game.IsFirstUpdate) // don't do anything mid-game or with no lang set
        {
            return;
        }

        ApplyTranslations();
    }
}
