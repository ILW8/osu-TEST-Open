// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Online.Broadcasts
{
    public partial class MultiplayerGameplayStateBroadcaster : GameStateBroadcaster<MultiplayerGameplayRoomState>
    {
        public override string Type => @"MultiplayerGameplay";
        public sealed override MultiplayerGameplayRoomState Message { get; } = new MultiplayerGameplayRoomState();

        private readonly Dictionary<int, SpectatorScoreProcessor> scoreProcessors = new Dictionary<int, SpectatorScoreProcessor>();
        private readonly Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData> trackedUsers;

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        public MultiplayerGameplayStateBroadcaster(Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData> trackedUsers)
        {
            this.trackedUsers = trackedUsers;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var item in trackedUsers.Select((value, i) => new { i, value }))
            {
                var trackedUser = item.value;
                var yes = Message.PlayerStates[trackedUser.Value.ScoreProcessor.UserId] = new GameplayPlayerState(trackedUser.Value.User,
                              item.i,
                              trackedUser.Value.Team);

                yes.TotalScore.BindTo(trackedUser.Value.ScoreProcessor.TotalScore);
                yes.TotalScore.ValueChanged += _ => Broadcast();

                yes.Accuracy.BindTo(trackedUser.Value.ScoreProcessor.Accuracy);
                yes.Accuracy.ValueChanged += _ => Broadcast();

                yes.Combo.BindTo(trackedUser.Value.ScoreProcessor.Combo);
                yes.Combo.ValueChanged += _ => Broadcast();

                trackedUser.Value.ScoreProcessor.ModsBindable.BindValueChanged(valueChangedEvent =>
                {
                    yes.Mods.Clear();
                    yes.Mods.AddRange(valueChangedEvent.NewValue);
                }, true);

                yes.HighestCombo.BindTo(trackedUser.Value.ScoreProcessor.HighestCombo);
                yes.HighestCombo.ValueChanged += _ => Broadcast();
            }

            multiplayerClient.RoomUpdated += onRoomUpdated;
        }

        protected override void Dispose(bool isDisposing)
        {
            multiplayerClient.RoomUpdated -= onRoomUpdated;

            base.Dispose(isDisposing);
        }

        private void onRoomUpdated()
        {
            foreach (MultiplayerRoomUser roomUser in multiplayerClient.Room?.Users ?? System.Array.Empty<MultiplayerRoomUser>())
            {
                if (Message.PlayerStates.TryGetValue(roomUser.UserID, out var gameplayPlayerState))
                {
                    gameplayPlayerState.UserState = roomUser.State;
                }
            }

            Broadcast();
        }
    }

    public class MultiplayerGameplayRoomState
    {
        public readonly Dictionary<int, GameplayPlayerState> PlayerStates = new Dictionary<int, GameplayPlayerState>();
    }

    public class GameplayPlayerState : RoomPlayerState
    {
        public GameplayPlayerState(MultiplayerRoomUser user, int slotIndex, int? teamID)
            : base(user, slotIndex, teamID)
        {
        }

        public readonly BindableLong TotalScore = new BindableLong();
        public readonly BindableDouble Accuracy = new BindableDouble();
        public readonly BindableInt Combo = new BindableInt();
        public readonly List<Mod> Mods = new List<Mod>();
        public readonly BindableInt HighestCombo = new BindableInt();
    }
}
