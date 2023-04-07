// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Tournament.Models;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tournament.Components
{
    public partial class DrawableTeamWithPlayers : CompositeDrawable
    {
        public DrawableTeamWithPlayers(TournamentTeam team, TeamColour colour)
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(30),
                    Children = new Drawable[]
                    {
                        new DrawableTeamTitleWithHeader(team, colour),
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Padding = new MarginPadding { Left = 10 },
                            Spacing = new Vector2(30),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Vertical,
                                    AutoSizeAxes = Axes.Both,
                                    Spacing = new Vector2(12),
                                    ChildrenEnumerable = team?.Players.Select(createPlayerText).Take(5) ?? Enumerable.Empty<Drawable>()
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Vertical,
                                    AutoSizeAxes = Axes.Both,
                                    Spacing = new Vector2(12),
                                    ChildrenEnumerable = team?.Players.Select(createPlayerText).Skip(5) ?? Enumerable.Empty<Drawable>()
                                },
                            }
                        },
                    }
                },
            };

            Drawable createPlayerText(TournamentUser p) =>
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(16),
                    Children = new Drawable[]
                    {
                        // new UserTile
                        // {
                        //     User = p.ToAPIUser(),
                        //     Size = new Vector2(32),
                        //     // Position = new Vector2(727, y_flag_screen_offset + y_flag_relative_offset),
                        //     // Scale = new Vector2(flag_size_scale),
                        //     // Margin = new MarginPadding { Right = 20 }
                        // },
                        new UpdateableFlag(p.CountryCode)
                        {
                            Margin = new MarginPadding() { Top = 3 },
                            Size = new Vector2(32, 23),
                        },
                        new TournamentSpriteText
                        {
                            Text = p.Username,
                            Font = OsuFont.Torus.With(size: 24, weight: FontWeight.SemiBold),
                            Colour = Color4.White,
                        },
                    }
                };
        }
    }
}
