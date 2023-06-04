// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Tournament.IPC;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    // TODO: Update to derive from osu-side class?
    public partial class TournamentMatchScoreDisplay : CompositeDrawable
    {
        private const float bar_height = 18;

        private readonly BindableInt score1 = new BindableInt();
        private readonly BindableInt score2 = new BindableInt();
        private readonly BindableInt score1Mult = new BindableInt();
        private readonly BindableInt score2Mult = new BindableInt();

        private readonly MatchScoreCounter score1Text;
        private readonly MatchScoreCounter score1MultipliedText;
        private readonly MatchScoreCounter score2Text;
        private readonly MatchScoreCounter score2MultipliedText;

        private readonly Drawable score1Bar;
        private readonly Drawable score1BarMultiplied;
        private readonly Drawable score2Bar;
        private readonly Drawable score2BarMultiplied;

        public TournamentMatchScoreDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new[]
            {
                score1BarMultiplied = new Box
                {
                    Name = "top bar red mult",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height,
                    Width = 0,
                    Colour = new OsuColour().Red,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopRight
                },
                score2BarMultiplied = new Box
                {
                    Name = "top bar blue mult",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height,
                    Width = 0,
                    Colour = new OsuColour().Blue,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopLeft
                },
                new Box
                {
                    Name = "top bar red (static)",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height / 4,
                    Width = 0.5f,
                    Colour = TournamentGame.COLOUR_RED,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopRight
                },
                new Box
                {
                    Name = "top bar blue (static)",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height / 4,
                    Width = 0.5f,
                    Colour = TournamentGame.COLOUR_BLUE,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopLeft
                },
                score1Bar = new Box
                {
                    Name = "top bar red",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height,
                    Width = 0,
                    Colour = TournamentGame.COLOUR_RED,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopRight
                },
                score1Text = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                },
                score1MultipliedText = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new Vector2(0.8f),
                    Colour = new Color4(0, 255, 12, 255),
                    Y = -48
                },
                score2MultipliedText = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new Vector2(0.8f),
                    Colour = new Color4(0, 255, 12, 255),
                    Y = -48
                },
                score2Bar = new Box
                {
                    Name = "top bar blue",
                    RelativeSizeAxes = Axes.X,
                    Height = bar_height,
                    Width = 0,
                    Colour = TournamentGame.COLOUR_BLUE,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopLeft
                },
                score2Text = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(MatchIPCInfo ipc)
        {
            score1.BindValueChanged(_ => updateScores());
            score1Mult.BindValueChanged(_ => updateScores());
            score1.BindTo(ipc.Score1);
            score1Mult.BindTo(ipc.Score1WithMult);

            score2.BindValueChanged(_ => updateScores());
            score2Mult.BindValueChanged(_ => updateScores());
            score2.BindTo(ipc.Score2);
            score2Mult.BindTo(ipc.Score2WithMult);
        }

        private void updateScores()
        {
            if (score1Mult.Value == -1 || score2Mult.Value == -1 || (score1.Value == 0 && score2.Value == 0))
            {
                score1Mult.Value = score1.Value;
                score2Mult.Value = score2.Value;
            }
            score1Text.Current.Value = score1.Value;
            score2Text.Current.Value = score2.Value;
            score1MultipliedText.Current.Value = score1Mult.Value;
            score2MultipliedText.Current.Value = score2Mult.Value;
            int diffMultScore = Math.Max(score1Mult.Value, score2Mult.Value) - Math.Min(score1Mult.Value, score2Mult.Value);

            // if winning side's point advantage is less than its score bonus, base bar needs to be 0.
            int winnerDiffBaseScore = Math.Max(0, score1Mult.Value > score2Mult.Value ? score1.Value - score2Mult.Value : score2.Value - score1Mult.Value);
            // this will only work if theBaseScoreAdvantageOverLoserTeam <= diffMultScore (which is always the case when multiplier >= 1.0x
            float winDeltaBaseScoreRatio = diffMultScore > 0 ? Math.Min(1.0f, winnerDiffBaseScore / (float)diffMultScore) : 1.0f; // ternary to handle when both scores == 0
            float fullWinnerWidth = Math.Min(0.4f, MathF.Pow(diffMultScore / 1500000f, 0.5f) / 2);

            var winningText = score1.Value > score2.Value ? score1Text : score2Text;
            var losingText = score1.Value <= score2.Value ? score1Text : score2Text;
            var winningBarBase = score1Mult.Value > score2Mult.Value ? score1Bar : score2Bar;
            var losingBarBase = score1Mult.Value <= score2Mult.Value ? score1Bar : score2Bar;
            var winningBarMult = score1Mult.Value > score2Mult.Value ? score1BarMultiplied : score2BarMultiplied;
            var losingBarMult = score1Mult.Value <= score2Mult.Value ? score1BarMultiplied : score2BarMultiplied;
            winningText.Winning = true;
            losingText.Winning = false;
            losingBarBase.ResizeWidthTo(0, 400, Easing.OutQuint);
            losingBarMult.ResizeWidthTo(0, 400, Easing.OutQuint);
            winningBarBase.ResizeWidthTo(fullWinnerWidth * winDeltaBaseScoreRatio, 400, Easing.OutQuint);
            winningBarMult.ResizeWidthTo(fullWinnerWidth, 400, Easing.OutQuint);

            Logger.Log($"winner advantage base: {winnerDiffBaseScore} mult:{diffMultScore} || "
                + $"delta base ratio: {winDeltaBaseScoreRatio}",
                LoggingTarget.Runtime, LogLevel.Important);

        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            // score1MultipliedText.Y = 28;
            score1Text.X = -Math.Max(5 + score1MultipliedText.DrawWidth / 2, score1BarMultiplied.DrawWidth);
            score1MultipliedText.X = -Math.Max(5 + score1MultipliedText.DrawWidth / 2, score1BarMultiplied.DrawWidth);

            // score2MultipliedText.Y = 28;
            score2Text.X = Math.Max(5 + score2MultipliedText.DrawWidth / 2, score2BarMultiplied.DrawWidth);
            score2MultipliedText.X = Math.Max(5 + score2MultipliedText.DrawWidth / 2, score2BarMultiplied.DrawWidth);
        }

        private partial class MatchScoreCounter : CommaSeparatedScoreCounter
        {
            private OsuSpriteText displayedSpriteText;

            public MatchScoreCounter()
            {
                Margin = new MarginPadding { Top = bar_height, Horizontal = 10 };
            }

            public bool Winning
            {
                set => updateFont(value);
            }

            protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
            {
                displayedSpriteText = s;
                displayedSpriteText.Spacing = new Vector2(-6);
                updateFont(false);
            });

            private void updateFont(bool winning)
                => displayedSpriteText.Font = winning
                    ? OsuFont.Torus.With(weight: FontWeight.Bold, size: 50, fixedWidth: true)
                    : OsuFont.Torus.With(weight: FontWeight.Regular, size: 40, fixedWidth: true);
        }
    }
}
