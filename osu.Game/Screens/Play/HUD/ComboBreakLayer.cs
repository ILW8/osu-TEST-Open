// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osuTK.Graphics;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// An overlay layer on top of the playfield which flashes red when the current player breaks combo
    /// </summary>
    public partial class ComboBreakLayer : CompositeDrawable
    {
        private const float max_alpha = 0.4f;
        private const int fade_time = 400;
        private const float gradient_size = 0.3f;

        /// <summary>
        /// The minimum combo that needs to be reached before combo break overlay flashes red on combo break.
        /// </summary>
        private const int minimum_combo_threshold = 8;

        private readonly Container boxes;

        private int oldCombo = 0;

        public ComboBreakLayer()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                boxes = new Container
                {
                    Alpha = 0,
                    Blending = BlendingParameters.Additive,
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.White, Color4.White.Opacity(0)),
                            Width = gradient_size,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = gradient_size,
                            Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0), Color4.White),
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                        },
                    }
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour color, ScoreProcessor scoreProcessor)
        {
            boxes.Colour = color.Red;

            scoreProcessor.Combo.BindValueChanged(vce => onComboUpdated(vce.NewValue));
        }

        private void onComboUpdated(int newCombo)
        {
            if (newCombo < oldCombo && oldCombo >= minimum_combo_threshold && newCombo < minimum_combo_threshold)
            {
                // flash here
                boxes.FadeInFromZero(50).Then().FadeOut(500, Easing.Out);
                Logger.Log(@$"old: {oldCombo}, new: {newCombo} flashing combo break boxes!");
            }

            oldCombo = newCombo;
        }
    }
}
