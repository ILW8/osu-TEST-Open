// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Online.Multiplayer;
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

        protected override void LoadComplete()
        {
            base.LoadComplete();
            multiplayerClient.RoomUpdated += onRoomUpdated;

            Message.RoomName.BindValueChanged(valueChangedEvent =>
            {
                if (valueChangedEvent.OldValue == valueChangedEvent.NewValue)
                    return;

                Broadcast();
            });

            Broadcast();
        }

        protected override void Dispose(bool isDisposing)
        {
            multiplayerClient.RoomUpdated -= onRoomUpdated;

            base.Dispose(isDisposing);
        }

        private void onRoomUpdated()
        {
            var newState = multiplayerClient.Room?.State ?? MultiplayerRoomState.Closed;
            if (newState == Message.RoomState)
                return;

            Message.RoomState = newState;

            Broadcast();
        }
    }

    public class MultiplayerRoomWsState
    {
        public readonly Bindable<string> RoomName = new Bindable<string>();
        public MultiplayerRoomState RoomState = MultiplayerRoomState.Open;
    }
}
