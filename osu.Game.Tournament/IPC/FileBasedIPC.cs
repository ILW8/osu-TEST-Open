// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Tournament.IO;
using osu.Game.Tournament.Models;

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

        public Bindable<string> ChatChannel { get; } = new Bindable<string>();
        public BindableLong Score1 { get; } = new BindableLong();
        public BindableLong Score2 { get; } = new BindableLong();
    }

    public partial class FileBasedIPC : MatchIPCInfo
    {
        public Storage AllTournamentsStorage { get; private set; } = null!;

        [Resolved]
        protected IAPIProvider API { get; private set; } = null!;

        [Resolved]
        protected IRulesetStore Rulesets { get; private set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(TournamentStorage tournamentStorage)
        {
            AllTournamentsStorage = tournamentStorage.AllTournaments;
            Logger.Log($"ipc storage path: {AllTournamentsStorage.GetFullPath(string.Empty)}");
            string thestr = AllTournamentsStorage.Exists("ipc.txt") ? "file ipc.txt found in game storage yay" : "no ipc.txt found in game storage, uh oh";
            Logger.Log(thestr, LoggingTarget.Runtime, LogLevel.Debug);
        }
    }
}
