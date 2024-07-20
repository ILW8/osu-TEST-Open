// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.Models;
using osuTK;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    public partial class TeamDisplay : DrawableTournamentTeam
    {
        protected partial class MatchCumulativeScoreCounter : CommaSeparatedScoreCounter
        {
            private OsuSpriteText displayedSpriteText = null!;
            private const int font_size = 50;
            private Bindable<bool> useCumulativeScore = null!;

            [Resolved]
            private LadderInfo ladder { get; set; } = null!;

            public MatchCumulativeScoreCounter()
            {
                Margin = new MarginPadding(8);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                useCumulativeScore = ladder.CumulativeScore.GetBoundCopy();
                useCumulativeScore.BindValueChanged(v => displayedSpriteText.Alpha = v.NewValue ? 1 : 0, true);
            }

            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                displayedSpriteText = s;
                displayedSpriteText.Spacing = new Vector2(-6);
                displayedSpriteText.Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: font_size, fixedWidth: true);
            });
        }

        private readonly TeamScore score;

        private readonly MatchCumulativeScoreCounter cumulativeScoreCounter;

        private readonly TournamentSpriteTextWithBackground teamNameText;

        private readonly Bindable<string> teamName = new Bindable<string>("???");

        private bool showScore;

        public bool ShowScore
        {
            get => showScore;
            set
            {
                if (showScore == value)
                    return;

                showScore = value;

                if (IsLoaded)
                    updateDisplay();
            }
        }

        public TeamDisplay(TournamentTeam? team, TeamColour colour, Bindable<long?> currentTeamScore, int pointsToWin)
            : base(team)
        {
            AutoSizeAxes = Axes.Both;

            bool flip = colour == TeamColour.Red;

            var anchor = flip ? Anchor.TopLeft : Anchor.TopRight;

            Flag.RelativeSizeAxes = Axes.None;
            Flag.Scale = new Vector2(0.8f);
            Flag.Origin = anchor;
            Flag.Anchor = anchor;

            Margin = new MarginPadding(20);

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            Flag,
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Origin = anchor,
                                Anchor = anchor,
                                Spacing = new Vector2(5),
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(5),
                                        Origin = anchor,
                                        Anchor = anchor,
                                        Children = new Drawable[]
                                        {
                                            new DrawableTeamHeader(colour)
                                            {
                                                Scale = new Vector2(0.75f),
                                                Origin = anchor,
                                                Anchor = anchor,
                                            },
                                            score = new TeamScore(currentTeamScore, colour, pointsToWin)
                                            {
                                                Origin = anchor,
                                                Anchor = anchor,
                                            }
                                        }
                                    },
                                    teamNameText = new TournamentSpriteTextWithBackground
                                    {
                                        Scale = new Vector2(0.5f),
                                        Origin = anchor,
                                        Anchor = anchor,
                                    },
                                    new DrawableTeamSeed(Team)
                                    {
                                        Scale = new Vector2(0.5f),
                                        Origin = anchor,
                                        Anchor = anchor,
                                    },
                                }
                            },
                            cumulativeScoreCounter = new MatchCumulativeScoreCounter
                            {
                                Origin = anchor,
                                Anchor = anchor,
                            },
                        }
                    },
                }
            };
            currentTeamScore.BindValueChanged(_ =>
            {
                cumulativeScoreCounter.Current.Value = currentTeamScore.Value ?? 0;
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            updateDisplay();
            FinishTransforms(true);

            if (Team != null)
                teamName.BindTo(Team.FullName);

            teamName.BindValueChanged(name => teamNameText.Text.Text = name.NewValue, true);
        }

        private void updateDisplay()
        {
            score.FadeTo(ShowScore ? 1 : 0, 200);
            cumulativeScoreCounter.FadeTo(ShowScore ? 1 : 0, 200);
        }
    }
}
