// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Overlays.Chat;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Components
{
    public partial class TournamentMatchChatDisplay : StandAloneChatDisplay
    {
        private readonly Bindable<string> chatChannel = new Bindable<string>();
        private readonly BindableList<Message> lazerIpcChatMessages = new BindableList<Message>();

        private ChannelManager? manager;

        [Resolved]
        private LadderInfo ladderInfo { get; set; } = null!;

        public TournamentMatchChatDisplay()
        {
            RelativeSizeAxes = Axes.X;
            Height = 144;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            CornerRadius = 0;
        }

        [BackgroundDependencyLoader]
        private void load(LegacyMatchIPCInfo? legacyIpc, MatchIPCInfo? lazerIpc, IAPIProvider api)
        {
            if (lazerIpc != null)
                lazerIpcChatMessages.BindTo(lazerIpc.ChatMessages);

            ladderInfo.UseLazerIpc.BindValueChanged(useLazerIpcValueChanged =>
            {
                switch (useLazerIpcValueChanged.NewValue)
                {
                    case false:
                    {
                        lazerIpcChatMessages.UnbindEvents();

                        if (legacyIpc == null) return;

                        chatChannel.BindTo(legacyIpc.ChatChannel);
                        chatChannel.BindValueChanged(c =>
                        {
                            if (string.IsNullOrWhiteSpace(c.NewValue))
                                return;

                            int id = int.Parse(c.NewValue);

                            if (id <= 0) return;

                            if (manager == null)
                            {
                                AddInternal(manager = new ChannelManager(api));
                            }

                            Channel.BindTo(manager.CurrentChannel);

                            foreach (var ch in manager.JoinedChannels.ToList())
                                manager.LeaveChannel(ch);

                            var channel = new Channel
                            {
                                Id = id,
                                Type = ChannelType.Public
                            };

                            manager.JoinChannel(channel);
                            manager.CurrentChannel.Value = channel;
                        }, true);
                        break;
                    }

                    case true:
                    {
                        chatChannel.UnbindAll();
                        Channel.UnbindBindings();
                        manager?.Dispose();
                        Channel.Value = new Channel();

                        lazerIpcChatMessages.BindCollectionChanged((_, collectionChangedEventArgs) =>
                        {
                            if (collectionChangedEventArgs.NewItems == null || collectionChangedEventArgs.NewItems.Count == 0)
                            {
                                Channel.Value = new Channel();
                                return;
                            }

                            Channel.Value.AddNewMessages(collectionChangedEventArgs.NewItems.OfType<Message>().ToArray());
                        }, true);
                        break;
                    }
                }
            }, true);
        }

        public void Expand() => this.FadeIn(300);

        public void Contract() => this.FadeOut(200);

        protected override ChatLine CreateMessage(Message message) => new MatchMessage(message, ladderInfo);

        protected override StandAloneDrawableChannel CreateDrawableChannel(Channel channel) => new MatchChannel(channel);

        public partial class MatchChannel : StandAloneDrawableChannel
        {
            public MatchChannel(Channel channel)
                : base(channel)
            {
                ScrollbarVisible = false;
            }
        }

        protected partial class MatchMessage : StandAloneMessage
        {
            public MatchMessage(Message message, LadderInfo info)
                : base(message)
            {
                if (info.CurrentMatch.Value is TournamentMatch match)
                {
                    if (match.Team1.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentGame.COLOUR_RED;
                    else if (match.Team2.Value?.Players.Any(u => u.OnlineID == Message.Sender.OnlineID) == true)
                        UsernameColour = TournamentGame.COLOUR_BLUE;
                }
            }
        }
    }
}
