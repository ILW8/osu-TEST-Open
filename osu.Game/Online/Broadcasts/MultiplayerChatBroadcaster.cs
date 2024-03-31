// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Users;

namespace osu.Game.Online.Broadcasts
{
    public partial class MultiplayerChatBroadcaster : GameStateBroadcaster<MultiplayerChatState>
    {
        public override string Type => @"MultiplayerChatState";
        public override MultiplayerChatState Message { get; } = new MultiplayerChatState();

        [Resolved]
        protected MultiplayerClient Client { get; private set; } = null!;

        public void AddNewMessage(Message message)
        {
            var user = Client.Room?.Users.FirstOrDefault(u => u.UserID == message.Sender.Id) ?? null;
            int? teamId = user?.State == MultiplayerUserState.Spectating ? 2 : (user?.MatchState as TeamVersusUserState)?.TeamID;
            Message.ChatMessages.Add(new ChatMessage(message.Timestamp, message.Sender, message.Content, teamId));
        }

        protected override void LoadComplete()
        {
            Message.ChatMessages.CollectionChanged += (_, _) => Broadcast();
            Broadcast(); // in case we missed any messages between ctor and LoadComplete
        }

        protected override void Dispose(bool isDisposing)
        {
            Logger.Log("disposing mpcb");
            base.Dispose(isDisposing);
        }
    }

    public class MultiplayerChatState
    {
        public readonly BindableList<ChatMessage> ChatMessages = new BindableList<ChatMessage>();

        public void Clear()
        {
            ChatMessages.Clear();
        }
    }

    public class ChatMessage
    {
        public ChatMessage(DateTimeOffset messageTime, IUser user, string messageContent, int? teamId = null)
        {
            MessageTime = messageTime;
            SenderName = user.Username;
            MessageContent = messageContent;
            TeamId = teamId;
        }

        public readonly int? TeamId;
        public readonly DateTimeOffset MessageTime;
        public readonly string SenderName;
        public readonly string MessageContent;
    }
}
