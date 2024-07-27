// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    public partial class ComboEffects : CompositeDrawable
    {
        private readonly ScoreProcessor processor;

        private SkinnableSound comboBreakSample;

        private Bindable<bool> alwaysPlayFirst;

        private double? firstBreakTime;

        private readonly Container boxes;
        private const float max_alpha = 0.65f;
        private const int fade_time = 750;
        private const float gradient_size = 0.3f;

        public ComboEffects(ScoreProcessor processor)
        {
            this.processor = processor;

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
        private void load(OsuConfigManager config, OsuColour color)
        {
            AddInternal(comboBreakSample = new SkinnableSound(new SampleInfo("Gameplay/combobreak")));
            alwaysPlayFirst = config.GetBindable<bool>(OsuSetting.AlwaysPlayFirstComboBreak);

            boxes.Colour = color.Red;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            processor.Combo.BindValueChanged(onComboChange);
        }

        [Resolved(canBeNull: true)]
        private ISamplePlaybackDisabler samplePlaybackDisabler { get; set; }

        [Resolved]
        private IGameplayClock gameplayClock { get; set; }

        private void onComboChange(ValueChangedEvent<int> combo)
        {
            // handle the case of rewinding before the first combo break time.
            if (gameplayClock.CurrentTime < firstBreakTime)
                firstBreakTime = null;

            if (gameplayClock.IsRewinding)
                return;

            if (combo.NewValue == 0 && (combo.OldValue > 20 || (alwaysPlayFirst.Value && firstBreakTime == null)))
            {
                firstBreakTime = gameplayClock.CurrentTime;

                // combo break isn't a pausable sound itself as we want to let it play out.
                // we still need to disable during seeks, though.
                if (samplePlaybackDisabler?.SamplePlaybackDisabled.Value == true)
                    return;

                comboBreakSample?.Play();
                boxes.FadeOut().Then().FadeTo(max_alpha, 50).Then().FadeOut(fade_time, Easing.Out);
            }
        }
    }
}
