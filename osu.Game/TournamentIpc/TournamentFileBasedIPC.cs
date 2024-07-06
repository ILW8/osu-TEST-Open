// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Online.Chat;

namespace osu.Game.TournamentIpc
{
    // am I being paranoid with the locks? Not familiar with threading model in C#
    public partial class TournamentFileBasedIPC : Component
    {
        private const string file_ipc_filename = @"ipc.txt";
        private const string file_ipc_state_filename = @"ipc-state.txt";
        private const string file_ipc_scores_filename = @"ipc-scores.txt";
        private const string file_ipc_chat_filename = @"ipc-chat.txt";

        private readonly object mtx = new object();
        private bool dirty = false;

        private Storage tournamentStorage = null!;
        private ScheduledDelegate? scheduled;

        private readonly List<Message> chatMessages = new List<Message>();

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            tournamentStorage = storage.GetStorageForDirectory(@"tournaments");

            scheduled?.Cancel();
            scheduled = Scheduler.AddDelayed(delegate
            {
                lock (mtx)
                {
                    Logger.Log(@$"chat has {chatMessages.Count} messages", LoggingTarget.Runtime, LogLevel.Debug);
                    if (!dirty)
                        return;

                    using (var chatIpcStream = tournamentStorage.CreateFileSafely(file_ipc_chat_filename))
                    using (var chatIpcStreamWriter = new StreamWriter(chatIpcStream))
                    {
                        foreach (var message in chatMessages)
                        {
                            chatIpcStreamWriter.Write($"{message.Timestamp.ToUnixTimeMilliseconds()},{message.Sender.Username},{message.Sender.Id},{message.Content}\n");
                        }
                    }

                    dirty = false;
                }
            }, 500, true);
        }

        public void AddChatMessage(Message message)
        {
            chatMessages.Add(message);
            lock (mtx)
                dirty = true;
        }

        public void ClearChatMessages()
        {
            chatMessages.Clear();
            lock (mtx)
                dirty = true;
        }
    }
}
