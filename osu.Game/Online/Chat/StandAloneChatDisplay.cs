// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Broadcasts;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Chat;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay;
using osu.Game.Utils;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Online.Chat
{
    /// <summary>
    /// Display a chat channel in an insolated region.
    /// </summary>
    public partial class StandAloneChatDisplay : CompositeDrawable
    {
        [Cached]
        public readonly Bindable<Channel> Channel = new Bindable<Channel>();

        [Resolved]
        private IGameStateBroadcastServer broadcastServer { get; set; } = null!;

        private readonly MultiplayerChatBroadcaster chatBroadcaster;

        [Resolved]
        protected MultiplayerClient Client { get; private set; }

        [Resolved]
        protected RulesetStore RulesetStore { get; private set; }

        private BeatmapModelDownloader beatmapsDownloader = null!;

        private BeatmapLookupCache beatmapLookupCache = null!;

        private BeatmapDownloadTracker beatmapDownloadTracker = null!;

        private IDisposable selectionOperation;

        private readonly Queue<Tuple<string, Channel>> messageQueue = new Queue<Tuple<string, Channel>>();

        private readonly Queue<Tuple<string, Channel>> botMessageQueue = new Queue<Tuple<string, Channel>>();

        [Resolved]
        private OngoingOperationTracker operationTracker { get; set; } = null!;

        [Resolved(typeof(Room), nameof(Room.Playlist), canBeNull: true)]
        private BindableList<PlaylistItem> roomPlaylist { get; set; }

        protected readonly ChatTextBox TextBox;

        private ChannelManager channelManager;

        private StandAloneDrawableChannel drawableChannel;

        private readonly bool postingTextBox;

        protected readonly Box Background;

        private const float text_box_height = 30;

        [CanBeNull]
        private ScheduledDelegate countdownUpdateDelegate;

        protected readonly MultiplayerCountdown Countdown = new MatchStartCountdown { TimeRemaining = TimeSpan.Zero };

        private double countdownChangeTime;

        /// <summary>
        /// Construct a new instance.
        /// </summary>
        /// <param name="postingTextBox">Whether a textbox for posting new messages should be displayed.</param>
        public StandAloneChatDisplay(bool postingTextBox = false)
        {
            const float corner_radius = 10;

            this.postingTextBox = postingTextBox;
            CornerRadius = corner_radius;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                Background = new Box
                {
                    Colour = Color4.Black,
                    Alpha = 0.8f,
                    RelativeSizeAxes = Axes.Both
                },
            };

            if (postingTextBox)
            {
                AddInternal(TextBox = new ChatTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Height = text_box_height,
                    PlaceholderText = ChatStrings.InputPlaceholder,
                    CornerRadius = corner_radius,
                    ReleaseFocusOnCommit = false,
                    HoldFocus = true,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                });

                TextBox.OnCommit += postMessage;
            }

            chatBroadcaster = new MultiplayerChatBroadcaster();
            Channel.BindValueChanged(channelChanged);
        }

        [BackgroundDependencyLoader(true)]
        private void load(ChannelManager manager, BeatmapModelDownloader beatmaps, BeatmapLookupCache beatmapsCache)
        {
            channelManager ??= manager;
            beatmapsDownloader = beatmaps;
            beatmapLookupCache = beatmapsCache;

            AddInternal(beatmapDownloadTracker = new BeatmapDownloadTracker(new BeatmapSetInfo()));

            Client.RoomUpdated += () =>
            {
                if (Client.Room?.State == MultiplayerRoomState.Open) return; // only allow timer if room is idle

                countdownUpdateDelegate?.Cancel();
                countdownUpdateDelegate = null;
            };
            Scheduler.Add(() => broadcastServer.Add(chatBroadcaster));
            Scheduler.Add(processMessageQueue);
        }

        private void processMessageQueue()
        {
            lock (messageQueue)
            {
                if (messageQueue.Count > 0)
                {
                    (string text, var target) = messageQueue.Dequeue();
                    channelManager?.PostMessage(text, target: target);
                    Scheduler.AddDelayed(processMessageQueue, 1000);
                    return;
                }
            }

            lock (botMessageQueue)
            {
                if (botMessageQueue.Count > 0)
                {
                    (string text, Channel target) = botMessageQueue.Dequeue();
                    channelManager?.PostMessage($@"[TESTOpenBot]: {text}", target: target);
                    Scheduler.AddDelayed(processMessageQueue, 1000);
                    return;
                }
            }

            // no message has been posted
            Scheduler.AddDelayed(processMessageQueue, 50);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (IsDisposed)
                return;

            Scheduler.Add(() => broadcastServer.Remove(chatBroadcaster));
            base.Dispose(isDisposing);
        }

        protected virtual StandAloneDrawableChannel CreateDrawableChannel(Channel channel) =>
            new StandAloneDrawableChannel(channel);

        private void postMessage(TextBox sender, bool newText)
        {
            string text = TextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return;

            if (text[0] == '/')
                channelManager?.PostCommand(text.Substring(1), Channel.Value);
            else
            {
                // channelManager?.PostMessage(text, target: Channel.Value);
                messageQueue.Enqueue(new Tuple<string, Channel>(text, Channel.Value));

                string[] parts = text.Split();

                for (;;)
                {
                    // 3 part commands
                    if (!(parts.Length == 3 && parts[0] == @"!mp"))
                        break;

                    // commands with numerical parameter
                    if (int.TryParse(parts[2], out int onlineID))
                    {
                        switch (parts[1])
                        {
                            case @"map":
                                beatmapLookupCache.GetBeatmapAsync(onlineID).ContinueWith(task => Schedule(() =>
                                {
                                    APIBeatmap beatmapInfo = task.GetResultSafely();

                                    if (beatmapInfo?.BeatmapSet == null) return;

                                    RemoveInternal(beatmapDownloadTracker, true);
                                    AddInternal(beatmapDownloadTracker = new BeatmapDownloadTracker(beatmapInfo.BeatmapSet));

                                    beatmapDownloadTracker.State.BindValueChanged(changeEvent =>
                                    {
                                        switch (changeEvent.NewValue)
                                        {
                                            case DownloadState.LocallyAvailable:
                                                addPlaylistItem(beatmapInfo);
                                                return;

                                            case DownloadState.NotDownloaded:
                                                beatmapsDownloader.Download(beatmapInfo.BeatmapSet);
                                                break;
                                        }
                                    });
                                }));
                                break;

                            case @"timer":
                                Countdown.TimeRemaining = TimeSpan.FromSeconds(onlineID);
                                countdownChangeTime = Time.Current;
                                sendTimerMessage();

                                break;
                        }
                    }
                    else
                    {
                        switch (parts[1])
                        {
                            case @"timer":
                                if (parts[2] == @"abort")
                                {
                                    if (countdownUpdateDelegate == null)
                                        break;

                                    countdownUpdateDelegate.Cancel();
                                    countdownUpdateDelegate = null;

                                    // Scheduler.AddDelayed(() => channelManager?.PostMessage(@"Countdown aborted", target: Channel.Value), 1000);
                                    botMessageQueue.Enqueue(new Tuple<string, Channel>(@"Countdown aborted", Channel.Value));
                                }

                                break;

                            case @"mods":
                                // abort if room playlist is somehow null:
                                if (roomPlaylist == null)
                                    break;

                                var itemToEdit = roomPlaylist.First();
                                string[] mods = parts[2].Split("+");
                                List<Mod> modInstances = new List<Mod>();

                                foreach (string mod in mods)
                                {
                                    if (mod.Length < 2)
                                    {
                                        Logger.Log($@"[!mp mods] Unknown mod '{mod}', ignoring", LoggingTarget.Runtime, LogLevel.Important);
                                        continue;
                                    }

                                    string modAcronym = mod[..2];
                                    var rulesetInstance = RulesetStore.GetRuleset(itemToEdit.RulesetID)?.CreateInstance();
                                    var modInstance = rulesetInstance?.CreateModFromAcronym(modAcronym);
                                    if (modInstance == null)
                                        break;

                                    if (mod.Length == 2)
                                    {
                                        modInstances.Add(modInstance);
                                        continue;
                                    }

                                    // mod has parameters
                                    JsonNode modParamsNode;

                                    try
                                    {
                                        modParamsNode = JsonNode.Parse(mod[2..]);
                                    }
                                    catch (JsonReaderException)
                                    {
                                        Logger.Log($@"[!mp mods] Couldn't parse mod parameter(s) '{mod[2..]}', ignoring", LoggingTarget.Runtime, LogLevel.Important);
                                        continue;
                                    }

                                    if (modParamsNode is JsonArray modParams)
                                    {
                                        var sourceProperties = modInstance.GetOrderedSettingsSourceProperties().ToArray();

                                        if (modParams.Count > sourceProperties.Length)
                                        {
                                            Logger.Log(
                                                $@"[!mp mods] Expected at most {sourceProperties.Length} parameter(s) for mod {modAcronym}, got {modParams.Count} parameter(s). Ignoring extra parameter(s)",
                                                LoggingTarget.Runtime, LogLevel.Important);
                                            break;
                                        }

                                        for (int i = 0; i < Math.Min(sourceProperties.Length, modParams.Count); i++)
                                        {
                                            var node = modParams[i];

                                            if (node.GetValueKind() is not (JsonValueKind.Number or JsonValueKind.False or JsonValueKind.True))
                                                continue;

                                            object paramValue = sourceProperties[i].Item2.GetValue(modInstance);
                                            var paramAttr = sourceProperties[i].Item1;

                                            if (node.AsValue().TryGetValue(out int intData))
                                            {
                                                switch (paramValue)
                                                {
                                                    case BindableNumber<int> bParamValue:
                                                        bParamValue.Value = intData;
                                                        continue;

                                                    case Bindable<int?> bParamValue:
                                                        bParamValue.Value = intData;
                                                        continue;

                                                    case BindableNumber<double> bParamValueDouble:
                                                        bParamValueDouble.Value = intData;
                                                        continue;

                                                    case IBindable bindable:
                                                        var enumType = bindable.GetType().GetGenericArguments()[0];

                                                        if (enumType.IsEnum)
                                                        {
                                                            if (Enum.GetValues(enumType).Cast<int>().Contains(intData))
                                                            {
                                                                typeof(Bindable<>).MakeGenericType(enumType).GetProperty(nameof(Bindable<object>.Value))?.SetValue(bindable, intData);
                                                                continue;
                                                            }

                                                            Logger.Log($@"[!mp mods] {modAcronym}'s {paramAttr.Label} not assignable to value {intData} (out of range)", LoggingTarget.Runtime,
                                                                LogLevel.Important);
                                                            continue;
                                                        }

                                                        Logger.Log(
                                                            $@"[!mp mods] {modAcronym}'s {paramAttr.Label} (of type {bindable.GetType().GetRealTypeName()}) not assignable to value {intData} ({intData.GetType().Name})",
                                                            LoggingTarget.Runtime, LogLevel.Important);
                                                        continue;

                                                    default:
                                                        Logger.Log(
                                                            $@"[!mp mods] Tried setting {modAcronym}'s {paramAttr.Label} parameter (of type {paramValue?.GetType().Name}) using type {intData.GetType().Name}",
                                                            LoggingTarget.Runtime, LogLevel.Important);
                                                        continue;
                                                }
                                            }

                                            if (node.AsValue().TryGetValue(out double doubleData))
                                            {
                                                if (paramValue is BindableNumber<double> bParamValue)
                                                {
                                                    bParamValue.Value = doubleData;
                                                    continue;
                                                }

                                                Logger.Log(
                                                    $@"[!mp mods] Tried setting {modAcronym}'s {paramAttr.Label} parameter (of type {paramValue?.GetType().Name}) using type {doubleData.GetType().Name}",
                                                    LoggingTarget.Runtime, LogLevel.Important);
                                                continue;
                                            }

                                            if (node.AsValue().TryGetValue(out bool boolData))
                                            {
                                                if (paramValue is BindableBool bParamValue)
                                                {
                                                    bParamValue.Value = boolData;
                                                    continue;
                                                }

                                                Logger.Log(
                                                    $@"[!mp mods] Tried setting {modAcronym}'s {paramAttr.Label} parameter (of type {paramValue?.GetType().Name}) using type {boolData.GetType().Name}",
                                                    LoggingTarget.Runtime, LogLevel.Important);
                                            }
                                        }

                                        modInstances.Add(modInstance);
                                    }
                                    else
                                    {
                                        Logger.Log(@$"[!mp mods] Couldn't parse mod parameter(s) {modAcronym}, ignoring", LoggingTarget.Runtime, LogLevel.Important);
                                    }
                                }

                                if (!ModUtils.CheckCompatibleSet(modInstances))
                                {
                                    Logger.Log($@"[!mp mods] Mods {string.Join(", ", modInstances.Select(mod => mod.Acronym))} are not compatible together", LoggingTarget.Runtime, LogLevel.Important);
                                    break;
                                }

                                // get playlist item to edit:
                                beatmapLookupCache.GetBeatmapAsync(itemToEdit.Beatmap.OnlineID).ContinueWith(task => Schedule(() =>
                                {
                                    APIBeatmap beatmapInfo = task.GetResultSafely();

                                    var multiplayerItem = new MultiplayerPlaylistItem
                                    {
                                        ID = itemToEdit.ID,
                                        BeatmapID = beatmapInfo.OnlineID,
                                        BeatmapChecksum = beatmapInfo.MD5Hash,
                                        RulesetID = itemToEdit.RulesetID,
                                        RequiredMods = modInstances.Select(mod => new APIMod(mod)).ToArray(),
                                        AllowedMods = Array.Empty<APIMod>()
                                    };

                                    selectionOperation = operationTracker.BeginOperation();
                                    Task editPlaylistTask = Client.EditPlaylistItem(multiplayerItem);

                                    editPlaylistTask.FireAndForget(onSuccess: () =>
                                    {
                                        selectionOperation?.Dispose();
                                    }, onError: _ =>
                                    {
                                        selectionOperation?.Dispose();
                                    });
                                }));
                                break;
                        }
                    }

                    break;
                }
            }

            TextBox.Text = string.Empty;
        }

        private TimeSpan countdownTimeRemaining
        {
            get
            {
                double timeElapsed = Time.Current - countdownChangeTime;
                TimeSpan remaining;

                if (timeElapsed > Countdown.TimeRemaining.TotalMilliseconds)
                    remaining = TimeSpan.Zero;
                else
                    remaining = Countdown.TimeRemaining - TimeSpan.FromMilliseconds(timeElapsed);

                return remaining;
            }
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

            countdownUpdateDelegate = Scheduler.AddDelayed(sendTimerMessage, timeToNextMessage);
        }

        private void sendTimerMessage()
        {
            int secondsRemaining = (int)Math.Round(countdownTimeRemaining.TotalSeconds);

            // channelManager?.PostMessage(secondsRemaining == 0 ? @"Countdown finished" : $@"Countdown ends in {secondsRemaining} seconds", target: Channel.Value);
            botMessageQueue.Enqueue(new Tuple<string, Channel>(secondsRemaining == 0 ? @"Countdown finished" : $@"Countdown ends in {secondsRemaining} seconds", Channel.Value));

            if (secondsRemaining > 0)
                countdownUpdateDelegate = Scheduler.AddDelayed(processTimerEvent, 800); // force delay invocation of next timer event
        }

        private void addPlaylistItem(APIBeatmap beatmapInfo)
        {
            // ensure user is host
            if (!Client.IsHost)
                return;

            selectionOperation = operationTracker.BeginOperation();

            var item = new PlaylistItem(beatmapInfo)
            {
                RulesetID = beatmapInfo.Ruleset.OnlineID,
                RequiredMods = Array.Empty<APIMod>(),
                AllowedMods = Array.Empty<APIMod>()
            };

            // PlaylistItem item
            var multiplayerItem = new MultiplayerPlaylistItem
            {
                ID = 0,
                BeatmapID = item.Beatmap.OnlineID,
                BeatmapChecksum = item.Beatmap.MD5Hash,
                RulesetID = item.RulesetID,
                RequiredMods = item.RequiredMods,
                AllowedMods = item.AllowedMods
            };

            var itemsToRemove = roomPlaylist?.ToArray() ?? Array.Empty<PlaylistItem>();
            Task addPlaylistItemTask = Client.AddPlaylistItem(multiplayerItem);

            addPlaylistItemTask.FireAndForget(onSuccess: () =>
            {
                selectionOperation?.Dispose();

                foreach (var playlistItem in itemsToRemove)
                    Client.RemovePlaylistItem(playlistItem.ID).FireAndForget();
            }, onError: _ =>
            {
                selectionOperation?.Dispose();
            });
        }

        protected virtual ChatLine CreateMessage(Message message)
        {
            string[] parts = message.Content.Split();

            if (parts.Length > 0 && parts[0] == @"!roll" && Client.IsHost)
            {
                long limit = 100;

                if (parts.Length > 1)
                {
                    try
                    {
                        limit = long.Parse(parts[1]);
                    }
                    catch (OverflowException)
                    {
                        limit = long.MaxValue;
                    }
                    catch (Exception)
                    {
                        limit = 100;
                    }
                }

                var rnd = new Random();
                long randomNumber = rnd.NextInt64(1, limit);
                // channelManager?.PostMessage($@"{message.Sender} rolls ${randomNumber}", target: Channel.Value);
                botMessageQueue.Enqueue(new Tuple<string, Channel>($@"{message.Sender} rolls {randomNumber}", Channel.Value));
            }

            chatBroadcaster.AddNewMessage(message);
            return new StandAloneMessage(message);
        }

        private void channelChanged(ValueChangedEvent<Channel> e)
        {
            drawableChannel?.Expire();

            if (e.OldValue != null)
                TextBox?.Current.UnbindFrom(e.OldValue.TextBoxMessage);

            if (e.NewValue == null) return;

            TextBox?.Current.BindTo(e.NewValue.TextBoxMessage);

            drawableChannel = CreateDrawableChannel(e.NewValue);
            drawableChannel.CreateChatLineAction = CreateMessage;
            drawableChannel.Padding = new MarginPadding { Bottom = postingTextBox ? text_box_height : 0 };

            chatBroadcaster.Message.ChatMessages.Clear();
            AddInternal(drawableChannel);
        }

        public partial class ChatTextBox : HistoryTextBox
        {
            protected override bool OnKeyDown(KeyDownEvent e)
            {
                // Chat text boxes are generally used in places where they retain focus, but shouldn't block interaction with other
                // elements on the same screen.
                if (!HoldFocus)
                {
                    switch (e.Key)
                    {
                        case Key.Up:
                        case Key.Down:
                            return false;
                    }
                }

                return base.OnKeyDown(e);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                BackgroundUnfocused = new Color4(10, 10, 10, 10);
                BackgroundFocused = new Color4(10, 10, 10, 255);
            }

            protected override void OnFocusLost(FocusLostEvent e)
            {
                base.OnFocusLost(e);
                FocusLost?.Invoke();
            }

            public Action FocusLost;
        }

        public partial class StandAloneDrawableChannel : DrawableChannel
        {
            public Func<Message, ChatLine> CreateChatLineAction;

            public StandAloneDrawableChannel(Channel channel)
                : base(channel)
            {
            }

            protected override ChatLine CreateChatLine(Message m) => CreateChatLineAction(m);

            protected override DaySeparator CreateDaySeparator(DateTimeOffset time) => new StandAloneDaySeparator(time);
        }

        protected partial class StandAloneDaySeparator : DaySeparator
        {
            protected override float TextSize => 14;
            protected override float LineHeight => 1;
            protected override float Spacing => 5;
            protected override float DateAlign => 125;

            public StandAloneDaySeparator(DateTimeOffset date)
                : base(date)
            {
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Height = 25;
                Colour = colours.Yellow;
            }
        }

        protected partial class StandAloneMessage : ChatLine
        {
            protected override float FontSize => 15;
            protected override float Spacing => 5;
            protected override float UsernameWidth => 75;

            public StandAloneMessage(Message message)
                : base(message)
            {
            }
        }
    }
}

public static class YepExtension
{
    public static string GetRealTypeName(this Type t)
    {
        if (!t.IsGenericType)
            return t.Name;

        StringBuilder sb = new StringBuilder();
        sb.Append(t.Name.Substring(0, t.Name.IndexOf('`')));
        sb.Append('<');
        bool appendComma = false;

        foreach (Type arg in t.GetGenericArguments())
        {
            if (appendComma) sb.Append(',');
            sb.Append(GetRealTypeName(arg));
            appendComma = true;
        }

        sb.Append('>');
        return sb.ToString();
    }
}
