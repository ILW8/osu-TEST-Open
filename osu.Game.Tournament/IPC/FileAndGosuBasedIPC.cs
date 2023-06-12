// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.IO.Network;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;

namespace osu.Game.Tournament.IPC
{
    public partial class FileAndGosuBasedIPC : FileBasedIPC
    {
        private DateTime gosuRequestWaitUntil = DateTime.Now.AddSeconds(15); // allow 15 seconds for lazer to start and get ready
        private dynamic multipliers;
        private List<MappoolShowcaseMap> maps = new List<MappoolShowcaseMap>();
        private ScheduledDelegate scheduled;
        private ScheduledDelegate scheduledMultiplier;
        private ScheduledDelegate scheduledShowcase;
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

        public class GosuIpcClientSpectating
        {
            [JsonProperty(@"name")]
            public string Name { get; set; }

            [JsonProperty(@"country")]
            public string Country { get; set; }

            [JsonProperty(@"userID")]
            public string UserID { get; set; }
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

            [JsonProperty(@"spectating")]
            public GosuIpcClientSpectating Spectating { get; set; }
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

            [JsonProperty(@"md5")]
            public string MD5 { get; set; }

            [JsonProperty(@"set")]
            public int Set { get; set; }
        }

        public class GosuMenu
        {
            [JsonProperty(@"bm")]
            public GosuMenuBeatmap Bm { get; set; }
        }

        public class MappoolShowcaseMap
        {
            [JsonProperty(@"id")]
            public int Id { get; set; }

            [JsonProperty(@"md5")]
            public string MD5 { get; set; }

            [JsonProperty(@"slot")]
            public string Slot { get; set; }
        }

        public class MappoolShowcaseData
        {
            [JsonProperty(@"maps")]
            public List<MappoolShowcaseMap> Maps { get; set; }
        }

        public class GosuJson
        {
            [JsonProperty(@"error")]
            public string GosuError { get; set; }

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

        public int ModStringToInt(string modString)
        {
            switch (modString)
            {
                case "HD": return (int)LegacyMods.Hidden;

                case "HR": return (int)LegacyMods.HardRock;

                case "EZ": return (int)LegacyMods.Easy;

                case "FL": return (int)LegacyMods.Flashlight;

                case "NF": return (int)LegacyMods.NoFail;

                default: return 0;
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

        public class GosuMappoolShowcaseRequest : APIRequest<MappoolShowcaseData>
        {
            protected override string Target => @"showcase.json";
            protected override string Uri => $@"http://localhost:24050/{Target}";

            protected override WebRequest CreateWebRequest()
            {
                return new OsuJsonWebRequest<MappoolShowcaseData>(Uri)
                {
                    AllowInsecureRequests = true,
                    Timeout = 2000,
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            scheduled?.Cancel();
            scheduledMultiplier?.Cancel();
            scheduledShowcase?.Cancel();

            scheduledMultiplier = Scheduler.AddDelayed(delegate
            {
                // if (multipliers != null)
                // {
                //     scheduledMultiplier?.Cancel();
                // }
                GosuMultipliersRequest req = new GosuMultipliersRequest();
                req.Success += newMultipliers =>
                {
                    if (JToken.DeepEquals(multipliers, newMultipliers)) return;

                    Logger.Log("Loaded/updated multipliers", LoggingTarget.Runtime, LogLevel.Important);
                    multipliers = newMultipliers;

                };
                req.Failure += exception =>
                {
                    Logger.Log($"Failed requesting multipliers data: {exception}", LoggingTarget.Runtime, LogLevel.Important);
                };
                API.Queue(req);
            }, 1000, true);

            scheduledShowcase = Scheduler.AddDelayed(delegate
            {
                GosuMappoolShowcaseRequest req = new GosuMappoolShowcaseRequest();
                req.Success += newMappoolData =>
                {
                    // Logger.Log("hey", LoggingTarget.Runtime, LogLevel.Important);
                    // foreach (var map in newMappoolData.Maps)
                    // {
                        // Logger.Log(map.Slot, LoggingTarget.Runtime, LogLevel.Important);
                    // }
                    // Logger.Log(newMappoolData.Maps);
                    maps = newMappoolData.Maps;
                };
                req.Failure += exception =>
                {
                    Logger.Log($"Failed requesting mappool data: {exception}", LoggingTarget.Runtime, LogLevel.Important);
                };
                API.Queue(req);
            }, 5000, true);

            scheduled = Scheduler.AddDelayed(delegate
            {
                Logger.Log("Executing gosu IPC scheduled delegate", LoggingTarget.Network, LogLevel.Debug);

                if (gosuRequestWaitUntil > DateTime.Now) // request inhibited
                {
                    Score1WithMult.Value = -1;
                    Score2WithMult.Value = -1;
                    return;
                }
                gosuJsonQueryRequest?.Cancel();
                gosuJsonQueryRequest = new GosuJsonRequest();
                gosuJsonQueryRequest.Success += gj =>
                {
                    if (gj == null)
                    {
                        Logger.Log($"[Warning] failed to parse gosumemory json", LoggingTarget.Runtime, LogLevel.Important);
                        return;
                    }

                    if (gj.GosuError != null)
                    {
                        Logger.Log($"[Warning] gosumemory reported an error: {gj.GosuError}", LoggingTarget.Runtime, LogLevel.Important);
                        return;
                    }

                    UpdateScore(gj);

                    // =====
                    // set replayer
                    // Replayer name can appear either in resultScreen.name or gameplay.name, depending on _when_ the API is queried.
                    string newVal = (gj.GosuGameplay?.Name?.Length > 0
                        ? gj.GosuGameplay.Name
                        : gj.GosuResultScreen?.Name) ?? "";

                    if (Replayer.Value == newVal) return; // not strictly necessary with a bindable

                    Logger.Log($"[IPC] Setting Replayer to {newVal}", LoggingTarget.Runtime, LogLevel.Debug);
                    Replayer.Value = newVal;

                    // ====
                    // set slot
                    foreach (var map in maps)
                    {
                        if (gj.GosuMenu.Bm.Id != map.Id && gj.GosuMenu.Bm.MD5 != map.MD5) continue;

                        // Logger.Log("we hit a match!", LoggingTarget.Runtime, LogLevel.Important);

                        if (Slot.Value == map.Slot) return;

                        Slot.Value = map.Slot;
                        break;
                    }
                };
                gosuJsonQueryRequest.Failure += exception =>
                {
                    Logger.Log($"Failed requesting gosu data: {exception}", LoggingTarget.Runtime, LogLevel.Important);
                    gosuRequestWaitUntil = DateTime.Now.AddSeconds(2); // inhibit calling gosu api again for 2 seconds if failure occured
                    Score1WithMult.Value = -1;
                    Score2WithMult.Value = -1;
                    Replayer.Value = "";
                };
                API.Queue(gosuJsonQueryRequest);
            }, 250, true);
        }
        private void UpdateScore(GosuJson gj)
        {
            if (multipliers == null)
            {
                Logger.Log("multipliers not yet loaded, skipping...", LoggingTarget.Runtime, LogLevel.Important);
                gosuRequestWaitUntil = DateTime.Now.AddSeconds(1); // inhibit score fetching until multipliers are updated
                return;
            }

            bool shouldUseMult = false;

            List<int> left = new List<int>();
            List<int> right = new List<int>();
            int ipcClientIndex = 0;

            foreach (GosuIpcClient ipcClient in gj.GosuTourney.IpcClients ?? new List<GosuIpcClient>())
            {
                // Logger.Log($"{ipcClient.Team}: {ipcClient.Gameplay.Score}".PadLeft(7), LoggingTarget.Runtime, LogLevel.Important);

                float scoreMultiplier = 1.0f;
                float scoreMultiplierExclusive = 1.0f;
                int modsMatched = 0;

                if (multipliers.ContainsKey(gj.GosuMenu.Bm.Id.ToString()))
                {
                    foreach (dynamic x in (JObject)multipliers[gj.GosuMenu.Bm.Id.ToString()])
                    {
                        // Logger.Log($"{x.Key} ({ModStringToInt(x.Key)}): {x.Value["mult"]}x (exclusive: {x.Value["exclusive"]})", LoggingTarget.Runtime, LogLevel.Important);
                        int modInt = ModStringToInt(x.Key);

                        if ((modInt & ipcClient.Gameplay.Mods.Num & -2) <= 0) continue; // check if mod match, ignore no fail

                        modsMatched++;
                        float mult = (float)x.Value["mult"];
                        bool isExclusive = (bool)x.Value["exclusive"];

                        if (isExclusive && mult > scoreMultiplierExclusive)
                        {
                            scoreMultiplierExclusive = mult;
                        }
                        else
                        {
                            scoreMultiplier = mult > scoreMultiplier ? mult : scoreMultiplier;
                        }

                    }

                    // use exclusive multiplier if only one mod found. Otherwise use highest non-exclusive mult
                    scoreMultiplier = modsMatched == 1 && scoreMultiplierExclusive > scoreMultiplier ? scoreMultiplierExclusive : scoreMultiplier;

                    if (scoreMultiplier > 1.0f)
                    {
                        Logger.Log($"{modsMatched} mods matched for {ipcClient.Spectating.Name}", LoggingTarget.Runtime, LogLevel.Important);
                        Logger.Log($"({gj.GosuMenu.Bm.Id.ToString()}) applying {scoreMultiplier} multiplier to {ipcClient.Spectating.Name}", LoggingTarget.Runtime, LogLevel.Important);
                        shouldUseMult = true;
                    }
                }

                bool theConditional = ipcClientIndex < gj.GosuTourney.IpcClients.Count / 2;
                Logger.Log($"isLeft: {theConditional}: {ipcClient.Gameplay.Score * scoreMultiplier}");
                (theConditional ? left : right).Add((int)(ipcClient.Gameplay.Score * scoreMultiplier));
                ipcClientIndex++;
            }

            if (Score1WithMult.Value == left.Sum() && Score2WithMult.Value == right.Sum()) return;

            Score1WithMult.Value = left.Sum();
            Score2WithMult.Value = right.Sum();

            if (ShouldUseMult.Value != shouldUseMult) ShouldUseMult.Value = shouldUseMult;
        }
    }
}
