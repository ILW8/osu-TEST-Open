// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.HUD;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Participants
{
    public partial class ParticipantPanel : CompositeDrawable, IHasContextMenu, IHasPopover
    {
        public readonly MultiplayerRoomUser User;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private SpriteIcon crown = null!;

        private OsuSpriteText userRankText = null!;
        private ModDisplay userModsDisplay = null!;
        private StateDisplay userStateDisplay = null!;
        private OsuSpriteText userSlotText = null!;

        private IconButton kickButton = null!;

        private int participantSlotNumber;

        public int ParticipantSlotNumber
        {
            get => participantSlotNumber;
            set
            {
                participantSlotNumber = value;
                userSlotText.Text = value > 0 ? $"{value}" : "";
            }
        }

        public ParticipantPanel(MultiplayerRoomUser user, int slot = -1)
        {
            User = user;
            participantSlotNumber = slot;

            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var user = User.User;

            var backgroundColour = Color4Extensions.FromHex("#33413C");

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 18),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        crown = new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.Crown,
                            Size = new Vector2(14),
                            Colour = Color4Extensions.FromHex("#F7E65D"),
                            Alpha = 0
                        },
                        userSlotText = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 12),
                            Text = ParticipantSlotNumber > 0 ? $"{ParticipantSlotNumber}" : ""
                        },
                        new TeamDisplay(User),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = 5,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = backgroundColour
                                },
                                new UserCoverBackground
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0.75f,
                                    User = user,
                                    Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0), Color4.White.Opacity(0.25f))
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Spacing = new Vector2(10),
                                    Direction = FillDirection.Horizontal,
                                    Children = new Drawable[]
                                    {
                                        new UpdateableAvatar
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            RelativeSizeAxes = Axes.Both,
                                            FillMode = FillMode.Fit,
                                            User = user
                                        },
                                        new UpdateableFlag
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Size = new Vector2(28, 20),
                                            CountryCode = user?.CountryCode ?? default
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 18),
                                            Text = user?.Username ?? string.Empty
                                        },
                                        userRankText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Font = OsuFont.GetFont(size: 14),
                                        }
                                    }
                                },
                                new Container
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Margin = new MarginPadding { Right = 70 },
                                    Child = userModsDisplay = new ModDisplay
                                    {
                                        Scale = new Vector2(0.5f),
                                        ExpansionMode = ExpansionMode.AlwaysContracted,
                                    }
                                },
                                userStateDisplay = new StateDisplay
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Margin = new MarginPadding { Right = 10 },
                                }
                            }
                        },
                        kickButton = new KickButton
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Alpha = 0,
                            Margin = new MarginPadding(4),
                            Action = () => client.KickUser(User.UserID).FireAndForget(),
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.RoomUpdated += onRoomUpdated;
            updateState();
        }

        private void onRoomUpdated() => Scheduler.AddOnce(updateState);

        private void updateState()
        {
            if (client.Room == null || client.LocalUser == null)
                return;

            if (client.Room.MatchState is TeamVersusRoomState)
            {
                var teamRedUsers = client.Room.Users.Where(u =>
                {
                    if (u.MatchState is TeamVersusUserState teamVersusUserState)
                        return teamVersusUserState.TeamID == 0;

                    return false;
                });

                var teamBlueUsers = client.Room.Users.Where(u =>
                {
                    if (u.MatchState is TeamVersusUserState teamVersusUserState)
                        return teamVersusUserState.TeamID == 1;

                    return false;
                });

                var multiplayerRoomUsers = teamRedUsers as MultiplayerRoomUser[] ?? teamRedUsers.ToArray();
                foreach (var user in multiplayerRoomUsers)
                    client.Room.Users.Remove(user);
                client.Room.Users.AddRange(multiplayerRoomUsers);

                var blueUsers = teamBlueUsers as MultiplayerRoomUser[] ?? teamBlueUsers.ToArray();
                foreach (var user in blueUsers)
                    client.Room.Users.Remove(user);
                client.Room.Users.AddRange(blueUsers);
            }

            // move spectators to very bottom
            for (int i = client.Room.Users.Count - 1; i >= 0; i--)
            {
                if (client.Room.Users[i].State != MultiplayerUserState.Spectating)
                    continue;

                var user = client.Room.Users[i];
                client.Room.Users.RemoveAt(i);
                client.Room.Users.Add(user);
            }

            const double fade_time = 50;

            MultiplayerPlaylistItem? currentItem = client.Room.GetCurrentItem();
            Ruleset? ruleset = currentItem != null ? rulesets.GetRuleset(currentItem.RulesetID)?.CreateInstance() : null;

            int? currentModeRank = ruleset != null ? User.User?.RulesetsStatistics?.GetValueOrDefault(ruleset.ShortName)?.GlobalRank : null;
            userRankText.Text = currentModeRank != null ? $"#{currentModeRank.Value:N0}" : string.Empty;

            userStateDisplay.UpdateStatus(User.State, User.BeatmapAvailability);

            if ((User.BeatmapAvailability.State == DownloadState.LocallyAvailable) && (User.State != MultiplayerUserState.Spectating))
                userModsDisplay.FadeIn(fade_time);
            else
                userModsDisplay.FadeOut(fade_time);

            kickButton.Alpha = client.IsHost && !User.Equals(client.LocalUser) ? 1 : 0;
            crown.Alpha = client.Room.Host?.Equals(User) == true ? 1 : 0;

            ParticipantSlotNumber = client.Room.Users.IndexOf(User) + 1;

            // If the mods are updated at the end of the frame, the flow container will skip a reflow cycle: https://github.com/ppy/osu-framework/issues/4187
            // This looks particularly jarring here, so re-schedule the update to that start of our frame as a fix.
            Schedule(() =>
            {
                userModsDisplay.Current.Value = ruleset != null ? User.Mods.Select(m => m.ToMod(ruleset)).ToList() : Array.Empty<Mod>();
            });
        }

        public MenuItem[]? ContextMenuItems
        {
            get
            {
                if (client.Room == null)
                    return null;

                // always allow moving slots regardless of host status
                List<MenuItem> menuItems = new List<MenuItem> { new OsuMenuItem("Move to slot (client side)", MenuItemType.Standard, this.ShowPopover) };

                // If the local user is not the host of the room.
                if (client.Room.Host?.UserID != api.LocalUser.Value.Id)
                    return menuItems.ToArray();

                int targetUser = User.UserID;
                menuItems.AddRange(new MenuItem[]
                {
                    new OsuMenuItem("Give host", MenuItemType.Standard, () =>
                    {
                        // Ensure the local user is still host.
                        if (!client.IsHost)
                            return;

                        client.TransferHost(targetUser).FireAndForget();
                    }),
                    new OsuMenuItem("Kick", MenuItemType.Destructive, () =>
                    {
                        // Ensure the local user is still host.
                        if (!client.IsHost)
                            return;

                        client.KickUser(targetUser).FireAndForget();
                    })
                });

                return menuItems.ToArray();
            }
        }

        public Popover GetPopover() => new SlotEntryPopover(User);

        public partial class SlotEntryPopover : OsuPopover
        {
            public override bool HandleNonPositionalInput => true;

            protected override bool BlockNonPositionalInput => true;

            private OsuNumberBox slotTextBox = null!;
            private RoundedButton moveButton = null!;
            private OsuSpriteText errorText = null!;
            private Sample? sampleJoinFail;
            private readonly MultiplayerRoomUser user;

            [Resolved]
            protected MultiplayerClient Client { get; private set; } = null!;

            public SlotEntryPopover(MultiplayerRoomUser user)
            {
                this.user = user;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours, AudioManager audio)
            {
                Child = new FillFlowContainer
                {
                    Margin = new MarginPadding(10),
                    Spacing = new Vector2(5),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    LayoutDuration = 500,
                    LayoutEasing = Easing.OutQuint,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(5),
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                slotTextBox = new OsuNumberBox
                                {
                                    Width = 160,
                                    PlaceholderText = "slot number",
                                },
                                moveButton = new RoundedButton
                                {
                                    Width = 240,
                                    Text = $@"Move {user.User?.Username ?? ""}",
                                }
                            }
                        },
                        errorText = new OsuSpriteText
                        {
                            Colour = colours.Red,
                        },
                    }
                };

                sampleJoinFail = audio.Samples.Get(@"UI/generic-error");

                moveButton.Action = performMove;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                ScheduleAfterChildren(() => GetContainingFocusManager()?.ChangeFocus(slotTextBox));
                slotTextBox.OnCommit += (_, _) => performMove();
            }

            private void performMove()
            {
                int newSlot;

                try
                {
                    newSlot = int.Parse(slotTextBox.Text);
                    newSlot--;
                }
                catch (FormatException)
                {
                    moveFailed($@"couldn't parse {slotTextBox.Text} as integer");
                    return;
                }
                catch (OverflowException)
                {
                    moveFailed($@"{slotTextBox.Text} is too large");
                    return;
                }

                var room = Client.Room;

                if (room == null)
                {
                    moveFailed(@"room is null");
                    return;
                }

                newSlot = Math.Max(newSlot, 0);
                newSlot = Math.Min(newSlot, room.Users.Count - 1);

                room.Users.Remove(user);
                room.Users.Insert(newSlot, user);
                Client.ForceInvokeRoomUpdated();
                GetContainingFocusManager()?.TriggerFocusContention(slotTextBox);
            }

            private void moveFailed(string error) => Schedule(() =>
            {
                slotTextBox.Text = string.Empty;

                GetContainingFocusManager()?.ChangeFocus(slotTextBox);

                errorText.Text = error;
                errorText
                    .FadeIn()
                    .FlashColour(Color4.White, 200)
                    .Delay(1000)
                    .FadeOutFromOne(1000, Easing.In);

                Body.Shake();

                sampleJoinFail?.Play();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.RoomUpdated -= onRoomUpdated;
        }

        public partial class KickButton : IconButton
        {
            public KickButton()
            {
                Icon = FontAwesome.Solid.UserTimes;
                TooltipText = "Kick";
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                IconHoverColour = colours.Red;
            }
        }
    }
}
