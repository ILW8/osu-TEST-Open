// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneComboBreakLayer : OsuTestScene
    {
        private readonly Bindable<bool> showHealth = new Bindable<bool>();

        [Cached]
        private ScoreProcessor scoreProcessor = new ScoreProcessor(new OsuRuleset());

        private void create()
        {
            AddStep("create combo break layer", () =>
            {
                Child = new ComboBreakLayer
                {
                    RelativeSizeAxes = Axes.Both,
                };
            });

            AddStep("add argon combo counter", () =>
            {
                Add(new ArgonComboCounter());
            });
        }

        [Test]
        public void TestLayerFading()
        {
            create();

            AddStep("set high combo", () => scoreProcessor.Combo.Value = 16);
            AddStep("add more combo", () => scoreProcessor.Combo.Value++);
            AddStep("reset combo", () => scoreProcessor.Combo.Value = 0);
        }
    }
}
