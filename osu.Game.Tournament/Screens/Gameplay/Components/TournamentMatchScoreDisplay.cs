// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Screens.Play.HUD;
using osu.Game.Tournament.IPC;
using osu.Game.Tournament.Models;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    public partial class TournamentMatchScoreDisplay : MatchScoreDisplay
    {
        [BackgroundDependencyLoader]
        private void load(LegacyMatchIPCInfo legacyIpc, MatchIPCInfo lazerIpc, LadderInfo ladder)
        {
            ladder.UseLazerIpc.BindValueChanged(vce =>
            {
                Team1Score.UnbindAll();
                Team2Score.UnbindAll();

                if (vce.NewValue)
                {
                    Team1Score.BindTo(lazerIpc.Score1);
                    Team2Score.BindTo(lazerIpc.Score2);
                }
                else
                {
                    Team1Score.BindTo(legacyIpc.Score1);
                    Team2Score.BindTo(legacyIpc.Score2);
                }
            }, true);
        }
    }
}
