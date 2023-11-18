// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Tournament.Models;
using osu.Game.Tournament.Screens.TeamIntro;
using osuTK;

namespace osu.Game.Tournament.Components
{
    public partial class DrawableTeamFlag : Container
    {
        private readonly Bindable<float> playerOffsetX = new Bindable<float>(32f);
        private readonly Bindable<float> playerOffsetY = new Bindable<float>(32f);
        public float PlayerOffsetX { get => playerOffsetX.Value; set => playerOffsetX.Value = value; }
        public float PlayerOffsetY { get => playerOffsetY.Value; set => playerOffsetY.Value = value; }
        private readonly TournamentTeam? team;
        public Bindable<bool> IsFlipped = new Bindable<bool>();

        // [UsedImplicitly]
        // private Bindable<string> flag;

        // private Sprite flagSprite;
        // private UserTile leftUser;
        // private UserTile rightUser;

        public DrawableTeamFlag(TournamentTeam team, bool isFlipped = false)
        {
            IsFlipped.Value = isFlipped;
            this.team = team;
            IsFlipped.BindValueChanged(_ => updateChildren(), true);
            playerOffsetX.BindValueChanged(_ => updateChildren(), true);
            playerOffsetY.BindValueChanged(_ => updateChildren(), true);
        }

        private void updateChildren()
        {
            if (team == null) return;

            if (IsFlipped.Value)
            {
                Position = new Vector2(playerOffsetX.Value, 0);
            }

            Children = new Drawable[]
            {
                new UserTile
                {
                    User = (IsFlipped.Value ? team.Players.FirstOrDefault()?.ToAPIUser() : team.Players.LastOrDefault()?.ToAPIUser()) ?? new APIUser(),
                    Position = IsFlipped.Value ? new Vector2(-playerOffsetX.Value, playerOffsetY.Value) : new Vector2(playerOffsetX.Value, playerOffsetY.Value),
                    Size = new Vector2(64),
                    // Margin = new MarginPadding { Right = 20 },
                },
                new UserTile
                {
                    User = (IsFlipped.Value ? team.Players.LastOrDefault()?.ToAPIUser() : team.Players.FirstOrDefault()?.ToAPIUser()) ?? new APIUser(),
                    Position = new Vector2(0, 0),
                    Size = new Vector2(64),
                    // Margin = new MarginPadding { Right = 20 }
                },
                // new UserTile
                // {
                //     User = (IsFlipped.Value ? team.Players.LastOrDefault()?.ToAPIUser() : team.Players.FirstOrDefault()?.ToAPIUser()) ?? new APIUser(),
                //     Position = IsFlipped.Value ? new Vector2(playerOffsetX.Value, 0) : new Vector2(0, 0),
                //     Size = new Vector2(64),
                //     // Margin = new MarginPadding { Right = 20 }
                // },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (team == null) return;

            Size = new Vector2(96, 96);
        }
    }
}
