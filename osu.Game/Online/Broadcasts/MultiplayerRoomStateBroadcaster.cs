// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Users;

namespace osu.Game.Online.Broadcasts
{
    // todo: multiplayerGAMEPLAYstatebroadcaster instead? or MultiplayerSpectatorStateBroadcaster?
    public partial class MultiplayerRoomStateBroadcaster : GameStateBroadcaster<TheRealMultiplayerRoomState>
    {
        public override string Type => @"MultiplayerSpectatedPlayersState";
        public sealed override TheRealMultiplayerRoomState Message { get; } = new TheRealMultiplayerRoomState();

        private readonly Dictionary<int, SpectatorScoreProcessor> scoreProcessors = new Dictionary<int, SpectatorScoreProcessor>();
        private readonly Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData> trackedUsers;

        public MultiplayerRoomStateBroadcaster(Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData> trackedUsers)
        {
            this.trackedUsers = trackedUsers;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var item in trackedUsers.Select((value, i) => new { i, value }))
            {
                var trackedUser = item.value;
                var yes = Message.PlayerStates[trackedUser.Value.ScoreProcessor.UserId] = new RoomPlayerState(trackedUser.Value.User.User ?? new APIUser
                              {
                                  Id = 0,
                                  Username = @"??Unknown User??"
                              },
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
        }
    }

    public class TheRealMultiplayerRoomState
    {
        public readonly Dictionary<int, RoomPlayerState> PlayerStates = new Dictionary<int, RoomPlayerState>();
    }

    public class RoomPlayerState
    {
        public RoomPlayerState(IUser user, int slotIndex, int? teamID)
        {
            Username = user.Username;
            UserID.Value = user.OnlineID;
            SlotIndex.Value = slotIndex;
            TeamID = teamID;
        }

        public readonly string Username;
        public readonly BindableInt UserID = new BindableInt(); // using BindableInt instead of int because int doesn't appear when value == 0 in output json...
        public readonly int? TeamID;
        public readonly BindableInt SlotIndex = new BindableInt();
        public readonly BindableLong TotalScore = new BindableLong();
        public readonly BindableDouble Accuracy = new BindableDouble();
        public readonly BindableInt Combo = new BindableInt();
        public readonly List<Mod> Mods = new List<Mod>();
        public readonly BindableInt HighestCombo = new BindableInt();
    }
}
