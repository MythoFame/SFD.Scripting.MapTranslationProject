namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript : GameScriptInterfaceExtended
{
    private const string STORED_LANG_KEY = "MapTranslationProject.Language";

    public static string LanguageKey
    {
        get => Game.LocalStorage.GetItem(STORED_LANG_KEY) as string;
        set => Game.LocalStorage.SetItem(STORED_LANG_KEY, value);
    }
}
