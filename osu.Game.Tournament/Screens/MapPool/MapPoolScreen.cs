// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;
using osu.Game.Tournament.Screens.Gameplay;
using osu.Game.Tournament.Screens.Gameplay.Components;
using osu.Game.TournamentIpc;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tournament.Screens.MapPool
{
    public partial class MapPoolScreen : TournamentMatchScreen
    {
        private FillFlowContainer<FillFlowContainer<TournamentBeatmapPanel>> availableMapsFlows = null!;
        private FillFlowContainer<TournamentBeatmapPanel> pickedMapsFlow = null!;

        [Resolved]
        private TournamentSceneManager? sceneManager { get; set; }

        private Bindable<TournamentBeatmap?> beatmap { get; } = new Bindable<TournamentBeatmap?>();
        private Bindable<TourneyState> lazerState { get; } = new Bindable<TourneyState>();

        private TeamColour pickColour;
        private ChoiceType pickType;

        private OsuButton buttonRedBan = null!;
        private OsuButton buttonBlueBan = null!;
        private OsuButton buttonRedPick = null!;
        private OsuButton buttonBluePick = null!;

        private SpriteIcon currentMapIndicator = null!;

        private ScheduledDelegate? scheduledScreenChange;

        [BackgroundDependencyLoader]
        private void load(LegacyMatchIPCInfo legacyIpc, MatchIPCInfo lazerIpc)
        {
            InternalChildren = new Drawable[]
            {
                new TourneyVideo("mappool")
                {
                    Loop = true,
                    RelativeSizeAxes = Axes.Both,
                },
                new MatchHeader
                {
                    ShowScores = true,
                },
                new GridContainer
                {
                    Y = 170,
                    X = 0f,
                    Anchor = Anchor.TopLeft,
                    RelativePositionAxes = Axes.X,
                    Width = 0.65f,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize)
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new TournamentSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Padding = new MarginPadding { Vertical = 4 },
                                Font = OsuFont.Torus.With(weight: FontWeight.Bold, size: 18),
                                Text = "Pool"
                            },
                        },
                        new Drawable[]
                        {
                            availableMapsFlows = new FillFlowContainer<FillFlowContainer<TournamentBeatmapPanel>>
                            {
                                Anchor = Anchor.TopLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,

                                Spacing = new Vector2(10, 10),
                                Direction = FillDirection.Vertical,
                            },
                        }
                    }
                },

                new GridContainer
                {
                    Y = 170,
                    X = 0.65f,
                    Anchor = Anchor.TopLeft,
                    RelativePositionAxes = Axes.X,
                    Width = 0.35f,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize)
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new TournamentSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Padding = new MarginPadding { Vertical = 4 },
                                Font = OsuFont.Torus.With(weight: FontWeight.Bold, size: 18),
                                Text = "Drafted order"
                            },
                        },
                        new Drawable[]
                        {
                            pickedMapsFlow = new FillFlowContainer<TournamentBeatmapPanel>
                            {
                                Anchor = Anchor.TopLeft,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,

                                Spacing = new Vector2(10, 5),
                                Direction = FillDirection.Full,
                            },
                        }
                    }
                },

                currentMapIndicator = new SpriteIcon
                {
                    Icon = FontAwesome.Solid.AngleDoubleRight,
                    Width = 16f,
                    Height = 16f,
                    Alpha = 0
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TournamentSpriteText
                        {
                            Text = "Current Mode"
                        },
                        buttonRedBan = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Red Ban",
                            Action = () => setMode(TeamColour.Red, ChoiceType.Ban)
                        },
                        buttonBlueBan = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Blue Ban",
                            Action = () => setMode(TeamColour.Blue, ChoiceType.Ban)
                        },
                        buttonRedPick = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Red Pick",
                            Action = () => setMode(TeamColour.Red, ChoiceType.Pick)
                        },
                        buttonBluePick = new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Blue Pick",
                            Action = () => setMode(TeamColour.Blue, ChoiceType.Pick)
                        },
                        new ControlPanel.Spacer(),
                        new TourneyButton
                        {
                            RelativeSizeAxes = Axes.X,
                            Text = "Reset",
                            Action = reset
                        },
                        new ControlPanel.Spacer(),
                        new OsuCheckbox
                        {
                            LabelText = "Split display by mods",
                            Current = LadderInfo.SplitMapPoolByMods,
                        },
                    },
                }
            };

            LadderInfo.UseLazerIpc.BindValueChanged(vce =>
            {
                beatmap.UnbindAll();
                beatmap.BindTo(vce.NewValue ? lazerIpc.Beatmap : legacyIpc.Beatmap);

                if (LadderInfo.CumulativeScore.Value)
                {
                    lazerState.UnbindAll();
                    lazerState.BindTo(lazerIpc.State);
                }
            }, true);

            beatmap.BindValueChanged(beatmapChanged, true);
            lazerState.BindValueChanged(vce =>
            {
                if (vce.NewValue != TourneyState.Playing || !LadderInfo.AutoProgressScreens.Value) return;

                scheduledScreenChange?.Cancel();
                scheduledScreenChange = Scheduler.AddDelayed(() =>
                {
                    // scheduled screen change could be preempted by manual scene switch, then running when transitioning back from gameplay
                    if (lazerState.Value != TourneyState.Playing) return;

                    sceneManager?.SetScreen(typeof(GameplayScreen));
                }, 150);
            });
        }

        private Bindable<bool>? splitMapPoolByMods;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            splitMapPoolByMods = LadderInfo.SplitMapPoolByMods.GetBoundCopy();
            splitMapPoolByMods.BindValueChanged(_ => updatePoolDisplay());

            setMode(TeamColour.Red, ChoiceType.Ban);
        }

        private void beatmapChanged(ValueChangedEvent<TournamentBeatmap?> beatmap)
        {
            if (CurrentMatch.Value?.Round.Value == null)
                return;

            if (pickedMapsFlow.Count == 0)
                return;

            if (beatmap.NewValue?.OnlineID == null)
                return;

            var currentPanel = pickedMapsFlow.FirstOrDefault(p => p.Beatmap?.OnlineID == beatmap.NewValue.OnlineID);
            if (currentPanel == null) return;

            var parentSpacePosition = currentPanel.ToSpaceOfOtherDrawable(currentPanel.OriginPosition, Parent!);
            var offsetPosition = parentSpacePosition +
                                 new Vector2(-currentPanel.DrawWidth / 2 - currentMapIndicator.DrawWidth / 2 - 16,
                                     currentPanel.DrawHeight / 2 - currentMapIndicator.DrawHeight / 2);

            if (currentMapIndicator.Alpha == 0)
            {
                currentMapIndicator.MoveTo(offsetPosition);
                currentMapIndicator.FadeInFromZero(500);
            }
            else
            {
                currentMapIndicator.MoveTo(offsetPosition, 700, Easing.InOutExpo);
            }
        }

        private void setMode(TeamColour colour, ChoiceType choiceType)
        {
            pickColour = colour;
            pickType = choiceType;

            buttonRedBan.Colour = setColour(pickColour == TeamColour.Red && pickType == ChoiceType.Ban);
            buttonBlueBan.Colour = setColour(pickColour == TeamColour.Blue && pickType == ChoiceType.Ban);
            buttonRedPick.Colour = setColour(pickColour == TeamColour.Red && pickType == ChoiceType.Pick);
            buttonBluePick.Colour = setColour(pickColour == TeamColour.Blue && pickType == ChoiceType.Pick);

            static Color4 setColour(bool active) => active ? Color4.White : Color4.Gray;
        }

        // LGA bans (week 1)
        // The first player (A) will ban one beatmap, followed by the second player (B) also banning a beatmap: AB
        // Players will pick two beatmaps respecting the following order: BAAB
        // Both players will ban two maps, as such: ABBA
        // The last beatmap remaining in the pool will be used as the 5th pick for the match.
        //
        // LGA bans (week 2)
        // The first player (A) will ban one beatmap, followed by the second player (B) also banning a beatmap: AB (1)
        // Players will pick two beatmaps respecting the following order: BAAB (2)
        // Both players will ban two beatmaps, as such: ABBA (3)
        // Both players will pick one beatmap: AB (4)
        // Both players will ban one beatmap: BA (5)
        // The last beatmap remaining in the pool will be used as the 7th pick for the match. (6)
        // Exceptionally, for the Losers' Bracket Finals and Grand Finals, steps 5 and 6 will not be applied, and the last pick will be an osu! original beatmap, to be released at match time.
        //
        // Ban  AB
        // pick BAAB
        // ban  ABBA
        // pick AB
        // ban  BA
        //
        // boils down to ABBAAB(BA) then ABBAABBA
        private void setNextMode()
        {
            bool[] shouldBanAtCount =
            {
                true, true,
                false, false, false, false,
                true, true, true, true,
                false, false,
                true, true
            };

            TeamColour[] teamColourOrder =
            {
                TeamColour.Red, TeamColour.Blue,
                TeamColour.Blue, TeamColour.Red, TeamColour.Red, TeamColour.Blue,
                TeamColour.Red, TeamColour.Blue, TeamColour.Blue, TeamColour.Red,
                TeamColour.Red, TeamColour.Blue,
                TeamColour.Blue, TeamColour.Red
            };

            if (CurrentMatch.Value?.Round.Value == null)
                return;

            int pickedAndBannedCount = CurrentMatch.Value.PicksBans.Count;
            bool shouldBan = pickedAndBannedCount < shouldBanAtCount.Length && shouldBanAtCount[pickedAndBannedCount];
            TeamColour nextColour = pickedAndBannedCount < teamColourOrder.Length ? teamColourOrder[pickedAndBannedCount] : TeamColour.Red;

            setMode(nextColour, shouldBan ? ChoiceType.Ban : ChoiceType.Pick);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var maps = availableMapsFlows.Select(f => f.FirstOrDefault(m => m.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)));
            var map = maps.FirstOrDefault(m => m != null);

            if (map != null)
            {
                if (e.Button == MouseButton.Left && map.Beatmap?.OnlineID > 0)
                    addForBeatmap(map.Beatmap.OnlineID);
                else
                {
                    var existing = CurrentMatch.Value?.PicksBans.FirstOrDefault(p => p.BeatmapID == map.Beatmap?.OnlineID);

                    if (existing != null)
                    {
                        CurrentMatch.Value?.PicksBans.Remove(existing);
                        setNextMode();
                    }
                }

                updatePickedDisplay();
                return true;
            }

            return base.OnMouseDown(e);
        }

        private void reset()
        {
            CurrentMatch.Value?.PicksBans.Clear();
            setNextMode();
        }

        private void addForBeatmap(int beatmapId)
        {
            if (CurrentMatch.Value?.Round.Value == null)
                return;

            if (CurrentMatch.Value.Round.Value.Beatmaps.All(b => b.Beatmap?.OnlineID != beatmapId))
                // don't attempt to add if the beatmap isn't in our pool
                return;

            if (CurrentMatch.Value.PicksBans.Any(p => p.BeatmapID == beatmapId))
                // don't attempt to add if already exists.
                return;

            CurrentMatch.Value.PicksBans.Add(new BeatmapChoice
            {
                Team = pickColour,
                Type = pickType,
                BeatmapID = beatmapId
            });

            setNextMode();

            if (LadderInfo.AutoProgressScreens.Value && LadderInfo.CumulativeScore.Value == false)
            {
                if (pickType == ChoiceType.Pick && CurrentMatch.Value.PicksBans.Any(i => i.Type == ChoiceType.Pick))
                {
                    scheduledScreenChange?.Cancel();
                    scheduledScreenChange = Scheduler.AddDelayed(() => { sceneManager?.SetScreen(typeof(GameplayScreen)); }, 10000);
                }
            }
        }

        public override void Hide()
        {
            scheduledScreenChange?.Cancel();
            base.Hide();
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            base.CurrentMatchChanged(match);
            updatePoolDisplay();
            updatePickedDisplay();
        }

        private void updatePickedDisplay()
        {
            if (CurrentMatch.Value?.Round.Value == null)
                return;

            // remove extra panels
            pickedMapsFlow.RemoveAll(panel => CurrentMatch.Value.PicksBans.All(p => panel.Beatmap?.OnlineID != p.BeatmapID), true);

            // add missing panels
            var missingBeatmaps = CurrentMatch.Value.PicksBans
                                              .Where(pickBan
                                                  => pickBan.Type == ChoiceType.Pick
                                                     && pickedMapsFlow.All(panel => panel.Beatmap?.OnlineID != pickBan.BeatmapID)
                                              );

            foreach (var missingBeatmap in missingBeatmaps)
            {
                var map = CurrentMatch.Value.Round.Value.Beatmaps.FirstOrDefault(b => b.ID == missingBeatmap.BeatmapID);

                if (map != null)
                {
                    pickedMapsFlow.Add(new TournamentBeatmapPanel(map.Beatmap, map.Mods)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Height = 42,
                    });
                }
            }

            // add last decider panel if only one map is left untouched
            if (CurrentMatch.Value.Round.Value.Beatmaps.Count == CurrentMatch.Value.PicksBans.Count + 1)
            {
                var lastBeatmap = CurrentMatch.Value.Round.Value.Beatmaps.FirstOrDefault(b => CurrentMatch.Value.PicksBans.All(p => p.BeatmapID != b.ID));

                if (lastBeatmap != null)
                {
                    pickedMapsFlow.Add(new TournamentBeatmapPanel(lastBeatmap.Beatmap, lastBeatmap.Mods)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Height = 42,
                    });
                }
            }
        }

        private void updatePoolDisplay()
        {
            availableMapsFlows.Clear();

            if (CurrentMatch.Value == null)
                return;

            if (CurrentMatch.Value.Round.Value != null)
            {
                FillFlowContainer<TournamentBeatmapPanel>? currentFlow = null;
                string? currentMods = null;

                foreach (var b in CurrentMatch.Value.Round.Value.Beatmaps)
                {
                    if (currentFlow == null || (LadderInfo.SplitMapPoolByMods.Value && currentMods != b.Mods))
                    {
                        availableMapsFlows.Add(currentFlow = new FillFlowContainer<TournamentBeatmapPanel>
                        {
                            Spacing = new Vector2(10, 5),
                            Direction = FillDirection.Full,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y
                        });

                        currentMods = b.Mods;
                    }

                    currentFlow.Add(new TournamentBeatmapPanel(b.Beatmap, b.Mods)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Height = 42,
                        Width = 400,
                    });
                }
            }

            availableMapsFlows.Padding = new MarginPadding(5);
        }
    }
}
