using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript : GameScriptInterfaceExtended
{
    /// <summary>
    /// Polls <see cref="IGame.GetDialogues"/> on an update loop and fires the supplied
    /// callback once for each new dialogue, tracked by <see cref="IDialogue.ID"/>. Mirrors
    /// the lifecycle of the built-in SFD callbacks (exposed via the legacy
    /// <c>Events.*Callback</c> format): call <see cref="Start"/> to register,
    /// <see cref="Stop()"/> to unsubscribe.
    /// </summary>
    public class OnDialogueCallback : Events.CallbackDelegate
    {
        private const uint COOLDOWN = 50;

        private readonly Events.UpdateCallback _updateCallback;
        private readonly Action<IDialogue> _onDialogue;
        private readonly HashSet<int> _dialogueIds = [];

        private OnDialogueCallback(Action<IDialogue> onDialogue)
        {
            _updateCallback = Game.Events.StartUpdateCallback(Update, COOLDOWN);
            _onDialogue = onDialogue;
        }

        /// <summary>
        /// Starts polling for dialogues and registers <paramref name="func"/> to be invoked
        /// once for every new dialogue.
        /// </summary>
        /// <param name="func">The action to run for each new dialogue, receiving the
        /// <see cref="IDialogue"/> instance.</param>
        /// <returns>The active <see cref="OnDialogueCallback"/> instance.</returns>
        public static OnDialogueCallback Start(Action<IDialogue> func) => new(func);

        /// <summary>
        /// Stops the given callback, halting its update loop.
        /// </summary>
        /// <returns><c>true</c> if the callback was successfully stopped.</returns>
        public static bool Stop(OnDialogueCallback callback) => callback._updateCallback.Stop();

        /// <summary>
        /// Invokes the registered dialogue action manually.
        /// </summary>
        public void Invoke(IDialogue dialogue) => _onDialogue(dialogue);

        /// <inheritdoc/>
        public override void Dispose() { }

        /// <inheritdoc/>
        public override bool Stop() => _updateCallback.Stop();

        private void Update(float dlt)
        {
            foreach (IDialogue dialogue in Game.GetDialogues())
            {
                if (_dialogueIds.Add(dialogue.ID) && dialogue != null)
                {
                    Invoke(dialogue);
                }
            }
        }
    }
}
