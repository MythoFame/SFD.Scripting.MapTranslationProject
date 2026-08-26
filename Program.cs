using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public abstract class GameScriptInterfaceExtended : GameScriptInterface
{
    protected static readonly IGame Game;
}

public partial class GameScript : GameScriptInterfaceExtended
{
    // ...
}
