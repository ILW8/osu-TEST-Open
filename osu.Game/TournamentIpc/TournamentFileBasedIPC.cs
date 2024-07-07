// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Online.Chat;

namespace osu.Game.TournamentIpc
{
    public static class IpcFiles
    {
        public const string BEATMAP = @"ipc.txt";
        public const string STATE = @"ipc-state.txt";
        public const string SCORES = @"ipc-scores.txt";
        public const string CHAT = @"ipc-chat.txt";
    }

    // am I being paranoid with the locks? Not familiar with threading model in C#
    public partial class TournamentFileBasedIPC : Component
    {
        private Storage tournamentStorage = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        private readonly BindableList<Message> chatMessages = new BindableList<Message>();

        [BackgroundDependencyLoader]
        private void load(Storage storage)
        {
            tournamentStorage = storage.GetStorageForDirectory(@"tournaments");

            chatMessages.BindCollectionChanged((_, changedEventArgs) =>
            {
                // truncate file on disk
                if (changedEventArgs.NewItems == null || changedEventArgs.NewItems.Count == 0)
                {
                    using var chatIpcStream = tournamentStorage.CreateFileSafely(IpcFiles.CHAT);
                    chatIpcStream.SetLength(0);
                    Logger.Log(@"[FileIPC] Truncated chat messages on file");
                    return;
                }

                // else append to file normally
                using var chatAppendIpcStream = tournamentStorage.GetStream(IpcFiles.CHAT, FileAccess.Write, FileMode.Append);
                using var chatIpcStreamWriter = new StreamWriter(chatAppendIpcStream);

                foreach (var message in changedEventArgs.NewItems.OfType<Message>())
                {
                    chatIpcStreamWriter.Write($"{message.Timestamp.ToUnixTimeMilliseconds()},{message.Sender.Username},{message.Sender.Id},{message.Content}\n");
                }

                Logger.Log($@"[FileIPC] Wrote {changedEventArgs.NewItems.Count} message(s) to file");
            }, true);

            workingBeatmap.BindValueChanged(vce =>
            {
                using (var mainIpc = tournamentStorage.CreateFileSafely(IpcFiles.BEATMAP))
                using (var mainIpcStreamWriter = new StreamWriter(mainIpc))
                {
                    mainIpcStreamWriter.Write($"{vce.NewValue.BeatmapInfo.OnlineID}\n");
                }
            });
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
