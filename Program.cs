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
    }

    public static void AfterStartup()
    {
        if (string.IsNullOrEmpty(LanguageKey) || !Game.IsFirstUpdate)
        {
            return;
        }

        foreach (IObjectDialogueTrigger dialogueTrigger in Game.GetObjects<IObjectDialogueTrigger>())
        {
            string text = dialogueTrigger.GetDialogueText();
            string name = dialogueTrigger.GetDialogueName();

            // ...
        }

        foreach (IObjectText objectText in Game.GetObjects<IObjectText>())
        {
            string text = objectText.GetText();

            // ...
        }
    }
}
