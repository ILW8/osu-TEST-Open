// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.Mods
{
    public partial class DeleteModPresetDialog<T> : DangerousActionDialog
        where T : class, IModPreset
    {
        public DeleteModPresetDialog(Live<T> modPreset)
        {
            BodyText = modPreset.PerformRead(preset => preset.Name);
            DangerousAction = () => modPreset.PerformWrite(preset => preset.DeletePending = true);
        }
    }
}
