using TwitchLib.Api;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.Channels.GetChannelInformation;
using TwitchLib.Api.Helix.Models.Search;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Teams;
using TwitchLib.Api.Helix.Models.Users.GetUserFollows;
using TwitchChannel = TwitchLib.Api.Helix.Models.Search.Channel;
using TwitchStream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Nicrobot.Twitch
{
    public class TwitchService : IHostedService
    {
        private readonly TwitchAPI _api;
        private readonly ILogger<TwitchService> _logger;
        private readonly Helix _helix;

        public TwitchService(
            IConfiguration config,
            ILogger<TwitchService> logger,
            ILoggerFactory loggerFactory )
        {
            _logger = logger;

            _logger.LogInformation("Fetching Twitch configuration...");

            string clientId = config.GetValue<string>(Constants.TWITCH_CLIENT_ID);
            string appSecret = config.GetValue<string>(Constants.TWITCH_APP_SECRET);

            ArgumentNullException.ThrowIfNull(clientId);
            ArgumentNullException.ThrowIfNull(appSecret);

            _api = new TwitchAPI(loggerFactory: loggerFactory);
            _api.Settings.ClientId = clientId;
            _api.Settings.Secret = appSecret;

            _helix = _api.Helix;
        }
        
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to get Twitch access token...");
            string accessToken = await _api.Auth.GetAccessTokenAsync();

            ArgumentNullException.ThrowIfNull(accessToken);

            _logger.LogInformation("Successfully fetched Twitch access token.");
            _api.Settings.AccessToken = accessToken;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<IEnumerable<OnlineChannel>> GetFollowedOnlineChannels(string channelId)
        {
            int TAKE = 50;
            int start = 0;

            List<OnlineChannel> followedOnline = new();
            GetUsersFollowsResponse follows = await _helix.Users
                .GetUsersFollowsAsync(
                    fromId: channelId,
                    first: TAKE);

            // Paginate the users this user follows
            while (start < follows.TotalFollows)
            {
                // Add the list of follows
                List<string> followedIds = follows.Follows.Select(f => f.ToUserId).ToList();

                GetStreamsResponse streams = await _helix.Streams
                    .GetStreamsAsync(
                        userIds: followedIds,
                        first: TAKE);

                foreach (TwitchStream? stream in streams.Streams)
                {
                    followedOnline.Add(new OnlineChannel
                    {
                        ID = stream.Id,
                        Name = stream.UserName,
                        Game = stream.GameName,
                        Viewers = stream.ViewerCount,
                        StartedAt = stream.StartedAt
                    });
                }

                // Get more
                start += TAKE;
                follows = await _helix.Users
                    .GetUsersFollowsAsync(
                        fromId: channelId,
                        after: follows.Pagination.Cursor,
                        first: TAKE);
            }

            return followedOnline.OrderByDescending(o => o.Viewers);
        }

        public async Task<IEnumerable<StreamerTeam>> GetTeamsAsync(
            string broadcasterId, bool includeMembers = false)
        {
            List<StreamerTeam> list = new();

            // Get the teams this streamer is a member of
            GetChannelTeamsResponse teams = await _helix.Teams.GetChannelTeamsAsync(broadcasterId);

            if (teams?.ChannelTeams != null)
            {
                foreach (ChannelTeam? channelTeam in teams.ChannelTeams)
                {
                    StreamerTeam team = new StreamerTeam()
                    {
                        ID = channelTeam.Id,
                        Name = channelTeam.TeamDisplayName
                    };

                    if (includeMembers)
                    {
                        team.Streamers = (await GetTeam(channelTeam.Id)).Streamers;
                    }

                    list.Add(team);
                }
            }

            return list.OrderBy(t => t.Name);
        }

        public async Task<StreamerTeam> GetTeam(string teamId)
        {
            StreamerTeam streamerTeam = new();

            // The GetChannelTeamsResponse doesn't include users, so fetch the entire team object
            GetTeamsResponse teamsResponse = await _helix.Teams.GetTeamsAsync(teamId);
            Team? team = teamsResponse.Teams.FirstOrDefault(t => t.Id == teamId);

            if (team is not null)
            {
                streamerTeam.ID = teamId;
                streamerTeam.Name = team.TeamDisplayName;
                
                // Get the list of streamers in this team
                List<string> teamMemberIds = new();
                foreach (TeamMember? teamMember in team.Users)
                {
                    teamMemberIds.Add(teamMember.UserId);
                }

                // Get the streamers currently online for this team
                GetStreamsResponse streams = await _helix.Streams.GetStreamsAsync(userIds: teamMemberIds.Take(50).ToList());

                foreach (TwitchStream? stream in streams.Streams)
                {
                    streamerTeam.Streamers.Add(new()
                    {
                        ID = stream.Id,
                        Name = stream.UserName,
                        Game = stream.GameName,
                        Viewers = stream.ViewerCount,
                        StartedAt = stream.StartedAt
                    });
                }
            }
            else
            {
                throw new KeyNotFoundException($"The team with team ID '{teamId}' couldn't be found.");
            }

            return streamerTeam;
        }

        public async Task<Channel> GetChannelFromNameAsync(string channelName)
        {
            SearchChannelsResponse channels = await _helix.Search.SearchChannelsAsync(channelName);
            TwitchChannel? channel = channels.Channels.FirstOrDefault(c => c.DisplayName.ToLower() == channelName.ToLower());

            if (channel is not null)
            {
                return new Channel
                {
                    ID = channel.Id,
                    Name = channel.DisplayName
                };
            }
            else
            {
                throw new TwitchChannelNotFoundException(channelName);
            }
        }

        public async Task<Channel> GetChannelAsync(string broadcasterId)
        {
            GetChannelInformationResponse info = await _helix.Channels.GetChannelInformationAsync(broadcasterId);
            ChannelInformation? channel = info.Data.FirstOrDefault(c => c.BroadcasterId == broadcasterId);

            if (channel is not null)
            {
                return new Channel
                {
                    ID = channel.BroadcasterId,
                    Name = channel.BroadcasterName
                };
            }
            else
            {
                throw new TwitchChannelNotFoundException(broadcasterId);
            }
        }
    }
}
