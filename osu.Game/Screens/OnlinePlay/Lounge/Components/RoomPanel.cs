// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Screens.OnlinePlay.Components;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Extensions;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.Screens.OnlinePlay.Lounge.Components
{
    public abstract partial class RoomPanel : CompositeDrawable, IHasContextMenu
    {
        protected const float CORNER_RADIUS = 10;
        private const float height = 80;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private BeatmapLookupCache beatmapLookupCache { get; set; } = null!;

        public readonly Room Room;

        protected readonly Bindable<PlaylistItem?> SelectedItem = new Bindable<PlaylistItem?>();
        protected Container ButtonsContainer { get; private set; } = null!;

        private DrawableRoomParticipantsList? drawableRoomParticipantsList;
        private RoomSpecialCategoryPill? specialCategoryPill;
        private PasswordProtectedIcon? passwordIcon;
        private EndDateInfo? endDateInfo;
        private SpriteText? roomName;
        private DelayedLoadWrapper wrapper = null!;
        private CancellationTokenSource? beatmapLookupCancellation;

        /// <summary>
        /// A fully-populated representation of the selected item's current beatmap.
        /// </summary>
        private readonly Bindable<IBeatmapInfo?> currentBeatmap = new Bindable<IBeatmapInfo?>();

        protected RoomPanel(Room room)
        {
            Room = room;

            RelativeSizeAxes = Axes.X;
            Height = height;

            Masking = true;
            CornerRadius = CORNER_RADIUS;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(40),
                Radius = 5,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            ButtonsContainer = new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.Y,
                AutoSizeAxes = Axes.X
            };

            InternalChildren = new Drawable[]
            {
                // This resolves internal 1px gaps due to applying the (parenting) corner radius and masking across multiple filling background sprites.
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Background5,
                },
                CreateBackground().With(d =>
                {
                    d.RelativeSizeAxes = Axes.Both;
                    d.Beatmap.BindTarget = currentBeatmap;
                }),
                wrapper = new DelayedLoadWrapper(() =>
                    new Container
                    {
                        Name = @"Room content",
                        RelativeSizeAxes = Axes.Both,
                        // This negative padding resolves 1px gaps between this background and the background above.
                        Padding = new MarginPadding { Left = 20, Vertical = -0.5f },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            CornerRadius = CORNER_RADIUS,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colours.Background5,
                                    Width = 0.2f,
                                },
                                new Box
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourInfo.GradientHorizontal(colours.Background5, colours.Background5.Opacity(0.3f)),
                                    Width = 0.8f,
                                },
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new Container
                                            {
                                                Name = @"Left details",
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding
                                                {
                                                    Left = 20,
                                                    Right = DrawableRoomParticipantsList.SHEAR_WIDTH,
                                                    Vertical = 5
                                                },
                                                Children = new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Children = new Drawable[]
                                                        {
                                                            new FillFlowContainer
                                                            {
                                                                AutoSizeAxes = Axes.Both,
                                                                Direction = FillDirection.Horizontal,
                                                                Spacing = new Vector2(5),
                                                                Children = new Drawable[]
                                                                {
                                                                    new RoomStatusPill(Room)
                                                                    {
                                                                        Anchor = Anchor.CentreLeft,
                                                                        Origin = Anchor.CentreLeft
                                                                    },
                                                                    specialCategoryPill = new RoomSpecialCategoryPill(Room)
                                                                    {
                                                                        Anchor = Anchor.CentreLeft,
                                                                        Origin = Anchor.CentreLeft
                                                                    },
                                                                    new FreestyleStatusPill(Room)
                                                                    {
                                                                        Anchor = Anchor.CentreLeft,
                                                                        Origin = Anchor.CentreLeft
                                                                    },
                                                                    endDateInfo = new EndDateInfo(Room)
                                                                    {
                                                                        Anchor = Anchor.CentreLeft,
                                                                        Origin = Anchor.CentreLeft,
                                                                    },
                                                                }
                                                            },
                                                            new FillFlowContainer
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                                Padding = new MarginPadding { Top = 3 },
                                                                Direction = FillDirection.Vertical,
                                                                Children = new Drawable[]
                                                                {
                                                                    roomName = new TruncatingSpriteText
                                                                    {
                                                                        RelativeSizeAxes = Axes.X,
                                                                        Font = OsuFont.GetFont(size: 28)
                                                                    },
                                                                    new RoomStatusText(Room)
                                                                    {
                                                                        Beatmap = { BindTarget = currentBeatmap }
                                                                    }
                                                                }
                                                            }
                                                        },
                                                    },
                                                    new FillFlowContainer
                                                    {
                                                        Anchor = Anchor.BottomLeft,
                                                        Origin = Anchor.BottomLeft,
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(5),
                                                        ChildrenEnumerable = CreateBottomDetails()
                                                    }
                                                }
                                            },
                                            new FillFlowContainer
                                            {
                                                Name = "Right content",
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                AutoSizeAxes = Axes.X,
                                                RelativeSizeAxes = Axes.Y,
                                                Spacing = new Vector2(5),
                                                Padding = new MarginPadding
                                                {
                                                    Right = 10,
                                                    Vertical = 20,
                                                },
                                                Children = new Drawable[]
                                                {
                                                    ButtonsContainer,
                                                    drawableRoomParticipantsList = new DrawableRoomParticipantsList(Room)
                                                    {
                                                        Anchor = Anchor.CentreRight,
                                                        Origin = Anchor.CentreRight,
                                                        NumberOfCircles = NumberOfAvatars
                                                    }
                                                }
                                            },
                                        }
                                    }
                                },
                                passwordIcon = new PasswordProtectedIcon { Alpha = 0 }
                            },
                        },
                    }, 0)
                {
                    RelativeSizeAxes = Axes.Both,
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Room.PropertyChanged += onRoomPropertyChanged;

            wrapper.DelayedLoadComplete += _ =>
            {
                Debug.Assert(specialCategoryPill != null);
                Debug.Assert(endDateInfo != null);
                Debug.Assert(passwordIcon != null);

                wrapper.FadeInFromZero(200);

                updateRoomName();
                updateRoomCategory();
                updateRoomType();
                updateRoomHasPassword();
            };

            SelectedItem.BindValueChanged(onSelectedItemChanged, true);
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Room.Name):
                    updateRoomName();
                    break;

                case nameof(Room.Category):
                    updateRoomCategory();
                    break;

                case nameof(Room.Type):
                    updateRoomType();
                    break;

                case nameof(Room.HasPassword):
                    updateRoomHasPassword();
                    break;
            }
        }

        private void onSelectedItemChanged(ValueChangedEvent<PlaylistItem?> item)
        {
            if (item.NewValue?.Beatmap.OnlineID == item.OldValue?.Beatmap.OnlineID)
                return;

            beatmapLookupCancellation?.Cancel();
            beatmapLookupCancellation?.Dispose();
            beatmapLookupCancellation = null;

            if (item.NewValue?.Beatmap == null)
            {
                currentBeatmap.Value = null;
                return;
            }

            var cancellationSource = beatmapLookupCancellation = new CancellationTokenSource();

            beatmapLookupCache.GetBeatmapAsync(item.NewValue.Beatmap.OnlineID, cancellationSource.Token)
                              .ContinueWith(task => Schedule(() =>
                              {
                                  if (!cancellationSource.IsCancellationRequested)
                                      currentBeatmap.Value = task.GetResultSafely();
                              }), cancellationSource.Token);
        }

        private void updateRoomName()
        {
            if (roomName != null)
                roomName.Text = Room.Name;
        }

        private void updateRoomCategory()
        {
            if (Room.Category > RoomCategory.Normal)
                specialCategoryPill?.Show();
            else
                specialCategoryPill?.Hide();
        }

        private void updateRoomType()
        {
            if (endDateInfo != null)
                endDateInfo.Alpha = Room.Type == MatchType.Playlists ? 1 : 0;
        }

        private void updateRoomHasPassword()
        {
            if (passwordIcon != null)
                passwordIcon.Alpha = Room.HasPassword ? 1 : 0;
        }

        private int numberOfAvatars = 7;

        public int NumberOfAvatars
        {
            get => numberOfAvatars;
            set
            {
                numberOfAvatars = value;

                if (drawableRoomParticipantsList != null)
                    drawableRoomParticipantsList.NumberOfCircles = value;
            }
        }

        public virtual MenuItem[] ContextMenuItems
        {
            get
            {
                var items = new List<MenuItem>();

                if (Room.RoomID.HasValue)
                {
                    items.AddRange([
                        new OsuMenuItem("View in browser", MenuItemType.Standard, () => game?.OpenUrlExternally(formatRoomUrl(Room.RoomID.Value))),
                        new OsuMenuItem("Copy link", MenuItemType.Standard, () => game?.CopyToClipboard(formatRoomUrl(Room.RoomID.Value)))
                    ]);
                }

                return items.ToArray();

                string formatRoomUrl(long id) => $@"{api.Endpoints.WebsiteUrl}/multiplayer/rooms/{id}";
            }
        }

        protected virtual UpdateableBeatmapBackgroundSprite CreateBackground() => new UpdateableBeatmapBackgroundSprite();

        protected virtual IEnumerable<Drawable> CreateBottomDetails()
        {
            var pills = new List<Drawable>();

            if (Room.Type != MatchType.Playlists)
            {
                pills.AddRange(new Drawable[]
                {
                    new MatchTypePill(Room),
                    new QueueModePill(Room),
                });
            }

            pills.AddRange(new Drawable[]
            {
                new RoomStatusPill(Room)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft
                },
                specialCategoryPill = new RoomSpecialCategoryPill(Room)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft
                },
                endDateInfo = new EndDateInfo(Room)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new PlaylistCountPill(Room)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new StarRatingRangeDisplay(Room)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Scale = new Vector2(0.8f)
                }
            });

            return pills;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            Room.PropertyChanged -= onRoomPropertyChanged;
        }

        private partial class RoomStatusText : CompositeDrawable
        {
            public readonly Bindable<IBeatmapInfo?> Beatmap = new Bindable<IBeatmapInfo?>();

            public RoomStatusText(Room _)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize)
                    },
                };
            }
        }

        public partial class PasswordProtectedIcon : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                Anchor = Anchor.TopRight;
                Origin = Anchor.TopRight;

                Size = new Vector2(32);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopCentre,
                        Colour = colours.Gray5,
                        Rotation = 45,
                        RelativeSizeAxes = Axes.Both,
                        Width = 2,
                    },
                    new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.Lock,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding(6),
                        Size = new Vector2(14),
                    }
                };
            }
        }
    }
}
