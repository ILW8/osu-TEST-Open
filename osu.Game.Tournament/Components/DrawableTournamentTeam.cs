// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Components
{
    public abstract partial class DrawableTournamentTeam : CompositeDrawable
    {
        public readonly TournamentTeam Team;

        protected readonly DrawableTeamFlag Flag;
        protected readonly TournamentSpriteText AcronymText;

        [UsedImplicitly]
        private Bindable<string> acronym;

        protected DrawableTournamentTeam(TournamentTeam team)
        {
            Team = team;

            // Flag = new DrawableTeamFlag(team);

            Flag = new DrawableTeamFlag(team);

            // Flag = team == null
            //     ? new DrawableTeamFlag(null)
            //     : new Container
            //     {
            //         AutoSizeAxes = Axes.Both,
            //         // Direction = FillDirection.Horizontal,
            //         Padding = new MarginPadding { Left = 0 },
            //         // Spacing = new Vector2(0),
            //         Children = new Drawable[]
            //         {
            //             new UserTile // left team, top left
            //             {
            //                 // User = team.Players.FirstOrDefault()?.ToAPIUser(),
            //                 Position = new Vector2(0, 0),
            //                 Size = new Vector2(64),
            //                 Margin = new MarginPadding { Right = 20 }
            //             },
            //             new UserTile // left team, bottom right
            //             {
            //                 // User = team.Players.LastOrDefault()?.ToAPIUser(),
            //                 Position = new Vector2(48, 48),
            //                 Size = new Vector2(64),
            //                 Margin = new MarginPadding { Right = 20 }
            //             },
            //         },
            //     };

            AcronymText = new TournamentSpriteText
            {
                Font = OsuFont.Torus.With(weight: FontWeight.Regular),
            };
        }

        public void UpdateFlagAnchor(Anchor newAnchor)
        {
            Flag.Anchor = newAnchor;
            Flag.Origin = newAnchor;
            Flag.IsFlipped.Value = newAnchor == Anchor.TopRight;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Team == null) return;

            Logger.Log("Called load on DrawableTournamentTeam", LoggingTarget.Runtime);

            (acronym = Team.Acronym.GetBoundCopy()).BindValueChanged(_ => AcronymText.Text = Team?.Acronym.Value?.ToUpperInvariant() ?? string.Empty, true);
            // Flag.Child = new DrawableTeamFlag(Team);
            Flag.Clear();
            Flag.Child = new DrawableTeamFlag(Team, Flag.Anchor == Anchor.TopRight);
            // LoadComponentAsync(new DrawableTeamFlag(Team), Flag.Add);

            // Flag.FadeOut();
            // Flag.FadeIn(500, Easing.OutQuint);
        }
    }
}
