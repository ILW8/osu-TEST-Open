// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Screens.Gameplay;
using osu.Game.Tournament.Screens.Gameplay.Components;
using osu.Game.TournamentIpc;

namespace osu.Game.Tournament.Tests.Screens
{
    public partial class TestSceneGameplayScreen : TournamentScreenTestScene
    {
        [Cached]
        private TournamentMatchChatDisplay chat = new TournamentMatchChatDisplay { Width = 0.5f };

        [Test]
        public void TestWarmup()
        {
            createScreen();

            checkScoreVisibility(false);

            toggleWarmup();
            checkScoreVisibility(true);

            toggleWarmup();
            checkScoreVisibility(false);
        }

        [Test]
        public void TestScoreAdd()
        {
            AddStep("disable cumulative score", () => Ladder.CumulativeScore.Value = false);

            createScreen();
            toggleWarmup();

            AddStep("set state: lobby", () => LazerIPCInfo.State.Value = TourneyState.Lobby);

            AddStep("set state: playing", () => LazerIPCInfo.State.Value = TourneyState.Playing);
            AddStep("add score", () =>
            {
                LazerIPCInfo.Score1.Value = 127_727;
                LazerIPCInfo.Score2.Value = 63_727;
            });
            AddStep("set state: ranking", () => LazerIPCInfo.State.Value = TourneyState.Ranking);
        }

        [Test]
        public void TestScoreAddCumulative()
        {
            AddStep("enable cumulative score", () => Ladder.CumulativeScore.Value = true);

            createScreen();
            toggleWarmup();

            AddStep("set state: lobby", () => LazerIPCInfo.State.Value = TourneyState.Lobby);

            AddStep("set state: playing", () => LazerIPCInfo.State.Value = TourneyState.Playing);
            AddStep("add score", () =>
            {
                LazerIPCInfo.Score1.Value = 127_727;
                LazerIPCInfo.Score2.Value = 63_727;
            });
            AddStep("set state: ranking", () => LazerIPCInfo.State.Value = TourneyState.Ranking);
        }

        [Test]
        public void TestStartupState([Values] LegacyTourneyState state)
        {
            AddStep("set state", () => IPCInfo.State.Value = state);
            createScreen();
        }

        [Test]
        public void TestStartupStateNoCurrentMatch([Values] LegacyTourneyState state)
        {
            AddStep("set null current", () => Ladder.CurrentMatch.Value = null);
            AddStep("set state", () => IPCInfo.State.Value = state);
            createScreen();
        }

        private void createScreen()
        {
            AddStep("setup screen", () =>
            {
                Remove(chat, false);

                Children = new Drawable[]
                {
                    new GameplayScreen(),
                    chat,
                };
            });
        }

        private void checkScoreVisibility(bool visible)
            => AddUntilStep($"scores {(visible ? "shown" : "hidden")}",
                () => this.ChildrenOfType<TeamScore>().All(score => score.Alpha == (visible ? 1 : 0)));

        private void toggleWarmup()
            => AddStep("toggle warmup", () => this.ChildrenOfType<TourneyButton>().First().TriggerClick());
    }
}
