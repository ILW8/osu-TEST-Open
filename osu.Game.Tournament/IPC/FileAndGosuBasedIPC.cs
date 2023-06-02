// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Online.API;

namespace osu.Game.Tournament.IPC
{
    public partial class FileAndGosuBasedIPC : FileBasedIPC
    {
        private DateTime gosuRequestWaitUntil = DateTime.Now.AddSeconds(15); // allow 15 seconds for lazer to start and get ready
        private dynamic multipliers;
        private ScheduledDelegate scheduled;
        private ScheduledDelegate scheduledMultiplier;
        private GosuJsonRequest gosuJsonQueryRequest;

        public class GosuHasNameKey
        {
            [JsonProperty(@"name")]
            public string Name { get; set; } = "";
        }

        public class GosuIpcClientGameplay
        {
            [JsonProperty(@"score")]
            public int Score { get; set; }

            [JsonProperty(@"mods")]
            public GosuIpcClientMods Mods { get; set; }
        }

        public class GosuIpcClientMods
        {
            [JsonProperty(@"num")]
            public int Num { get; set; }

            [JsonProperty(@"str")]
            public string Str { get; set; }
        }

        public class GosuIpcClient
        {
            [JsonProperty(@"team")]
            public string Team { get; set; } = "";

            [JsonProperty(@"gameplay")]
            public GosuIpcClientGameplay Gameplay { get; set; }
        }

        public class GosuTourney
        {
            [JsonProperty(@"ipcClients")]
            public List<GosuIpcClient> IpcClients { get; set; }
        }

        public class GosuMenuBeatmap
        {
            [JsonProperty(@"id")]
            public int Id { get; set; }

            [JsonProperty(@"set")]
            public int Set { get; set; }
        }

        public class GosuMenu
        {
            [JsonProperty(@"bm")]
            public GosuMenuBeatmap Bm { get; set; }
        }

        public class GosuJson
        {
            [JsonProperty(@"gameplay")]
            public GosuHasNameKey GosuGameplay { get; set; }

            [JsonProperty(@"menu")]
            public GosuMenu GosuMenu { get; set; }

            [JsonProperty(@"resultsScreen")]
            public GosuHasNameKey GosuResultScreen { get; set; }

            [JsonProperty(@"tourney")]
            public GosuTourney GosuTourney { get; set; }
        }

        public class GosuJsonRequest : APIRequest<GosuJson>
        {
            protected override string Target => @"json";
            protected override string Uri => $@"http://localhost:24050/{Target}";

            protected override WebRequest CreateWebRequest()
            {
                // Thread.Sleep(500); // allow gosu to update json
                return new OsuJsonWebRequest<GosuJson>(Uri)
                {
                    AllowInsecureRequests = true,
                    Timeout = 200,
                };
            }
        }

        public class GosuMultipliersRequest : APIRequest<dynamic>
        {
            protected override string Target => @"multipliers.json";
            protected override string Uri => $@"http://localhost:24050/{Target}";

            protected override WebRequest CreateWebRequest()
            {
                return new OsuJsonWebRequest<dynamic>(Uri)
                {
                    AllowInsecureRequests = true,
                    Timeout = 1000,
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            scheduled?.Cancel();
            scheduledMultiplier?.Cancel();

            scheduledMultiplier = Scheduler.AddDelayed(delegate
            {
                if (multipliers != null)
                {
                    scheduledMultiplier?.Cancel();
                }
                GosuMultipliersRequest req = new GosuMultipliersRequest();
                req.Success += newMultipliers =>
                {
                    // IList<string> keys = multipliers.Properties().Select(p => p.Name).ToList();
                    // var a = newMultipliers.ContainsKey("19271241");
                    // var b = newMultipliers.ContainsKey("3455269");
                    Logger.Log("Loaded multipliers", LoggingTarget.Runtime, LogLevel.Important);
                    multipliers = newMultipliers;
                };
                req.Failure += exception =>
                {
                    Logger.Log($"Failed requesting multipliers data: {exception}", LoggingTarget.Runtime, LogLevel.Important);
                };
                API.Queue(req);
            }, 1000, true);

            scheduled = Scheduler.AddDelayed(delegate
            {
                Logger.Log("Executing gosu IPC scheduled delegate", LoggingTarget.Network, LogLevel.Debug);

                if (gosuRequestWaitUntil > DateTime.Now)
                {
                    return;
                }
                gosuJsonQueryRequest?.Cancel();
                gosuJsonQueryRequest = new GosuJsonRequest();
                gosuJsonQueryRequest.Success += gj =>
                {
                    Logger.Log("hi, entered success", LoggingTarget.Runtime, LogLevel.Important);

                    if (multipliers == null)
                    {
                        Logger.Log("multipliers not yet loaded, skipping...", LoggingTarget.Runtime, LogLevel.Important);
                        return;
                    }
                    // Score1WithMult.UnbindAll();
                    // Score2WithMult.UnbindAll();
                    // Replayer name can appear either in resultScreen.name or gameplay.name, depending on _when_ the API is queried.
                    string newVal = (gj.GosuGameplay?.Name?.Length > 0
                        ? gj.GosuGameplay.Name
                        : gj.GosuResultScreen?.Name) ?? "";

                    if (Replayer.Value == newVal) return; // not strictly necessary with a bindable

                    Logger.Log($"[IPC] Setting Replayer to {newVal}", LoggingTarget.Runtime, LogLevel.Debug);
                    Replayer.Value = newVal;

                    if (multipliers.ContainsKey(gj.GosuMenu.Bm.Id))
                    {
                        Logger.Log("map needs multiplier", LoggingTarget.Runtime, LogLevel.Important);
                    }
                };
                gosuJsonQueryRequest.Failure += exception =>
                {
                    Replayer.Value = "";
                    Logger.Log($"Failed requesting gosu data: {exception}", LoggingTarget.Runtime, LogLevel.Important);
                    // Score1WithMult.BindTo(Score1);
                    // Score2WithMult.BindTo(Score2);
                    gosuRequestWaitUntil = DateTime.Now.AddSeconds(2); // inhibit calling gosu api again for 2 seconds if failure occured
                };
                API.Queue(gosuJsonQueryRequest);
            }, 250, true);
        }
    }
}
