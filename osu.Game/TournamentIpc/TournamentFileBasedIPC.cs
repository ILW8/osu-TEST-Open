// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Online.Chat;

namespace osu.Game.TournamentIpc
{
    public partial class TournamentFileBasedIPC : Component
    {
        private ScheduledDelegate? scheduled;

        private readonly List<Message> chatMessages = new List<Message>();

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            var tournamentStorage = storage.GetStorageForDirectory(@"tournaments");
            scheduled?.Cancel();
            scheduled = Scheduler.AddDelayed(delegate
            {
                Logger.Log(@$"chat has {chatMessages.Count} messages", LoggingTarget.Runtime, LogLevel.Debug);
            }, 250, true);
        }

        public void AddChatMessage(Message message)
        {
            chatMessages.Add(message);
        }

        public void ClearChatMessages()
        {
            chatMessages.Clear();
        }
    }
}
