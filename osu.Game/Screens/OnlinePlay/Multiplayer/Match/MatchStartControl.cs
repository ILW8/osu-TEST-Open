// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Match
{
    public partial class MatchStartControl : MultiplayerRoomComposite
    {
        [Resolved]
        private OngoingOperationTracker ongoingOperationTracker { get; set; }

        [CanBeNull]
        private IDisposable clickOperation;

        [Resolved(canBeNull: true)]
        private IDialogOverlay dialogOverlay { get; set; }

        private readonly MultiplayerReadyButton readyButton;
        private readonly MultiplayerCountdownButton countdownButton;
        private int countReady;
        private ScheduledDelegate readySampleDelegate;
        private IBindable<bool> operationInProgress;

        public MatchStartControl()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize)
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        readyButton = new MultiplayerReadyButton
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = Vector2.One,
                            Action = onReadyButtonClick,
                        },
                        countdownButton = new MultiplayerCountdownButton
                        {
                            RelativeSizeAxes = Axes.Y,
                            Size = new Vector2(40, 1),
                            Alpha = 0,
                            Action = startCountdown,
                            CancelAction = cancelCountdown
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            operationInProgress = ongoingOperationTracker.InProgress.GetBoundCopy();
            operationInProgress.BindValueChanged(_ => updateState());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            CurrentPlaylistItem.BindValueChanged(_ => updateState());
        }

        protected override void OnRoomUpdated()
        {
            base.OnRoomUpdated();
            updateState();
        }

        protected override void OnRoomLoadRequested()
        {
            base.OnRoomLoadRequested();
            endOperation();
        }

        private void onReadyButtonClick()
        {
            if (Room == null)
                return;

            Debug.Assert(clickOperation == null);
            clickOperation = ongoingOperationTracker.BeginOperation();

            if (Client.IsHost)
            {
                if (Room.State == MultiplayerRoomState.Open)
                {
                    if (isReady() && !Room.ActiveCountdowns.Any(c => c is MatchStartCountdown))
                        startMatch();
                    else
                        toggleReady();
                }
                else
                {
                    if (dialogOverlay == null)
                        abortMatch();
                    else
                        dialogOverlay.Push(new ConfirmAbortDialog(abortMatch, endOperation));
                }
            }
            else if (Room.State != MultiplayerRoomState.Closed)
                toggleReady();

            bool isReady() => Client.LocalUser?.State == MultiplayerUserState.Ready || Client.LocalUser?.State == MultiplayerUserState.Spectating;

            void toggleReady() => Client.ToggleReady().FireAndForget(
                onSuccess: endOperation,
                onError: _ => endOperation());

            void startMatch() => Client.StartMatch().FireAndForget(onSuccess: () =>
            {
                // gameplay is starting, the button will be unblocked on load requested.
            }, onError: _ =>
            {
                // gameplay was not started due to an exception; unblock button.
                endOperation();
            });

            void abortMatch() => Client.AbortMatch().FireAndForget(endOperation, _ => endOperation());
        }

        private void startCountdown(TimeSpan duration)
        {
            Debug.Assert(clickOperation == null);
            clickOperation = ongoingOperationTracker.BeginOperation();

            Client.SendMatchRequest(new StartMatchCountdownRequest { Duration = duration }).ContinueWith(_ => endOperation());
        }

        private void cancelCountdown()
        {
            if (Client.Room == null)
                return;

            Debug.Assert(clickOperation == null);
            clickOperation = ongoingOperationTracker.BeginOperation();

            MultiplayerCountdown countdown = Client.Room.ActiveCountdowns.Single(c => c is MatchStartCountdown);
            Client.SendMatchRequest(new StopCountdownRequest(countdown.ID)).ContinueWith(_ => endOperation());
        }

        private void endOperation()
        {
            clickOperation?.Dispose();
            clickOperation = null;
        }

        private void updateState()
        {
            if (Room == null)
            {
                readyButton.Enabled.Value = false;
                countdownButton.Enabled.Value = false;
                return;
            }

            var localUser = Client.LocalUser;

            int newCountReady = Room.Users.Count(u => u.State == MultiplayerUserState.Ready);

            if (!Client.IsHost || Room.Settings.AutoStartEnabled)
                countdownButton.Hide();
            else
            {
                switch (localUser?.State)
                {
                    default:
                        countdownButton.Hide();
                        break;

                    case MultiplayerUserState.Idle:
                    case MultiplayerUserState.Spectating:
                    case MultiplayerUserState.Ready:
                        countdownButton.Show();
                        break;
                }
            }

            readyButton.Enabled.Value = countdownButton.Enabled.Value =
                Room.State != MultiplayerRoomState.Closed
                && CurrentPlaylistItem.Value?.ID == Room.Settings.PlaylistItemId
                && !Room.Playlist.Single(i => i.ID == Room.Settings.PlaylistItemId).Expired
                && !operationInProgress.Value;

            // When the local user is the host and spectating the match, the ready button should be enabled only if any users are ready.
            if (localUser?.State == MultiplayerUserState.Spectating)
                readyButton.Enabled.Value &= Client.IsHost && newCountReady > 0 && !Room.ActiveCountdowns.Any(c => c is MatchStartCountdown);

            // When the local user is not the host, the button should only be enabled when no match is in progress.
            if (!Client.IsHost)
                readyButton.Enabled.Value &= Room.State == MultiplayerRoomState.Open;

            // At all times, the countdown button should only be enabled when no match is in progress.
            countdownButton.Enabled.Value &= Room.State == MultiplayerRoomState.Open;

            // force enable ready button to allow host to abort regardless of state
            if (Client.IsHost && Room.State is MultiplayerRoomState.WaitingForLoad or MultiplayerRoomState.Playing)
                readyButton.Enabled.Value = true;

            if (newCountReady == countReady)
                return;

            readySampleDelegate?.Cancel();
            readySampleDelegate = Schedule(() => countReady = newCountReady);
        }

        public partial class ConfirmAbortDialog : DangerousActionDialog
        {
            public ConfirmAbortDialog(Action abortMatch, Action cancel)
            {
                HeaderText = "Are you sure you want to abort the match?";

                DangerousAction = abortMatch;
                CancelAction = cancel;
            }
        }
    }
}
