// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;
using osu.Game.Tournament.Screens.Gameplay.Components;
using osu.Game.Tournament.Screens.MapPool;
using osu.Game.Tournament.Screens.TeamWin;
using osu.Game.TournamentIpc;
using osuTK.Graphics;

namespace osu.Game.Tournament.Screens.Gameplay
{
    public partial class GameplayScreen : BeatmapInfoScreen
    {
        private readonly BindableBool warmup = new BindableBool();

        public readonly Bindable<LegacyTourneyState> LegacyState = new Bindable<LegacyTourneyState>();
        public readonly Bindable<TourneyState> LazerState = new Bindable<TourneyState>();
        private OsuButton warmupButton = null!;
        private SettingsLongNumberBox team1ScoreOverride = null!;
        private SettingsLongNumberBox team2ScoreOverride = null!;
        private OsuCheckbox matchCompleteOverride = null!;
        private LegacyMatchIPCInfo legacyIpc = null!;
        private MatchIPCInfo lazerIpc = null!;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        [Resolved]
        private TournamentMatchChatDisplay chat { get; set; } = null!;

        private Drawable chroma = null!;

        [BackgroundDependencyLoader]
        private void load(LegacyMatchIPCInfo legacyIpc, MatchIPCInfo lazerIpc)
        {
            this.legacyIpc = legacyIpc;
            this.lazerIpc = lazerIpc;

            AddRangeInternal(new Drawable[]
            {
                new TourneyVideo("gameplay")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                header = new MatchHeader
                {
                    ShowLogo = false,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Y = 110,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Children = new[]
                    {
                        chroma = new Container
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Height = 512,
                            Children = new Drawable[]
                            {
                                new ChromaArea
                                {
                                    Name = "Left chroma",
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0.5f,
                                },
                                new ChromaArea
                                {
                                    Name = "Right chroma",
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Width = 0.5f,
                                }
                            }
                        },
                    }
                },
                scoreDisplay = new TournamentMatchScoreDisplay
                {
                    Y = -147,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        warmupButton = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Toggle warmup",
                            Action = () => warmup.Toggle()
                        },
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Toggle chat",
                            Action = () => { LegacyState.Value = LegacyState.Value == LegacyTourneyState.Idle ? LegacyTourneyState.Playing : LegacyTourneyState.Idle; }
                        },
                        new SettingsSlider<int>
                        {
                            LabelText = "Chroma width",
                            Current = LadderInfo.ChromaKeyWidth,
                            KeyboardStep = 1,
                        },
                        new SettingsSlider<int>
                        {
                            LabelText = "Players per team",
                            Current = LadderInfo.PlayersPerTeam,
                            KeyboardStep = 1,
                        },
                        team1ScoreOverride = new SettingsLongNumberBox
                        {
                            LabelText = "Team red score override",
                            RelativeSizeAxes = Axes.None,
                            Width = 200,
                            ShowsDefaultIndicator = false,
                            Current = { Default = 0 }
                        },
                        team2ScoreOverride = new SettingsLongNumberBox
                        {
                            LabelText = "Team blue score override",
                            RelativeSizeAxes = Axes.None,
                            Width = 200,
                            ShowsDefaultIndicator = false,
                            Current = { Default = 0 }
                        },
                        matchCompleteOverride = new OsuCheckbox
                        {
                            LabelText = "match complete?",
                        },
                    }
                }
            });

            LadderInfo.ChromaKeyWidth.BindValueChanged(width => chroma.Width = width.NewValue, true);

            warmup.BindValueChanged(w =>
            {
                warmupButton.Alpha = !w.NewValue ? 0.5f : 1;
                header.ShowScores = !w.NewValue;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            LadderInfo.UseLazerIpc.BindValueChanged(vce =>
            {
                LegacyState.UnbindAll();
                LazerState.UnbindAll();

                if (vce.NewValue)
                {
                    LazerState.BindTo(lazerIpc.State);
                    LazerState.BindValueChanged(_ => updateStateLazer(), true);
                    return;
                }

                LegacyState.BindTo(legacyIpc.State);
                LegacyState.BindValueChanged(_ => updateStateLegacy(), true);
            }, true);
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            base.CurrentMatchChanged(match);

            if (match.NewValue == null)
                return;

            warmup.Value = match.NewValue.Team1Score.Value + match.NewValue.Team2Score.Value == 0;
            scheduledScreenChange?.Cancel();
            team1ScoreOverride.Current.UnbindBindings();
            team1ScoreOverride.Current.BindTo(match.NewValue.Team1Score);
            team2ScoreOverride.Current.UnbindBindings();
            team2ScoreOverride.Current.BindTo(match.NewValue.Team2Score);

            if (match.OldValue != null)
                matchCompleteOverride.Current.UnbindFrom(match.OldValue.Completed);
            matchCompleteOverride.Current.BindTo(match.NewValue.Completed);
        }

        private ScheduledDelegate? scheduledScreenChange;
        private ScheduledDelegate? scheduledContract;

        private TournamentMatchScoreDisplay scoreDisplay = null!;

        private LegacyTourneyState lastLegacyState;
        private TourneyState lastLazerState;
        private MatchHeader header = null!;

        private void contract()
        {
            if (!IsLoaded)
                return;

            scheduledContract?.Cancel();

            SongBar.Expanded = false;
            scoreDisplay.FadeOut(100);
            using (chat.BeginDelayedSequence(500))
                chat.Expand();
        }

        private void expand()
        {
            if (!IsLoaded)
                return;

            scheduledContract?.Cancel();

            chat.Contract();

            using (BeginDelayedSequence(300))
            {
                scoreDisplay.FadeIn(100);
                SongBar.Expanded = true;
            }
        }

        private void advanceAfterRanking(float delayBeforeProgression)
        {
            if (CurrentMatch.Value?.Completed.Value == true)
                scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(TeamWinScreen)); }, delayBeforeProgression);
            else if (CurrentMatch.Value?.Completed.Value == false)
                scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(MapPoolScreen)); }, delayBeforeProgression);
        }

        // kind of an ugly copy and paste from updateStateLegacy
        private void updateStateLazer()
        {
            Logger.Log($"lazer ipc state changed: {LazerState.Value}");

            try
            {
                scheduledScreenChange?.Cancel();

                if (LazerState.Value == TourneyState.Ranking)
                {
                    if (warmup.Value || CurrentMatch.Value == null) return;

                    if (LadderInfo.CumulativeScore.Value)
                    {
                        CurrentMatch.Value.Team1Score.Value += lazerIpc.Score1.Value;
                        CurrentMatch.Value.Team2Score.Value += lazerIpc.Score2.Value;

                        int mapId = lazerIpc.Beatmap.Value?.OnlineID ?? 0;

                        if (mapId > 0)
                        {
                            int pickBansCount = LadderInfo.CurrentMatch.Value?.PicksBans.Count ?? 0;
                            int poolSize = LadderInfo.CurrentMatch.Value?.Round.Value?.Beatmaps.Count ?? -1;

                            bool eligibleForWin = pickBansCount + 1 == poolSize;

                            Logger.Log($"{nameof(updateStateLazer)}: pickban#: {pickBansCount} | poolSize: {poolSize} | can win?: {eligibleForWin}");

                            if (eligibleForWin)
                            {
                                // we have a decider map
                                var deciderMap = CurrentMatch.Value.Round.Value?.Beatmaps
                                                             .FirstOrDefault(b => CurrentMatch.Value.PicksBans.All(p => p.BeatmapID != b.ID));

                                Logger.Log($"{nameof(updateStateLazer)}: on decider map?: {deciderMap != null}");

                                // mark match as completed, as we've played the decider map
                                if (deciderMap?.ID == mapId)
                                    CurrentMatch.Value.Completed.Value = true;
                            }
                        }
                    }
                    else
                    {
                        if (lazerIpc.Score1.Value > lazerIpc.Score2.Value)
                            CurrentMatch.Value.Team1Score.Value++;
                        else
                            CurrentMatch.Value.Team2Score.Value++;
                    }
                }

                switch (LazerState.Value)
                {
                    case TourneyState.Lobby:
                        contract();

                        if (LadderInfo.AutoProgressScreens.Value
                            && lastLazerState == TourneyState.Ranking
                            && !warmup.Value)
                        {
                            // if we've returned to idle and the last screen was ranking
                            // we should automatically proceed after a short delay
                            advanceAfterRanking(500);
                        }

                        break;

                    case TourneyState.Ranking:
                        scheduledContract = Scheduler.AddDelayed(contract, 10_000);
                        break;

                    default:
                        expand();
                        break;
                }
            }
            finally
            {
                lastLazerState = LazerState.Value;
            }
        }

        private void updateStateLegacy()
        {
            try
            {
                scheduledScreenChange?.Cancel();

                if (LegacyState.Value == LegacyTourneyState.Ranking)
                {
                    if (warmup.Value || CurrentMatch.Value == null) return;

                    if (legacyIpc.Score1.Value > legacyIpc.Score2.Value)
                        CurrentMatch.Value.Team1Score.Value++;
                    else
                        CurrentMatch.Value.Team2Score.Value++;
                }

                switch (LegacyState.Value)
                {
                    case LegacyTourneyState.Idle:
                        contract();

                        if (LadderInfo.AutoProgressScreens.Value
                            && lastLegacyState == LegacyTourneyState.Ranking
                            && !warmup.Value)
                        {
                            // if we've returned to idle and the last screen was ranking
                            // we should automatically proceed after a short delay
                            advanceAfterRanking(4000);
                        }

                        break;

                    case LegacyTourneyState.Ranking:
                        scheduledContract = Scheduler.AddDelayed(contract, 10000);
                        break;

                    default:
                        expand();
                        break;
                }
            }
            finally
            {
                lastLegacyState = LegacyState.Value;
            }
        }

        public override void Hide()
        {
            scheduledScreenChange?.Cancel();
            base.Hide();
        }

        public override void Show()
        {
            if (LadderInfo.UseLazerIpc.Value)
                updateStateLazer();
            else
                updateStateLegacy();

            base.Show();
        }

        private partial class ChromaArea : CompositeDrawable
        {
            [Resolved]
            private LadderInfo ladder { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                // chroma key area for stable gameplay
                Colour = new Color4(0, 255, 0, 255);

                ladder.PlayersPerTeam.BindValueChanged(performLayout, true);
            }

            private void performLayout(ValueChangedEvent<int> playerCount)
            {
                switch (playerCount.NewValue)
                {
                    case 3:
                        InternalChildren = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = 0.5f,
                                Height = 0.5f,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Height = 0.5f,
                            },
                        };
                        break;

                    default:
                        InternalChild = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                        };
                        break;
                }
            }
        }
    }
}
