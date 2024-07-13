// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Rulesets;
using osu.Game.Tournament.IO;
using osu.Game.Tournament.Models;
using osu.Game.TournamentIpc;

namespace osu.Game.Tournament.IPC
{
    public enum TourneyState
    {
        Lobby,
        Gameplay,
        Playing,
        Ranking
    }

    public partial class MatchIPCInfo : Component
    {
        public Bindable<TournamentBeatmap?> Beatmap { get; } = new Bindable<TournamentBeatmap?>();

        // let's ignore mods for now
        // public Bindable<LegacyMods> Mods { get; } = new Bindable<LegacyMods>();

        public Bindable<TourneyState> State { get; } = new Bindable<TourneyState>();

        public BindableList<Message> ChatMessages { get; } = new BindableList<Message>();
        public BindableLong Score1 { get; } = new BindableLong();
        public BindableLong Score2 { get; } = new BindableLong();
    }

    public partial class FileBasedIPC : MatchIPCInfo
    {
        public Storage IPCStorage { get; private set; } = null!;

        [Resolved]
        protected IAPIProvider API { get; private set; } = null!;

        [Resolved]
        protected IRulesetStore Rulesets { get; private set; } = null!;

        // [Resolved]
        // private GameHost host { get; set; } = null!;

        [Resolved]
        private LadderInfo ladder { get; set; } = null!;

        private int lastBeatmapId;
        private ScheduledDelegate? scheduled;
        private GetBeatmapRequest? beatmapLookupRequest;

        [BackgroundDependencyLoader]
        private void load(TournamentStorage tournamentStorage)
        {
            IPCStorage = tournamentStorage.AllTournaments;
            Logger.Log($"ipc storage path: {IPCStorage.GetFullPath(string.Empty)}");
            string thestr = IPCStorage.Exists("ipc.txt") ? "file ipc.txt found in game storage yay" : "no ipc.txt found in game storage, uh oh";
            Logger.Log(thestr, LoggingTarget.Runtime, LogLevel.Debug);

            if (IPCStorage.Exists("ipc.txt") && ladder.UseLazerIpc.Value)
            {
                scheduled = Scheduler.AddDelayed(delegate
                {
                    try
                    {
                        using (var stream = IPCStorage.GetStream(IpcFiles.BEATMAP))
                        using (var sr = new StreamReader(stream))
                        {
                            int beatmapId = int.Parse(sr.ReadLine().AsNonNull());

                            if (lastBeatmapId != beatmapId)
                            {
                                beatmapLookupRequest?.Cancel();

                                lastBeatmapId = beatmapId;

                                var existing = ladder
                                               .CurrentMatch.Value
                                               ?.Round.Value
                                               ?.Beatmaps
                                               .FirstOrDefault(b => b.ID == beatmapId);

                                if (existing != null)
                                    Beatmap.Value = existing.Beatmap;
                                else
                                {
                                    beatmapLookupRequest = new GetBeatmapRequest(new APIBeatmap { OnlineID = beatmapId });
                                    beatmapLookupRequest.Success += b =>
                                    {
                                        if (lastBeatmapId == beatmapId)
                                            Beatmap.Value = new TournamentBeatmap(b);
                                    };
                                    beatmapLookupRequest.Failure += _ =>
                                    {
                                        if (lastBeatmapId == beatmapId)
                                            Beatmap.Value = null;
                                    };
                                    API.Queue(beatmapLookupRequest);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // file might be in use
                    }

                    try
                    {
                        using (var stream = IPCStorage.GetStream(IpcFiles.CHAT))
                        using (var sr = new StreamReader(stream))
                        {
                            if (sr.Peek() == -1)
                            {
                                ChatMessages.Clear();
                            }

                            bool isFirstLine = true;

                            while (sr.ReadLine() is { } line)
                            {
                                string[] parts = line.Split(',');
                                if (parts.Length < 4) continue;

                                bool parseOk = long.TryParse(parts[0], out long ts);
                                parseOk &= int.TryParse(parts[2], out int uid);
                                if (!parseOk) continue;

                                if (isFirstLine)
                                {
                                    isFirstLine = false;
                                    ChatMessages.RemoveAll(msg => msg.Timestamp.ToUnixTimeMilliseconds() < ts);
                                }

                                if ((ChatMessages.LastOrDefault()?.Timestamp.ToUnixTimeMilliseconds() ?? 0) >= ts) continue;

                                Logger.Log($"added chat message {line}", LoggingTarget.Runtime, LogLevel.Debug);
                                ChatMessages.Add(new Message
                                {
                                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ts),
                                    Sender = new APIUser
                                    {
                                        Id = uid,
                                        Username = parts[1]
                                    },
                                    Content = string.Join(",", parts.Skip(3))
                                });
                            }
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        using var stream = IPCStorage.GetStream(IpcFiles.SCORES);
                        using var sr = new StreamReader(stream);

                        Score1.Value = long.Parse(sr.ReadLine().AsNonNull());
                        Score2.Value = long.Parse(sr.ReadLine().AsNonNull());
                    }
                    catch
                    {
                        Logger.Log("uh oh, couldn't read scores");
                        // file might be busy
                    }
                    // int beatmapId = int.Parse(sr.ReadLine().AsNonNull());
                    // int mods = int.Parse(sr.ReadLine().AsNonNull());
                    //
                    // if (lastBeatmapId != beatmapId)
                    // {
                    //     beatmapLookupRequest?.Cancel();
                    //
                    //     lastBeatmapId = beatmapId;
                    //
                    //     var existing = ladder.CurrentMatch.Value?.Round.Value?.Beatmaps.FirstOrDefault(b => b.ID == beatmapId);
                    //
                    //     if (existing != null)
                    //         Beatmap.Value = existing.Beatmap;
                    //     else
                    //     {
                    //         beatmapLookupRequest = new GetBeatmapRequest(new APIBeatmap { OnlineID = beatmapId });
                    //         beatmapLookupRequest.Success += b =>
                    //         {
                    //             if (lastBeatmapId == beatmapId)
                    //                 Beatmap.Value = new TournamentBeatmap(b);
                    //         };
                    //         beatmapLookupRequest.Failure += _ =>
                    //         {
                    //             if (lastBeatmapId == beatmapId)
                    //                 Beatmap.Value = null;
                    //         };
                    //         API.Queue(beatmapLookupRequest);
                    //     }
                    // }
                }, 250, true);
            }
        }
    }
}
