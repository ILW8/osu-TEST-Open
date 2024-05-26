// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.Models;
using osuTK;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    public partial class TeamDisplay : DrawableTournamentTeam
    {
        private readonly TeamScore score;

        private readonly TournamentSpriteTextWithBackground teamNameText;

        private readonly Bindable<string> teamName = new Bindable<string>("???");

        private readonly FillFlowContainer teamNameAndScoreContainer;

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

        public TeamDisplay(TournamentTeam? team, TeamColour colour, Bindable<int?> currentTeamScore, int pointsToWin)
            : base(team)
        {
            AutoSizeAxes = Axes.Both;

            bool flip = colour == TeamColour.Red;

            var anchor = flip ? Anchor.TopLeft : Anchor.TopRight;

            Margin = new MarginPadding(8);

            teamNameAndScoreContainer = new FillFlowContainer
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
                    }
                }
            };

            var teamNameAndScorePlusSeedContainer = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Origin = anchor,
                Anchor = anchor,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    teamNameAndScoreContainer,
                    new DrawableTeamSeed(Team)
                    {
                        Scale = new Vector2(0.5f),
                        Origin = anchor,
                        Anchor = anchor,
                    },
                }
            };

            Flag.RelativeSizeAxes = Axes.None;
            Flag.Size = new Vector2(160, teamNameAndScoreContainer.DrawHeight);
            // Flag.Origin = anchor;
            // Flag.Anchor = anchor;
            UpdateFlagAnchor(anchor);

            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Margin = new MarginPadding { Top = 12f },
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(16),
                        Children = new Drawable[]
                        {
                            Flag,
                            teamNameAndScorePlusSeedContainer
                        }
                    },
                }
            };
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
            Flag.Size = new Vector2(160, teamNameAndScoreContainer.DrawHeight);
            score.FadeTo(ShowScore ? 1 : 0, 200);
        }

        protected override bool OnInvalidate(Invalidation invalidation, InvalidationSource source)
        {
            if (Math.Abs(Flag.Size.Y - teamNameAndScoreContainer.DrawHeight) < 0.001f) return base.OnInvalidate(invalidation, source);

            Flag.Size = new Vector2(160, teamNameAndScoreContainer.DrawHeight);
            return true;
        }
    }
}
