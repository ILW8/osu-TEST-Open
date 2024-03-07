// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
// using osu.Framework.Logging;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Online.Broadcasts
{
    public partial class MultiplayerRoomStateBroadcaster : GameStateBroadcaster<TheRealMultiplayerRoomState>
    {
        public override string Type => @"MultiplayerRoomState";
        public override TheRealMultiplayerRoomState Message { get; } = new TheRealMultiplayerRoomState();

        private readonly Dictionary<int, SpectatorScoreProcessor> scoreProcessors = new Dictionary<int, SpectatorScoreProcessor>();

        public MultiplayerRoomStateBroadcaster(IEnumerable<SpectatorScoreProcessor> scores)
        {
            foreach (var scoreProcessor in scores)
            {
                scoreProcessors[scoreProcessor.UserId] = scoreProcessor;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Message.TotalScore.BindTo(scoreProcessors.HighestCombo);
            // Message.TotalScore.ValueChanged += _ => Broadcast();
            //
            // Message.Accuracy.BindTo(scoreProcessors.Accuracy);
            // Message.Accuracy.ValueChanged += _ => Broadcast();
            //
            // Message.Combo.BindTo(scoreProcessors.Combo);
            // Message.Combo.ValueChanged += _ => Broadcast();
            //
            // Message.Mods.BindTo(scoreProcessors.Mods);
            // Message.Mods.ValueChanged += _ => Broadcast();
            //
            // Message.HighestCombo.BindTo(scoreProcessors.HighestCombo);
            // Message.HighestCombo.ValueChanged += _ => Broadcast();
            //
            // Message.Rank.BindTo(scoreProcessors.Rank);
            // Message.Rank.ValueChanged += _ => Broadcast();

            foreach (var scoreProcessor in scoreProcessors)
            {
                var yes = Message.PlayerStates[scoreProcessor.Value.UserId] = new TheOtherMultiplayerRoomState();

                yes.TotalScore.BindTo(scoreProcessor.Value.TotalScore);
                yes.TotalScore.ValueChanged += _ => Broadcast();

                yes.Accuracy.BindTo(scoreProcessor.Value.Accuracy);
                yes.Accuracy.ValueChanged += _ => Broadcast();

                yes.Combo.BindTo(scoreProcessor.Value.Combo);
                yes.Combo.ValueChanged += _ => Broadcast();

                yes.Mods.BindTo(new Bindable<IReadOnlyList<Mod>>(scoreProcessor.Value.Mods));

                yes.HighestCombo.BindTo(scoreProcessor.Value.HighestCombo);
                yes.HighestCombo.ValueChanged += _ => Broadcast();
            }
        }
    }

    public class TheRealMultiplayerRoomState
    {
        public readonly Dictionary<int, TheOtherMultiplayerRoomState> PlayerStates = new Dictionary<int, TheOtherMultiplayerRoomState>();
    }

    public class TheOtherMultiplayerRoomState
    {
        public readonly BindableLong TotalScore = new BindableLong();
        public readonly BindableDouble Accuracy = new BindableDouble();
        public readonly BindableInt Combo = new BindableInt();
        public readonly Bindable<IReadOnlyList<Mod>> Mods = new Bindable<IReadOnlyList<Mod>>();
        public readonly BindableInt HighestCombo = new BindableInt();
    }
}
