// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
        private readonly TournamentTeam team;
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
            if (team != null)
            {
                // Child = new UserTile // left team, top left
                // {
                //     User = team.Players.FirstOrDefault()?.ToAPIUser(),
                //     Position = new Vector2(0, 0),
                //     Size = new Vector2(64),
                //     // Margin = new MarginPadding { Right = 20 }
                // };
                Children = new Drawable[]
                {
                    new UserTile
                    {
                        User = IsFlipped.Value ? team.Players.FirstOrDefault()?.ToAPIUser() : team.Players.LastOrDefault()?.ToAPIUser(),
                        Position = IsFlipped.Value ? new Vector2(0, playerOffsetY.Value) : new Vector2(playerOffsetX.Value, playerOffsetY.Value),
                        Size = new Vector2(64),
                        // Margin = new MarginPadding { Right = 20 },
                    },
                    new UserTile
                    {
                        User = IsFlipped.Value ? team.Players.LastOrDefault()?.ToAPIUser() : team.Players.FirstOrDefault()?.ToAPIUser(),
                        Position = IsFlipped.Value ? new Vector2(playerOffsetX.Value, 0) : new Vector2(0, 0),
                        Size = new Vector2(64),
                        // Margin = new MarginPadding { Right = 20 }
                    },
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (team == null) return;

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

            Size = new Vector2(96, 96);
            // Masking = true;
            // CornerRadius = 5;

            // Child = new UserTile // left team, top left
            // {
            //     User = team.Players.FirstOrDefault()?.ToAPIUser(),
            //     Position = new Vector2(0, 0),
            //     Size = new Vector2(64),
            //     // Margin = new MarginPadding { Right = 20 }
            // };

            // Child = flagSprite = new Sprite
            // {
            //     RelativeSizeAxes = Axes.Both,
            //     Anchor = Anchor.Centre,
            //     Origin = Anchor.Centre,
            //     FillMode = FillMode.Fill
            // };
            // Child = new UserTile // left team, top left
            // {
            //     User = team.Players.FirstOrDefault()?.ToAPIUser(),
            //     Position = new Vector2(0, 0),
            //     Size = new Vector2(64),
            //     // Margin = new MarginPadding { Right = 20 }
            // };

            // (flag = team.FlagName.GetBoundCopy()).BindValueChanged(_ => flagSprite.Texture = textures.Get($@"Flags/{team.FlagName}"), true);
        }
    }
}
