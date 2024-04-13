// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Menu;
using osu.Game.Screens.OnlinePlay.Components;
using osu.Game.Screens.OnlinePlay.Match;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match;
using osu.Game.Screens.OnlinePlay.Multiplayer.Match.Playlist;
using osu.Game.Screens.OnlinePlay.Multiplayer.Participants;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;
using ParticipantsList = osu.Game.Screens.OnlinePlay.Multiplayer.Participants.ParticipantsList;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    public class PoolMap
    {
        [JsonProperty("beatmapID")]
        public int BeatmapID;

        [JsonProperty("mods")]
        public Dictionary<string, List<object>> Mods;

        [JsonIgnore]
        public List<Mod> ParsedMods;
    }

    [Serializable]
    public class Pool
    {
        [JsonProperty]
        public readonly BindableList<PoolMap> Beatmaps = new BindableList<PoolMap>();
    }

    public partial class ChatTimerHandler : Component
    {
        protected readonly MultiplayerCountdown MultiplayerChatTimerCountdown = new MatchStartCountdown { TimeRemaining = TimeSpan.Zero };
        protected double CountdownChangeTime;

        private TimeSpan countdownTimeRemaining
        {
            get
            {
                double timeElapsed = Time.Current - CountdownChangeTime;
                TimeSpan remaining;

                if (timeElapsed > MultiplayerChatTimerCountdown.TimeRemaining.TotalMilliseconds)
                    remaining = TimeSpan.Zero;
                else
                    remaining = MultiplayerChatTimerCountdown.TimeRemaining - TimeSpan.FromMilliseconds(timeElapsed);

                return remaining;
            }
        }

        [CanBeNull]
        private ScheduledDelegate countdownUpdateDelegate;

        [Resolved]
        protected MultiplayerClient Client { get; private set; }

        [Resolved]
        protected ChannelManager ChannelManager { get; private set; }

        protected Channel TargetChannel;

        public event Action<string> OnChatMessageDue;

        [BackgroundDependencyLoader]
        private void load()
        {
            Client.RoomUpdated += () =>
            {
                if (Client.Room?.State is MultiplayerRoomState.Open or MultiplayerRoomState.Results)
                    return; // only allow timer if room is idle

                if (countdownUpdateDelegate == null)
                    return;

                Logger.Log($@"Timer scheduled delegate called, room state is {Client.Room?.State}");
                countdownUpdateDelegate?.Cancel();
                countdownUpdateDelegate = null;
                OnChatMessageDue?.Invoke(@"Countdown aborted (game started)");
            };
        }

        public void SetTimer(TimeSpan duration, double startTime, Channel targetChannel)
        {
            // OnChatMessageDue = null;
            MultiplayerChatTimerCountdown.TimeRemaining = duration;
            CountdownChangeTime = startTime;
            TargetChannel = targetChannel;

            countdownUpdateDelegate?.Cancel();
            countdownUpdateDelegate = Scheduler.Add(sendTimerMessage);
        }

        private void processTimerEvent()
        {
            countdownUpdateDelegate?.Cancel();

            double timeToNextMessage = countdownTimeRemaining.TotalSeconds switch
            {
                > 60 => countdownTimeRemaining.TotalMilliseconds % 60_000,
                > 30 => countdownTimeRemaining.TotalMilliseconds % 30_000,
                > 10 => countdownTimeRemaining.TotalMilliseconds % 10_000,
                _ => countdownTimeRemaining.TotalMilliseconds % 5_000
            };

            Logger.Log($@"Time until next timer message: {timeToNextMessage}ms");

            countdownUpdateDelegate = Scheduler.AddDelayed(sendTimerMessage, timeToNextMessage);
        }

        private void sendTimerMessage()
        {
            int secondsRemaining = (int)Math.Round(countdownTimeRemaining.TotalSeconds);
            string message = secondsRemaining == 0 ? @"Countdown finished" : $@"Countdown ends in {secondsRemaining} seconds";
            OnChatMessageDue?.Invoke(message);

            if (secondsRemaining <= 0) return;

            Logger.Log($@"Sent timer message, {secondsRemaining} seconds remaining on timer. ");
            countdownUpdateDelegate = Scheduler.AddDelayed(processTimerEvent, 100);
        }

        public void Abort()
        {
            countdownUpdateDelegate?.Cancel();
            countdownUpdateDelegate = null;
        }
    }

    [Cached]
    public partial class MultiplayerMatchSubScreen : RoomSubScreen, IHandlePresentBeatmap
    {
        public override string Title { get; }

        public override string ShortTitle => "room";
        private LinkFlowContainer linkFlowContainer = null!;

        [Resolved]
        private MultiplayerClient client { get; set; }

        [Resolved(canBeNull: true)]
        private OsuGame game { get; set; }

        [Resolved]
        private BeatmapModelDownloader beatmapsDownloader { get; set; } = null!;

        // private BeatmapDownloadTracker beatmapDownloadTracker = null!;
        private readonly List<BeatmapDownloadTracker> beatmapDownloadTrackers = new List<BeatmapDownloadTracker>();

        private readonly List<MultiplayerPlaylistItem> playlistItemsToAdd = new List<MultiplayerPlaylistItem>();

        private readonly Queue<APIBeatmapSet> downloadQueue = new Queue<APIBeatmapSet>();

        private AddItemButton addItemButton;

        private Container osuCookieBackgroundContainer = null!;

        [CanBeNull]
        private OsuCookieBackground osuCookieBackground;

        public MultiplayerMatchSubScreen(Room room)
            : base(room)
        {
            Title = room.RoomID.Value == null ? "New room" : room.Name.Value;
            Activity.Value = new UserActivity.InLobby(room);
        }

        private OsuCookieBackground getOsuCookieBackground(IWorkingBeatmap workingBeatmap) => new OsuCookieBackground(workingBeatmap)
        {
            // Scale = new Vector2(4f),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fill,
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            Beatmap.BindValueChanged(vce =>
            {
                Scheduler.Add(() =>
                {
                    Logger.Log($@"updating cookie background to map {vce.NewValue}");

                    LoadComponentAsync(getOsuCookieBackground(vce.NewValue), loaded =>
                    {
                        osuCookieBackground?.FadeOut(300, Easing.OutCubic);
                        osuCookieBackground?.Expire();

                        loaded.Depth = osuCookieBackground?.Depth + 1 ?? 0;
                        osuCookieBackground = loaded;
                        osuCookieBackgroundContainer.Add(loaded);
                    });
                });
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BeatmapAvailability.BindValueChanged(updateBeatmapAvailability, true);
            UserMods.BindValueChanged(onUserModsChanged);

            client.LoadRequested += onLoadRequested;
            client.RoomUpdated += onRoomUpdated;

            if (!client.IsConnected.Value)
                handleRoomLost();

            Scheduler.Add(processDownloadQueue);
        }

        protected override bool IsConnected => base.IsConnected && client.IsConnected.Value;

        private void processDownloadQueue()
        {
            lock (downloadQueue)
            {
                if (downloadQueue.Count > 0)
                {
                    var beatmapSet = downloadQueue.Dequeue();
                    beatmapsDownloader.Download(beatmapSet);

                    Scheduler.AddDelayed(processDownloadQueue, 2500);
                    return;
                }
            }

            // no message has been posted
            Scheduler.AddDelayed(processDownloadQueue, 50);
        }

        protected override Drawable CreateMainContent() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Padding = new MarginPadding { Horizontal = 5, Vertical = 10 },
            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions =
                    [
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 10),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 10),
                        new Dimension()
                    ],
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            // Participants column
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Content = new[]
                                {
                                    new Drawable[] { new OverlinedHeader("Lobby ID") },
                                    [linkFlowContainer = new LinkFlowContainer { Height = 24, AutoSizeAxes = Axes.X }],
                                    [new ParticipantsListHeader()],
                                    [
                                        new ParticipantsList
                                        {
                                            RelativeSizeAxes = Axes.Both
                                        }
                                    ],
                                },
                                RowDimensions =
                                [
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension()
                                ]
                            },
                            // Spacer
                            null,
                            // osu! cookie
                            new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(420f),
                                Children = new Drawable[]
                                {
                                    osuCookieBackgroundContainer = new Container
                                    {
                                        AutoSizeAxes = Axes.None,
                                        RelativeSizeAxes = Axes.Both,
                                        Size = new Vector2(1f),
                                        Child = osuCookieBackground = getOsuCookieBackground(Beatmap.Value)
                                    },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = new OsuLogo
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Scale = new Vector2(0.5f),
                                            Margin = new MarginPadding(32.0f),
                                        },
                                    }
                                }
                            },
                            // Spacer
                            null,
                            // Main right column
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Content = new[]
                                {
                                    new Drawable[] { new OverlinedHeader("Chat") },
                                    new Drawable[] { chatDisplay = new MatchChatDisplay(Room) { RelativeSizeAxes = Axes.Both } },
                                    new Drawable[] { new OverlinedHeader("Beatmap") },
                                    new Drawable[]
                                    {
                                        addItemButton = new AddItemButton
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 40,
                                            Text = "Add item",
                                            Action = () => OpenSongSelection()
                                        },
                                    },
                                    new Drawable[]
                                    {
                                        new MultiplayerPlaylist
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            RequestEdit = OpenSongSelection
                                        }
                                    },
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension()
                                }
                            },
                        }
                    }
                }
            }
        };

        /// <summary>
        /// Opens the song selection screen to add or edit an item.
        /// </summary>
        /// <param name="itemToEdit">An optional playlist item to edit. If null, a new item will be added instead.</param>
        internal void OpenSongSelection(PlaylistItem itemToEdit = null)
        {
            if (!this.IsCurrentScreen())
                return;

            this.Push(new MultiplayerMatchSongSelect(Room, itemToEdit));
        }

        protected override Drawable CreateFooter() => new MultiplayerMatchFooter();

        protected override RoomSettingsOverlay CreateRoomSettingsOverlay(Room room) => new MultiplayerMatchSettingsOverlay(room);

        protected override void UpdateMods()
        {
            if (SelectedItem.Value == null || client.LocalUser == null || !this.IsCurrentScreen())
                return;

            // update local mods based on room's reported status for the local user (omitting the base call implementation).
            // this makes the server authoritative, and avoids the local user potentially setting mods that the server is not aware of (ie. if the match was started during the selection being changed).
            var rulesetInstance = Rulesets.GetRuleset(SelectedItem.Value.RulesetID)?.CreateInstance();
            Debug.Assert(rulesetInstance != null);
            Mods.Value = client.LocalUser.Mods.Select(m => m.ToMod(rulesetInstance)).Concat(SelectedItem.Value.RequiredMods.Select(m => m.ToMod(rulesetInstance))).ToList();
        }

        [Resolved(canBeNull: true)]
        private IDialogOverlay dialogOverlay { get; set; }

        [Resolved]
        private ChatTimerHandler chatTimerHandler { get; set; }

        private bool exitConfirmed;

        public override void OnResuming(ScreenTransitionEvent e)
        {
            // chatTimerHandler.SetMessageHandler(chatDisplay.EnqueueMessageBot);
            chatTimerHandler.OnChatMessageDue += chatDisplay.EnqueueBotMessage;
            base.OnResuming(e);
        }

        public override void OnSuspending(ScreenTransitionEvent _)
        {
            chatTimerHandler.OnChatMessageDue -= chatDisplay.EnqueueBotMessage;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // chatTimerHandler.SetMessageHandler(chatDisplay.EnqueueMessageBot);
            chatTimerHandler.OnChatMessageDue += chatDisplay.EnqueueBotMessage;
            base.OnEntering(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            chatTimerHandler.OnChatMessageDue -= chatDisplay.EnqueueBotMessage;

            // room has not been created yet or we're offline; exit immediately.
            if (client.Room == null || !IsConnected)
                return base.OnExiting(e);

            if (!exitConfirmed && dialogOverlay != null)
            {
                if (dialogOverlay.CurrentDialog is ConfirmDialog confirmDialog)
                    confirmDialog.PerformOkAction();
                else
                {
                    dialogOverlay.Push(new ConfirmDialog("Are you sure you want to leave this multiplayer match?", () =>
                    {
                        exitConfirmed = true;
                        if (this.IsCurrentScreen())
                            this.Exit();
                    }));
                }

                return true;
            }

            return base.OnExiting(e);
        }

        private ModSettingChangeTracker modSettingChangeTracker;
        private ScheduledDelegate debouncedModSettingsUpdate;
        private StandAloneChatDisplay chatDisplay;

        private void onUserModsChanged(ValueChangedEvent<IReadOnlyList<Mod>> mods)
        {
            modSettingChangeTracker?.Dispose();

            if (client.Room == null)
                return;

            client.ChangeUserMods(mods.NewValue).FireAndForget();

            modSettingChangeTracker = new ModSettingChangeTracker(mods.NewValue);
            modSettingChangeTracker.SettingChanged += onModSettingsChanged;
        }

        private void onModSettingsChanged(Mod mod)
        {
            // Debounce changes to mod settings so as to not thrash the network.
            debouncedModSettingsUpdate?.Cancel();
            debouncedModSettingsUpdate = Scheduler.AddDelayed(() =>
            {
                if (client.Room == null)
                    return;

                client.ChangeUserMods(UserMods.Value).FireAndForget();
            }, 500);
        }

        private void updateBeatmapAvailability(ValueChangedEvent<BeatmapAvailability> availability)
        {
            if (client.Room == null)
                return;

            client.ChangeBeatmapAvailability(availability.NewValue).FireAndForget();

            switch (availability.NewValue.State)
            {
                case DownloadState.LocallyAvailable:
                    if (client.LocalUser?.State == MultiplayerUserState.Spectating
                        && (client.Room?.State == MultiplayerRoomState.WaitingForLoad || client.Room?.State == MultiplayerRoomState.Playing))
                    {
                        onLoadRequested();
                    }

                    break;

                case DownloadState.Unknown:
                    // Don't do anything rash in an unknown state.
                    break;

                default:
                    // while this flow is handled server-side, this covers the edge case of the local user being in a ready state and then deleting the current beatmap.
                    if (client.LocalUser?.State == MultiplayerUserState.Ready)
                        client.ChangeState(MultiplayerUserState.Idle);
                    break;
            }
        }

        private void onRoomUpdated()
        {
            // may happen if the client is kicked or otherwise removed from the room.
            if (client.Room == null)
            {
                handleRoomLost();
                return;
            }

            updateCurrentItem();

            addItemButton.Alpha = localUserCanAddItem ? 1 : 0;

            Scheduler.AddOnce(UpdateMods);
            Scheduler.AddOnce(() =>
            {
                string roomLink = $"https://{MessageFormatter.WebsiteRootUrl}/multiplayer/rooms/{Room.RoomID}";
                linkFlowContainer.Clear();
                linkFlowContainer.AddLink(roomLink, roomLink);
            });

            Activity.Value = new UserActivity.InLobby(Room);
        }

        private bool localUserCanAddItem => client.IsHost || Room.QueueMode.Value != QueueMode.HostOnly;

        private void updateCurrentItem()
        {
            Debug.Assert(client.Room != null);
            SelectedItem.Value = Room.Playlist.SingleOrDefault(i => i.ID == client.Room.Settings.PlaylistItemId);
        }

        private void handleRoomLost() => Schedule(() =>
        {
            Logger.Log($"{this} exiting due to loss of room or connection");

            if (this.IsCurrentScreen())
                this.Exit();
            else
                ValidForResume = false;
        });

        private void onLoadRequested()
        {
            // In the case of spectating, IMultiplayerClient.LoadRequested can be fired while the game is still spectating a previous session.
            // For now, we want to game to switch to the new game so need to request exiting from the play screen.
            if (!ParentScreen.IsCurrentScreen())
            {
                ParentScreen.MakeCurrent();

                Schedule(onLoadRequested);
                return;
            }

            // The beatmap is queried asynchronously when the selected item changes.
            // This is an issue with MultiSpectatorScreen which is effectively in an always "ready" state and receives LoadRequested() callbacks
            // even when it is not truly ready (i.e. the beatmap hasn't been selected by the client yet). For the time being, a simple fix to this is to ignore the callback.
            // Note that spectator will be entered automatically when the client is capable of doing so via beatmap availability callbacks (see: updateBeatmapAvailability()).
            if (client.LocalUser?.State == MultiplayerUserState.Spectating && (SelectedItem.Value == null || Beatmap.IsDefault))
                return;

            if (BeatmapAvailability.Value.State != DownloadState.LocallyAvailable)
                return;

            StartPlay();
        }

        protected override Screen CreateGameplayScreen()
        {
            Debug.Assert(client.LocalUser != null);
            Debug.Assert(client.Room != null);

            // force using room Users order when collecting players
            // int[] userIds = client.CurrentMatchPlayingUserIds.ToArray();
            int[] userIds = client.Room.Users.Where(u => u.State >= MultiplayerUserState.WaitingForLoad && u.State <= MultiplayerUserState.FinishedPlay).Select(u => u.UserID).ToArray();
            MultiplayerRoomUser[] users = userIds.Select(id => client.Room.Users.First(u => u.UserID == id)).ToArray();

            switch (client.LocalUser.State)
            {
                case MultiplayerUserState.Spectating:
                    return new MultiSpectatorScreen(Room, users.Take(PlayerGrid.MAX_PLAYERS).ToArray());

                default:
                    return new MultiplayerPlayerLoader(() => new MultiplayerPlayer(Room, SelectedItem.Value, users));
            }
        }

        public void PresentBeatmap(WorkingBeatmap beatmap, RulesetInfo ruleset)
        {
            if (!this.IsCurrentScreen())
                return;

            if (!localUserCanAddItem)
                return;

            // If there's only one playlist item and we are the host, assume we want to change it. Else add a new one.
            PlaylistItem itemToEdit = client.IsHost && Room.Playlist.Count == 1 ? Room.Playlist.Single() : null;

            OpenSongSelection(itemToEdit);

            // Re-run PresentBeatmap now that we've pushed a song select that can handle it.
            game?.PresentBeatmap(beatmap.BeatmapSetInfo, b => b.ID == beatmap.BeatmapInfo.ID);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client != null)
            {
                client.RoomUpdated -= onRoomUpdated;
                client.LoadRequested -= onLoadRequested;
            }

            modSettingChangeTracker?.Dispose();
        }

        public partial class AddItemButton : PurpleRoundedButton
        {
        }
    }

    internal partial class OsuCookieBackground : CompositeDrawable
    {
        private readonly IWorkingBeatmap beatmap;

        public OsuCookieBackground(IWorkingBeatmap beatmap)
        {
            this.beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // We will create the white-to-black gradient by modulating transparency and having
                    // a black backdrop. This results in an sRGB-space gradient and not linear space,
                    // transitioning from white to black more perceptually uniformly.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                    },
                    // We use a container, such that we can set the colour gradient to go across the
                    // vertices of the masked container instead of the vertices of the (larger) sprite.
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Color4.White, Color4.White.Opacity(0.3f)),
                        Children = new[]
                        {
                            // Zoomed-in and cropped beatmap background
                            new BeatmapBackgroundSprite(beatmap)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                FillMode = FillMode.Fill,
                            },
                        },
                    },
                }
            };
        }
    }
}
