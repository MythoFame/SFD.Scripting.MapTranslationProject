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
            Game.ShowChatMessage("You must provide a valid language key! e.g. es");

            return;
        }

        LanguageKey = msg;

        Game.ShowChatMessage($"Language set to {msg}!", Color.Green, uid);
    }
}
