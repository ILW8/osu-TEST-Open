// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Drawing;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Match
{
    public partial class MultiplayerMatchFooter : CompositeDrawable
    {
        private const float ready_button_width = 600;
        private const float spectate_button_width = 200;

        private readonly OsuNumberBox numberBox;
        private readonly OsuButton setResolutionButton;

        private const int minimum_window_height = 720;
        private const int maximum_window_height = 2160;
        private const float aspect_ratio = 1920f / 720f;
        private const float height_reduction_ratio = 720f / 1080f; // active area height is 720 for a 1080p stream
        private const int stream_area_width = 1366;

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            var windowSize = frameworkConfig.GetBindable<Size>(FrameworkSetting.WindowedSize);

            setResolutionButton.Action = () =>
            {
                if (string.IsNullOrEmpty(numberBox.Text))
                    return;

                // box contains text
                if (!int.TryParse(numberBox.Text, out int height))
                {
                    // at this point, the only reason we can arrive here is if the input number was too big to parse into an int
                    // so clamp to max allowed value
                    height = maximum_window_height;
                }
                else
                {
                    height = Math.Clamp(height, minimum_window_height, maximum_window_height);
                }

                // in case number got clamped, reset number in numberBox
                numberBox.Text = height.ToString();

                height = (int)(height_reduction_ratio * height);
                windowSize.Value = new Size((int)(height * aspect_ratio), height);
            };
        }

        public MultiplayerMatchFooter()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Content = new[]
                {
                    new Drawable[]
                    {
                        null,
                        numberBox = new OsuNumberBox
                        {
                            Text = "1080",
                            RelativeSizeAxes = Axes.Both,
                            Size = Vector2.One
                        },
                        null,
                        setResolutionButton = new RoundedButton
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = Vector2.One,
                            Text = "Set stream height",
                        },
                        new MultiplayerSpectateButton
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        new MatchStartControl
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                        null
                    }
                },
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(maxSize: spectate_button_width),
                    new Dimension(GridSizeMode.Absolute, 5),
                    new Dimension(maxSize: ready_button_width),
                    new Dimension()
                }
            };
        }
    }
}
