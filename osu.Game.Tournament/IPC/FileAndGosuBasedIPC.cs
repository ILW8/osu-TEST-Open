// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading;
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
        private ScheduledDelegate scheduled;
        private GosuJsonRequest gosuReplayerLookupRequest;

        public class GosuHasNameKey
        {
            [JsonProperty(@"name")]
            public string Name { get; set; } = "";
        }

        public class GosuJson
        {
            [JsonProperty(@"gameplay")]
            public GosuHasNameKey GosuGameplay { get; set; }

            [JsonProperty(@"resultsScreen")]
            public GosuHasNameKey GosuResultScreen { get; set; }
        }

        public class GosuJsonRequest : APIRequest<GosuJson>
        {
            protected override string Target => @"json";
            protected override string Uri => $@"http://localhost:24050/{Target}";

            protected override WebRequest CreateWebRequest()
            {
                Thread.Sleep(500); // allow gosu to update json
                return new OsuJsonWebRequest<GosuJson>(Uri)
                {
                    AllowInsecureRequests = true,
                    Timeout = 200,
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            scheduled?.Cancel();

            scheduled = Scheduler.AddDelayed(delegate
            {
                Logger.Log("Executing gosu IPC scheduled delegate", LoggingTarget.Network, LogLevel.Debug);
                gosuReplayerLookupRequest?.Cancel();
                gosuReplayerLookupRequest = new GosuJsonRequest();
                gosuReplayerLookupRequest.Success += gj =>
                {
                    // Replayer name can appear either in resultScreen.name or gameplay.name, depending on _when_ the API is queried.
                    string newVal = (gj.GosuGameplay?.Name?.Length > 0
                        ? gj.GosuGameplay.Name
                        : gj.GosuResultScreen?.Name) ?? "";

                    if (Replayer.Value == newVal) return; // not strictly necessary with a bindable

                    Logger.Log($"[IPC] Setting Replayer to {newVal}", LoggingTarget.Runtime, LogLevel.Debug);
                    Replayer.Value = newVal;
                };
                gosuReplayerLookupRequest.Failure += exception =>
                {
                    Replayer.Value = "";
                    Logger.Log($"Failed requesting gosu data: {exception}", LoggingTarget.Runtime, LogLevel.Debug);
                };
                API.Queue(gosuReplayerLookupRequest);
            }, 1000, true);
        }
    }
}
