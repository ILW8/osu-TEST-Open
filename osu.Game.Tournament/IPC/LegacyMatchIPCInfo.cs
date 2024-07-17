// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.IPC
{
    public partial class LegacyMatchIPCInfo : Component
    {
        public Bindable<TournamentBeatmap?> Beatmap { get; } = new Bindable<TournamentBeatmap?>();
        public Bindable<LegacyMods> Mods { get; } = new Bindable<LegacyMods>();
        public Bindable<LegacyTourneyState> State { get; } = new Bindable<LegacyTourneyState>();
        public Bindable<string> ChatChannel { get; } = new Bindable<string>();
        public BindableLong Score1 { get; } = new BindableLong();
        public BindableLong Score2 { get; } = new BindableLong();
    }
}
