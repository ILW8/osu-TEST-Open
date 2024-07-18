// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;

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

        private MultiplayerClient? multiplayerClient;

        public Bindable<TourneyState> TourneyState { get; } = new Bindable<TourneyState>();
        // private MultiplayerRoomState? lastRoomState = null;

        private readonly BindableList<Message> chatMessages = new BindableList<Message>();

        private long[] pendingScores = [];

        private ScheduledDelegate? flushScoresDelegate;

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

            Logger.Log($"watching for tourney state changes");

            TourneyState.BindValueChanged(vce =>
            {
                using var mainIpc = tournamentStorage.CreateFileSafely(IpcFiles.STATE);
                using var mainIpcStreamWriter = new StreamWriter(mainIpc);

                Logger.Log($"tourney state changed to: {vce.NewValue}");
                mainIpcStreamWriter.Write($"{(int)vce.NewValue}\n");
            }, true);

            flushScoresDelegate?.Cancel();
            flushScoresDelegate = Scheduler.AddDelayed(flushPendingScoresToDisk, 200, true);
        }

        public void AddChatMessage(Message message)
        {
            chatMessages.Add(message);
        }

        public void ClearChatMessages()
        {
            chatMessages.Clear();
        }

        public void UpdateTeamScores(long[] scores)
        {
            pendingScores = scores;
        }

        public void RegisterMultiplayerRoomClient(MultiplayerClient multiplayerClient)
        {
            if (this.multiplayerClient != null)
                this.multiplayerClient.RoomUpdated -= onRoomUpdated;

            this.multiplayerClient = multiplayerClient;
            this.multiplayerClient.RoomUpdated += onRoomUpdated;
            onRoomUpdated();
        }

        private void onRoomUpdated()
        {
            var newRoomState = multiplayerClient?.Room?.State ?? MultiplayerRoomState.Closed;

            // if (lastRoomState == newRoomState)
            //     return;
            //
            // lastRoomState = newRoomState;

            switch (newRoomState)
            {
                case MultiplayerRoomState.WaitingForLoad:
                case MultiplayerRoomState.Playing:
                    TourneyState.Value = TournamentIpc.TourneyState.Playing;
                    break;

                default:
                    // there is at least one user in results screen
                    if (multiplayerClient?.Room?.Users.FirstOrDefault(u => u.State == MultiplayerUserState.Results) != null
                        && multiplayerClient?.LocalUser?.State != MultiplayerUserState.Idle
                        && TourneyState.Value != TournamentIpc.TourneyState.Lobby)
                    {
                        Logger.Log($"(room updated) tourney state changed to: {TournamentIpc.TourneyState.Ranking}");
                        TourneyState.Value = TournamentIpc.TourneyState.Ranking;
                        break;
                    }

                    TourneyState.Value = TournamentIpc.TourneyState.Lobby;
                    break;
            }
        }

        private void flushPendingScoresToDisk()
        {
            if (pendingScores.Length == 0)
                return;

            var scoresToWrite = pendingScores.ToList();

            // ensure there is always at least 2 scores to write
            if (scoresToWrite.Count == 1)
                scoresToWrite.Add(0);

            using (var scoresIpc = tournamentStorage.CreateFileSafely(IpcFiles.SCORES))
            using (var scoresIpcWriter = new StreamWriter(scoresIpc))
            {
                foreach (long score in scoresToWrite)
                {
                    scoresIpcWriter.Write($"{score}\n");
                }
            }

            pendingScores = [];
        }
    }
}
