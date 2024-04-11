// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;

namespace osu.Game.Updater
{
    /// <summary>
    /// An update manager that shows notifications if a newer release is detected.
    /// Installation is left up to the user.
    /// </summary>
    public partial class SimpleUpdateManager : UpdateManager
    {
        protected override async Task<bool> PerformUpdateCheck()
        {
            await Task.Delay(10).ConfigureAwait(false);
            return false;
        }
    }
}
