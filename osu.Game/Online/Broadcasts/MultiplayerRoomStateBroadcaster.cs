// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;

namespace osu.Game.Online.Broadcasts
{
    public partial class MultiplayerRoomStateBroadcaster : GameStateBroadcaster<MultiplayerRoomWsState>
    {
        public override string Type => @"MultiplayerRoomState";
        public sealed override MultiplayerRoomWsState Message { get; } = new MultiplayerRoomWsState();

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        public MultiplayerRoomStateBroadcaster(Room room)
        {
            Message.RoomName.BindTo(room.Name);
        }

        private void broadcast()
        {
            Logger.Log($@"RoomState: {Message.RoomState}, {JsonConvert.SerializeObject(this, SerializationSettings)}");
            Broadcast();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Message.RoomName.BindValueChanged(valueChangedEvent =>
            {
                if (valueChangedEvent.OldValue == valueChangedEvent.NewValue)
                    return;

                broadcast();
            });

            multiplayerClient.RoomUpdated += onRoomUpdated;
            onRoomUpdated();
            broadcast();
        }

        protected override void Dispose(bool isDisposing)
        {
            multiplayerClient.RoomUpdated -= onRoomUpdated;

            base.Dispose(isDisposing);
        }

        private void onRoomUpdated()
        {
            var newState = multiplayerClient.Room?.State ?? MultiplayerRoomState.Closed;

            Message.RoomState = newState;

            // // todo: update lobby users here too
            // foreach (int key in Message.RoomUsers.Keys.Except((multiplayerClient.Room?.Users ?? Array.Empty<MultiplayerRoomUser>()).Select(mpRoomUser => mpRoomUser.UserID).ToList()).ToList())
            // {
            //     Message.RoomUsers.Remove(key);
            // }
            //
            //
            // foreach (MultiplayerRoomUser roomUser in multiplayerClient.Room?.Users ?? Array.Empty<MultiplayerRoomUser>())
            // {
            //     if (Message.RoomUsers.TryGetValue(roomUser.UserID, out var gameplayPlayerState))
            //     {
            //         gameplayPlayerState.UserState = roomUser.State;
            //         gameplayPlayerState.TeamID = (roomUser.MatchState as TeamVersusUserState)?.TeamID;
            //     }
            // }

            // efficient? no, but I don't care anymore
            Message.RoomUsers.Clear();

            foreach (var item in (multiplayerClient.Room?.Users ?? Array.Empty<MultiplayerRoomUser>()).Select((value, i) => new { i, value }))
            {
                var roomUser = item.value;
                Message.RoomUsers[roomUser.UserID] = new RoomPlayerState(
                    roomUser,
                    item.i,
                    (roomUser.MatchState as TeamVersusUserState)?.TeamID)
                {
                    UserState = roomUser.State
                };
            }

            broadcast();
        }
    }

    public class MultiplayerRoomWsState
    {
        public readonly Bindable<string> RoomName = new Bindable<string>();
        public MultiplayerRoomState RoomState = MultiplayerRoomState.Open;
        public readonly Dictionary<int, RoomPlayerState> RoomUsers = new Dictionary<int, RoomPlayerState>();
    }

    public class RoomPlayerState
    {
        public RoomPlayerState(MultiplayerRoomUser user, int slotIndex, int? teamID)
        {
            Username = user.User?.Username ?? @"%% unknown user %%";
            UserID.Value = user.User?.OnlineID ?? 0;
            SlotIndex.Value = slotIndex;
            TeamID = teamID;
        }

        public readonly string Username;
        public readonly BindableInt UserID = new BindableInt(); // using BindableInt instead of int because int doesn't appear when value == 0 in output json...
        public int? TeamID;
        public readonly BindableInt SlotIndex = new BindableInt();
        public MultiplayerUserState UserState;
    }
}
