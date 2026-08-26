using SFDGameScriptInterface;

namespace SFD.Scripting.MapTranslationProject;

public partial class GameScript : GameScriptInterfaceExtended
{
    /// <summary>
    /// Tracks registered chat commands and dispatches incoming user messages to
    /// their associated callbacks. The handler auto-subscribes to user message
    /// events the moment a command is added to <see cref="ActiveCommands"/>, and
    /// auto-unsubscribes once the list is emptied — no manual Initialize/Destroy
    /// calls required.
    /// </summary>
    public static class CommandHandler
    {
        private static Events.UserMessageCallback _callback = null;

        /// <summary>
        /// All commands currently registered with the handler. Fully public and
        /// mutable (Add, Remove, Clear, indexer, foreach, LINQ, etc. all work as
        /// normal) — the collection itself manages the subscription lifecycle.
        /// </summary>
        public static readonly CommandCollection ActiveCommands = [];

        /// <summary>
        /// Create a command instance using this function as a parameter for an automatic help command.
        /// </summary>
        public static void DisplayHelp(UserMessageCallbackArgs args)
        {
            IUser user = args.User;

            Game.ShowChatMessage("Available commands:", Color.Green, user.UserIdentifier);

            IOrderedEnumerable<Command> commands = ActiveCommands
                .OrderBy(cmd => cmd.ModeratorOnly)
                .ThenBy(cmd => cmd.HostOnly)
                .ThenBy(cmd => cmd.Name);

            foreach (Command command in commands)
            {
                if (command.ModeratorOnly && !user.IsModerator) continue;
                if (command.HostOnly && !user.IsHost) continue;

                string displayTxt = $"/{command.Name} ";

                if (command.Description != null)
                    displayTxt += command.Description;

                Color color = command.HostOnly ? Color.Magenta
                    : command.ModeratorOnly ? Color.Yellow
                    : Color.Green;

                Game.ShowChatMessage(displayTxt, color, args.User.UserIdentifier);
            }
        }

        /// <summary>
        /// Invoked for every user message. When the message is a command, locates the
        /// matching <see cref="Command"/> in <see cref="ActiveCommands"/>, enforces its
        /// <see cref="Command.ModeratorOnly"/> and <see cref="Command.HostOnly"/>
        /// permissions, and fires its callback.
        /// </summary>
        private static void OnUserMessage(UserMessageCallbackArgs args)
        {
            if (!args.IsCommand) return;

            Command commandActivated = ActiveCommands
                .FirstOrDefault(c => c.Name == args.Command);

            if (commandActivated == null) return;

            IUser user = args.User;

            if ((!user.IsModerator && commandActivated.ModeratorOnly)
                || (!user.IsHost && commandActivated.HostOnly))
            {
                Game.ShowChatMessage("You don't have permission to use this command.",
                    Color.Red, user.UserIdentifier);

                return;
            }

            commandActivated.OnCommand.Invoke(args);
        }

        private static void Subscribe()
        {
            if (_callback != null) return;

            _callback = Game.Events.StartUserMessageCallback(OnUserMessage);
            Game.WriteToConsoleF("CommandHandler initialized.");
        }

        private static void Unsubscribe()
        {
            if (_callback == null) return;

            if (!Game.Events.Stop(_callback))
                Game.WriteToConsoleF("Could not stop UserMessageCallback.");

            _callback = null;
            Game.WriteToConsoleF("CommandHandler destroyed.");
        }

        /// <summary>
        /// A <see cref="Collection{T}"/> of <see cref="Command"/> that transparently
        /// subscribes/unsubscribes the handler's user-message callback as commands are
        /// added and removed, while remaining fully public and behaving like a normal list.
        /// </summary>
        public sealed class CommandCollection : System.Collections.ObjectModel.Collection<Command>
        {
            protected override void InsertItem(int index, Command item)
            {
                base.InsertItem(index, item);
                Subscribe();
            }

            protected override void RemoveItem(int index)
            {
                base.RemoveItem(index);
                if (Count == 0) Unsubscribe();
            }

            protected override void ClearItems()
            {
                base.ClearItems();
                Unsubscribe();
            }

            protected override void SetItem(int index, Command item)
            {
                base.SetItem(index, item);
                Subscribe();
            }
        }

        /// <summary>
        /// Represents a single chat command that the <see cref="CommandHandler"/> can dispatch.
        /// </summary>
        public sealed class Command
        {
            private string _name = string.Empty;

            /// <summary>
            /// The case-insensitive name of the command. Stored and compared in upper-case.
            /// </summary>
            public string Name
            {
                get => _name; set => _name = value.ToUpper();
            }

            /// <summary>
            /// Whether the command requires a moderator to execute it. By default false.
            /// </summary>
            public bool ModeratorOnly = false;

            /// <summary>
            /// Whether the command requires the host to execute it. By default false.
            /// </summary>
            public bool HostOnly = false;

            /// <summary>
            /// The human-readable description to show when the user requests command help.
            /// </summary>
            public string Description = null;

            /// <summary>
            /// The action executed when a user issues this command in chat. Receives the
            /// original <see cref="UserMessageCallbackArgs"/> containing the sender and arguments.
            /// </summary>
            public Action<UserMessageCallbackArgs> OnCommand = null;

            /// <summary>
            /// Creates a new <see cref="Command"/> with the given name and callback.
            /// </summary>
            /// <param name="name">The name of the command, as typed in chat (without leading slash).</param>
            /// <param name="onCommand">The callback to invoke when the command is used.</param>
            public Command(string name, Action<UserMessageCallbackArgs> onCommand)
            {
                Name = name;
                OnCommand = onCommand;
            }
        }
    }
}
