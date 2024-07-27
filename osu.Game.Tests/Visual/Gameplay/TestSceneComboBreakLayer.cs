// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneComboBreakLayer : OsuTestScene
    {
        [Cached]
        private readonly ScoreProcessor scoreProcessor = new ScoreProcessor(new OsuRuleset());

        [Cached(typeof(IGameplayClock))]
        private readonly IGameplayClock gameplayClock = new GameplayClockContainer(new TrackVirtual(60000), false, false);

        private void create()
        {
            AddStep("create combo effects", () => Child = new ComboEffects(scoreProcessor));

            AddStep("add argon combo counter", () => Add(new ArgonComboCounter()));
        }

        [Test]
        public void TestLayerFading()
        {
            create();

            AddRepeatStep("add combo", () => scoreProcessor.Combo.Value += 4, 6);
            AddStep("reset combo", () => scoreProcessor.Combo.Value = 0);
            AddAssert("combo break overlay is visible (first break)", () => Children[0].ChildrenOfType<Container>().First().Alpha > 0);
        }
    }
}
