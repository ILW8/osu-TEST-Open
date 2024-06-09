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
    public partial class DrawableTeamFlag(TournamentTeam team) : Container
    {
        private readonly BindableList<TournamentUser> players = team.Players.GetBoundCopy();

        private void updateChildren()
        {
            Children = new Drawable[]
            {
                new UserTile
                {
                    User = players.ElementAtOrDefault(0)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(0, 0),
                    Size = new Vector2(1f / 3f, 1),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both,
                    Alpha = players.ElementAtOrDefault(0) != null ? 1 : 0
                },
                new UserTile
                {
                    User = players.ElementAtOrDefault(1)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(1f / 3f, 0),
                    Size = new Vector2(1f / 3f, 1),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both,
                    Alpha = players.ElementAtOrDefault(1) != null ? 1 : 0
                },
                new UserTile
                {
                    User = players.ElementAtOrDefault(2)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(2f / 3f, 0),
                    Size = new Vector2(1f / 3f, 1),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both,
                    Alpha = players.ElementAtOrDefault(2) != null ? 1 : 0
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            players.BindCollectionChanged((_, _) => updateChildren(), true);
        }
    }

    public partial class TriangleDrawableTeamFlag(TournamentTeam team) : Container
    {
        private readonly BindableList<TournamentUser> players = team.Players.GetBoundCopy();

        private void updateChildren()
        {
            Children = new Drawable[]
            {
                new RoundedPaddedUserTile
                {
                    User = players.ElementAtOrDefault(0)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(1f / 4f, 0),
                    Size = new Vector2(1f / 2f, 1f / 2f),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both
                },
                new RoundedPaddedUserTile
                {
                    User = players.ElementAtOrDefault(1)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(0, 1f / 2f),
                    Size = new Vector2(1f / 2f, 1f / 2f),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both
                },
                new RoundedPaddedUserTile
                {
                    User = players.ElementAtOrDefault(2)?.ToAPIUser() ?? new APIUser(),
                    Position = new Vector2(1f / 2f, 1f / 2f),
                    Size = new Vector2(1f / 2f, 1f / 2f),
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Both
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            players.BindCollectionChanged((_, _) => updateChildren(), true);
        }
    }

    public partial class RoundedPaddedUserTile : CompositeDrawable
    {
        public APIUser? User
        {
            get => userTile.User;
            set => userTile.User = value;
        }

        private readonly RoundedUserTile userTile;

        public RoundedPaddedUserTile()
        {
            Padding = new MarginPadding(3);
            InternalChildren = new Drawable[]
            {
                userTile = new RoundedUserTile
                {
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(1f),
                }
            };
        }
    }

    public partial class RoundedUserTile : UserTile
    {
        public RoundedUserTile()
        {
            CornerRadius = 25f;
        }
    }
}
