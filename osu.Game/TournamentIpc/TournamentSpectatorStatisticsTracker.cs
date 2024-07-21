// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;

namespace osu.Game.TournamentIpc
{
    [LongRunningLoad]
    public partial class TournamentSpectatorStatisticsTracker : CompositeDrawable
    {
        private readonly MultiplayerRoomUser[] playingUsers;

        protected readonly Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData> UserScores = new Dictionary<int, MultiplayerGameplayLeaderboard.TrackedUserData>();

        public readonly SortedDictionary<int, BindableLong> TeamScores = new SortedDictionary<int, BindableLong>();

        private bool hasTeams => TeamScores.Count > 0;

        [Resolved]
        private SpectatorClient spectatorClient { get; set; } = null!;

        [Resolved(canBeNull: true)]
        protected TournamentFileBasedIPC? TournamentIpc { get; private set; }

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private UserLookupCache userLookupCache { get; set; } = null!;

        private SpectatorScoreProcessor scoreProcessor = null!;

        private Bindable<ScoringMode> scoringMode = null!;

        public TournamentSpectatorStatisticsTracker(MultiplayerRoomUser[] users)
        {
            Logger.Log($"Created new {nameof(TournamentSpectatorStatisticsTracker)}");
            playingUsers = users;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IAPIProvider api, CancellationToken cancellationToken)
        {
            scoringMode = config.GetBindable<ScoringMode>(OsuSetting.ScoreDisplayMode);

            foreach (var user in playingUsers)
            {
                scoreProcessor = new SpectatorScoreProcessor(user.UserID);
                scoreProcessor.Mode.BindTo(scoringMode);
                scoreProcessor.TotalScore.BindValueChanged(_ => Scheduler.AddOnce(updateTotals));
                AddInternal(scoreProcessor);

                var trackedUser = new MultiplayerGameplayLeaderboard.TrackedUserData(user, scoreProcessor);
                UserScores[user.UserID] = trackedUser;

                if (trackedUser.Team is int team && !TeamScores.ContainsKey(team))
                {
                    var teamScoreBindable = new BindableLong();
                    TeamScores.Add(team, teamScoreBindable);

                    // teamScoreBindable.BindValueChanged(vce =>
                    // {
                    //     Logger.Log($"team {team} has new score: {vce.NewValue}");
                    // });
                }
            }

            TournamentIpc?.UpdateTeamScores(TeamScores.Values.Select(bindableLong => bindableLong.Value).ToArray());

            // userLookupCache.GetUsersAsync(playingUsers.Select(u => u.UserID).ToArray(), cancellationToken)
            // .ContinueWith(task =>
            // {
            //     Schedule(() =>
            //     {
            //         var users = task.GetResultSafely();
            //
            //         for (int i = 0; i < users.Length; i++)
            //         {
            //             var user = users[i] ?? new APIUser
            //             {
            //                 Id = playingUsers[i].UserID,
            //                 Username = "Unknown user",
            //             };
            //
            //             var trackedUser = UserScores[user.Id];
            //
            //             var leaderboardScore = Add(user, user.Id == api.LocalUser.Value.Id);
            //             leaderboardScore.GetDisplayScore = trackedUser.ScoreProcessor.GetDisplayScore;
            //             leaderboardScore.Accuracy.BindTo(trackedUser.ScoreProcessor.Accuracy);
            //             leaderboardScore.TotalScore.BindTo(trackedUser.ScoreProcessor.TotalScore);
            //             leaderboardScore.Combo.BindTo(trackedUser.ScoreProcessor.Combo);
            //             leaderboardScore.HasQuit.BindTo(trackedUser.UserQuit);
            //         }
            //     });
            // }, cancellationToken);
        }

        private void updateTotals()
        {
            if (!hasTeams)
                return;

            var teamScores = new Dictionary<int, long>();

            foreach (var u in UserScores.Values)
            {
                if (u.Team == null)
                    continue;

                if (teamScores.ContainsKey(u.Team.Value))
                {
                    teamScores[u.Team.Value] += u.ScoreProcessor.TotalScore.Value;
                }
                else
                {
                    teamScores[u.Team.Value] = u.ScoreProcessor.TotalScore.Value;
                }
            }

            foreach (var teamScore in teamScores)
            {
                TeamScores[teamScore.Key].Value = teamScore.Value;
            }

            TournamentIpc?.UpdateTeamScores(teamScores.OrderBy(score => score.Key)
                                                      .Select(score => score.Value)
                                                      .ToArray());
        }

        public void AddClock(int userId, IClock clock)
        {
            if (!UserScores.TryGetValue(userId, out var data))
                throw new ArgumentException(@"Provided user is not tracked by this leaderboard", nameof(userId));

            data.ScoreProcessor.ReferenceClock = clock;
        }

        protected override void Update()
        {
            base.Update();

            foreach (var (_, data) in UserScores)
                data.ScoreProcessor.UpdateScore();
        }
    }
}
