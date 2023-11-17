// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.Models;
using osu.Game.Tournament.Screens.Gameplay;

namespace osu.Game.Tournament.Tests.Screens
{
    public partial class TestSceneGameplayScreenWLC : TournamentScreenTestScene
    {
        [Cached]
        private TournamentMatchChatDisplay chat = new TournamentMatchChatDisplay { Width = 0.5f };

        // protected override void LoadComplete()
        // {
        //
        // }

        [Test]
        public void TestWarmup()
        {
            AddStep("update users", () =>
            {
                var newPlayers = new BindableList<TournamentUser>();
                Ladder.CurrentMatch.Value?.Team1.Value?.Players.BindTo(newPlayers);
                newPlayers.Clear();
                newPlayers.AddRange(new List<TournamentUser>
                {
                    new TournamentUser { OnlineID = 14167692 },
                    new TournamentUser { OnlineID = 5182050 },
                    new TournamentUser { OnlineID = 9501251 },
                });
            });
            createScreen();
            toggleWarmup();
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

        private void toggleWarmup()
            => AddStep("toggle warmup", () => this.ChildrenOfType<TourneyButton>().First().TriggerClick());
    }
}
