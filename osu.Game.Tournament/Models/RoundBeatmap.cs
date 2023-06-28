// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using Newtonsoft.Json;
using osu.Framework.Bindables;

namespace osu.Game.Tournament.Models
{
    public enum WinCondition
    {
        Accuracy,
        MissCount
    }
    public class RoundBeatmap
    {
        public int ID;
        public string Mods;
        public Bindable<WinCondition> WinCondition = new Bindable<WinCondition>();

        [JsonProperty("BeatmapInfo")]
        public TournamentBeatmap Beatmap;
    }
}
