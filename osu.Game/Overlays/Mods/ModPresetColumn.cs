// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osuTK;
using Realms;

namespace osu.Game.Overlays.Mods
{
    public partial class ModPresetColumn<T> : ModSelectColumn
        where T : RealmObject, IModPreset, new()
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        private const float contracted_width = WIDTH - 120;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AccentColour = colours.Orange1;
            HeaderText = ModSelectOverlayStrings.PersonalPresets;

            AddPresetButton<T> addPresetButton;
            ItemsFlow.Add(addPresetButton = new AddPresetButton<T>());
            ItemsFlow.SetLayoutPosition(addPresetButton, float.PositiveInfinity);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => rulesetChanged(), true);

            Width = contracted_width;
        }

        private IDisposable? presetSubscription;

        // actually just a real entrypoit to modpresetcolumn
        private void rulesetChanged()
        {
            presetSubscription?.Dispose();
            presetSubscription = realm.RegisterForNotifications(r =>
                r.All<T>()
                 .Filter($"{nameof(IModPreset.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0"
                         + $" && {nameof(IModPreset.DeletePending)} == false", ruleset.Value.ShortName)
                 .OrderBy(preset => preset.Name), asyncLoadPanels);
        }

        private CancellationTokenSource? cancellationTokenSource;

        private Task? latestLoadTask;
        internal bool ItemsLoaded => latestLoadTask?.IsCompleted == true;

        private void asyncLoadPanels(IRealmCollection<T> presets, ChangeSet? changes)
        {
            cancellationTokenSource?.Cancel();

            bool hasPresets = presets.Any();

            this.ResizeWidthTo(hasPresets ? WIDTH : contracted_width, 200, Easing.OutQuint);

            if (!hasPresets)
            {
                removeAndDisposePresetPanels();
                return;
            }

            latestLoadTask = LoadComponentsAsync(presets.Select(p => new ModPresetPanel<T>(p.ToLive(realm))
            {
                Shear = Vector2.Zero
            }), loaded =>
            {
                removeAndDisposePresetPanels();
                ItemsFlow.AddRange(loaded);
            }, (cancellationTokenSource = new CancellationTokenSource()).Token);

            void removeAndDisposePresetPanels()
            {
                foreach (var panel in ItemsFlow.OfType<ModPresetPanel<T>>().ToArray())
                    panel.RemoveAndDisposeImmediately();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            presetSubscription?.Dispose();
        }
    }
}
