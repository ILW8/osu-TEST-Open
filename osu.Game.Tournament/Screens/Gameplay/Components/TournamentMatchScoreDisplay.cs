// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
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
        private readonly BindableFloat accuracy1 = new BindableFloat();
        private readonly BindableFloat accuracy2 = new BindableFloat();

        private readonly MatchScoreCounter score1Text;
        private readonly MatchScoreCounter score1HiddenText;
        private readonly MatchScoreCounter score2Text;
        private readonly MatchScoreCounter score2HiddenText;
        private readonly AccScoreCounter acc1Text;
        private readonly AccScoreCounter acc2Text;

        private readonly Drawable score1Bar;
        private readonly Drawable score2Bar;

        public TournamentMatchScoreDisplay()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new[]
            {
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
                score1HiddenText = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new Vector2(0.8f),
                    Colour = new Color4(0, 255, 12, 255),
                    Y = -48
                },
                acc1Text = new AccScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = -128
                },
                score2HiddenText = new MatchScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Scale = new Vector2(0.8f),
                    Colour = new Color4(0, 255, 12, 255),
                    Y = -48
                },
                acc2Text = new AccScoreCounter
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = -128
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
            // score1Mult.BindValueChanged(_ => updateScores());
            score1.BindTo(ipc.Score1);

            score2.BindValueChanged(_ => updateScores());
            score2.BindTo(ipc.Score2);

            accuracy1.BindValueChanged(_ => Scheduler.AddOnce(updateScores));
            accuracy1.BindTo(ipc.Accuracy1);
            accuracy2.BindValueChanged(_ => Scheduler.AddOnce(updateScores));
            accuracy2.BindTo(ipc.Accuracy2);
        }

        private void updateScores()
        {
            score1Text.Current.Value = score1.Value;
            score2Text.Current.Value = score2.Value;
            acc1Text.Current.Value = accuracy1.Value;
            acc2Text.Current.Value = accuracy2.Value;
            score1HiddenText.Current.Value = score1.Value;
            score2HiddenText.Current.Value = score2.Value;
            float diffMultScore = Math.Max(accuracy1.Value, accuracy2.Value) - Math.Min(accuracy1.Value, accuracy2.Value);

            float fullWinnerWidth = Math.Min(0.4f, MathF.Pow(diffMultScore / 10f, 0.5f) / 2);

            var winningText = accuracy1.Value > accuracy2.Value ? acc1Text : acc2Text;
            var losingText = accuracy1.Value <= accuracy2.Value ? acc1Text : acc2Text;
            var winningBarBase = accuracy1.Value > accuracy2.Value ? score1Bar : score2Bar;
            var losingBarBase = accuracy1.Value <= accuracy2.Value ? score1Bar : score2Bar;

            winningText.Winning = true;
            losingText.Winning = Math.Abs(accuracy1.Value - accuracy2.Value) < 0.005; // mark both as winning if same accuracy
            losingBarBase.ResizeWidthTo(0, 400, Easing.OutQuint);
            winningBarBase.ResizeWidthTo(fullWinnerWidth, 400, Easing.OutQuint);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            // score1MultipliedText.Y = 28;
            score1Text.X = -Math.Max(5 + score1Text.DrawWidth / 2, score1Bar.DrawWidth);
            score1HiddenText.X = -Math.Max(5 + score1Text.DrawWidth / 2, score1Bar.DrawWidth);
            acc1Text.X = -Math.Max(5 + acc1Text.DrawWidth / 2, score1Bar.DrawWidth);

            // score2MultipliedText.Y = 28;
            score2Text.X = Math.Max(5 + score2Text.DrawWidth / 2, score2Bar.DrawWidth);
            score2HiddenText.X = Math.Max(5 + score2Text.DrawWidth / 2, score2Bar.DrawWidth);
            acc2Text.X = Math.Max(5 + acc2Text.DrawWidth / 2, score2Bar.DrawWidth);
        }

        private partial class AccScoreCounter : MatchScoreCounter
        {
            protected override double RollingDuration => 500;

            protected override LocalisableString FormatCount(double count) => $"{count:F2}%";
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
