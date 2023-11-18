// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
                var knownGoodUsersList = new List<TournamentUser>
                {
                    new TournamentUser { OnlineID = 6447454 },
                    new TournamentUser { OnlineID = 6600930 },
                    new TournamentUser { OnlineID = 7562902 },
                    new TournamentUser { OnlineID = 9269034 },
                    new TournamentUser { OnlineID = 11367222 },
                    new TournamentUser { OnlineID = 12779141 },
                    new TournamentUser { OnlineID = 10549880 },
                    new TournamentUser { OnlineID = 13211727 },
                    new TournamentUser { OnlineID = 10323184 },
                };
                var newPlayers1 = new BindableList<TournamentUser>();
                var newPlayers2 = new BindableList<TournamentUser>();
                Ladder.CurrentMatch.Value?.Team1.Value?.Players.BindTo(newPlayers1);
                Ladder.CurrentMatch.Value?.Team2.Value?.Players.BindTo(newPlayers2);
                newPlayers1.Clear();
                newPlayers2.Clear();

                Random rnd = new Random();
                newPlayers1.AddRange(knownGoodUsersList.OrderBy(x => rnd.Next()).Take(3));
                newPlayers2.AddRange(knownGoodUsersList.OrderBy(x => rnd.Next()).Take(3));
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
