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
        private DateTime gosuRequestWaitUntil = DateTime.Now.AddSeconds(5); // allow 15 seconds for lazer to start and get ready
        private List<MappoolShowcaseMap> maps = new List<MappoolShowcaseMap>();
        private ScheduledDelegate scheduled;
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

            [JsonProperty(@"accuracy")]
            public float Accuracy { get; set; }

            [JsonProperty(@"hits")]
            public GosuIpcClientHits Hits { get; set; }
        }

        public class GosuIpcClientHits
        {
            [JsonProperty(@"0")]
            public int MissCount { get; set; }

            [JsonProperty(@"sliderBreaks")]
            public int SliderBreaks { get; set; }
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
            scheduledShowcase?.Cancel();

            Accuracy1.BindValueChanged(_ => Logger.Log($"acc left: {Accuracy1.Value} | acc right: {Accuracy2.Value}", LoggingTarget.Runtime, LogLevel.Important));
            Accuracy2.BindValueChanged(_ => Logger.Log($"acc left: {Accuracy1.Value} | acc right: {Accuracy2.Value}", LoggingTarget.Runtime, LogLevel.Important));

            scheduledShowcase = Scheduler.AddDelayed(delegate
            {
                if (!API.IsLoggedIn)
                {
                    return;
                }

                GosuMappoolShowcaseRequest req = new GosuMappoolShowcaseRequest();
                req.Success += newMappoolData =>
                {
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
                // Logger.Log("Executing gosu IPC scheduled delegate", LoggingTarget.Network, LogLevel.Debug);

                if (!API.IsLoggedIn || gosuRequestWaitUntil > DateTime.Now) // request inhibited
                {
                    Accuracy1.Value = 0f;
                    Accuracy2.Value = 0f;
                    MissCount1.Value = 0;
                    MissCount2.Value = 0;
                    return;
                }

                gosuJsonQueryRequest?.Cancel();
                gosuJsonQueryRequest = new GosuJsonRequest();
                gosuJsonQueryRequest.Success += gj =>
                {
                    if (gj == null)
                    {
                        Logger.Log("[Warning] failed to parse gosumemory json", LoggingTarget.Runtime, LogLevel.Important);
                        return;
                    }

                    if (gj.GosuError != null)
                    {
                        Logger.Log($"[Warning] gosumemory reported an error: {gj.GosuError}", LoggingTarget.Runtime, LogLevel.Important);
                        return;
                    }

                    // Logger.Log($"aaaa: {gj.GosuTourney.IpcClients[0].Gameplay.Accuracy}", LoggingTarget.Runtime, LogLevel.Important);

                    updateScore(gj);

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
                    Accuracy1.Value = 0f;
                    Accuracy2.Value = 0f;
                    MissCount1.Value = 0;
                    MissCount2.Value = 0;
                    Replayer.Value = "";
                };
                API.Queue(gosuJsonQueryRequest);
            }, 250, true);
        }

        private void updateScore(GosuJson gj)
        {
            int ipcIndex = 0;

            foreach (GosuIpcClient ipcClient in gj.GosuTourney.IpcClients ?? new List<GosuIpcClient>()) // assumes there are only two clients
            {
                // Logger.Log($"({ipcIndex}) {ipcClient.Gameplay.Hits.MissCount}: {ipcClient.Gameplay.Accuracy}", LoggingTarget.Runtime, LogLevel.Important);
                (ipcIndex % 2 == 0 ? MissCount1 : MissCount2).Value = ipcClient.Gameplay.Hits.MissCount;
                (ipcIndex % 2 == 0 ? Accuracy1 : Accuracy2).Value = ipcClient.Gameplay.Accuracy;
                ipcIndex++;
                if (ipcIndex == 2) break; // ermmmmmmmmmmmmmmmmmmmmmmmmmmmm
            }
        }
    }
}
