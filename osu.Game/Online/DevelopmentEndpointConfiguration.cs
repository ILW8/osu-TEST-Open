// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    public class DevelopmentEndpointConfiguration : EndpointConfiguration
    {
        public DevelopmentEndpointConfiguration()
        {
            WebsiteRootUrl = APIEndpointUrl = @"http://10.24.0.76:8080";
            const string spectator_server_root_url = @"http://10.24.0.76:8081";
            APIClientSecret = @"0SPDK2ePwhxo9lC0KOdCKTMm1vtCVDWYuRLcXhob";
            APIClientID = "1";
            SpectatorEndpointUrl = $@"{spectator_server_root_url}/spectator";
            MultiplayerEndpointUrl = $@"{spectator_server_root_url}/multiplayer";
            MetadataEndpointUrl = $@"{spectator_server_root_url}/metadata";
        }
    }
}
