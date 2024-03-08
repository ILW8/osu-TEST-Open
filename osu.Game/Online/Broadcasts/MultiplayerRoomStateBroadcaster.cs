// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Mods;

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

            foreach (var scoreProcessor in scoreProcessors)
            {
                var yes = Message.PlayerStates[scoreProcessor.Value.UserId] = new TheOtherMultiplayerRoomState();

                yes.TotalScore.BindTo(scoreProcessor.Value.TotalScore);
                yes.TotalScore.ValueChanged += _ => Broadcast();

                yes.Accuracy.BindTo(scoreProcessor.Value.Accuracy);
                yes.Accuracy.ValueChanged += _ => Broadcast();

                yes.Combo.BindTo(scoreProcessor.Value.Combo);
                yes.Combo.ValueChanged += _ => Broadcast();

                scoreProcessor.Value.ModsBindable.BindValueChanged(valueChangedEvent =>
                {
                    yes.Mods.Clear();
                    yes.Mods.AddRange(valueChangedEvent.NewValue);
                }, true);

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
        public readonly List<Mod> Mods = new List<Mod>();
        public readonly BindableInt HighestCombo = new BindableInt();
    }
}
