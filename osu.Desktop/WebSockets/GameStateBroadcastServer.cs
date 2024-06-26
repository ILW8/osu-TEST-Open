// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Online.Broadcasts;

namespace osu.Desktop.WebSockets
{
    [Cached(typeof(IGameStateBroadcastServer))]
    public partial class GameStateBroadcastServer : WebSocketServer, IGameStateBroadcastServer
    {
        public override string Endpoint => @"state";

        private Bindable<bool> enabled = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.BroadcastGameState);
            enabled.BindValueChanged(handleEnableStateChange, true);
        }

        private void handleEnableStateChange(ValueChangedEvent<bool> e)
        {
            enabled.Disabled = true;

            if (e.NewValue)
            {
                Task.Run(() => Start()).ContinueWith(t => enabled.Disabled = false);
            }
            else
            {
                Task.Run(() => Close()).ContinueWith(t => enabled.Disabled = false);
            }
        }

        public void Add(GameStateBroadcaster broadcaster)
            => AddInternal(broadcaster);

        public void AddRange(IEnumerable<GameStateBroadcaster> broadcasters)
            => AddRangeInternal(broadcasters);

        public void Remove(GameStateBroadcaster broadcaster)
            => RemoveInternal(broadcaster, true);

        protected override void OnConnectionReady(WebSocketConnection connection)
        {
            var broadcasters = InternalChildren.OfType<GameStateBroadcaster>();

            foreach (var broadcaster in broadcasters)
                broadcaster.Broadcast();
        }
    }
}
