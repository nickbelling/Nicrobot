using Discord;
using Discord.Interactions;
using Humanizer;
using Nicrobot.Twitch;
using System.Text;

namespace Nicrobot.Discord.Modules
{
    [Group("twitch", "Twitch Commands")]
    public class TwitchModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly TwitchService _twitch;
        private readonly ILogger<TwitchModule> _logger;

        public TwitchModule(
            TwitchService twitch,
            ILogger<TwitchModule> logger)
        {
            _twitch = twitch;
            _logger = logger;
        }

        [SlashCommand("raid",
            "Give me a channel, and I'll get you some ideas for who to raid.",
            ignoreGroupNames: true,
            runMode: RunMode.Async)]
        public async Task GetRaidOptions(string channelName)
        {
            await DeferAsync(ephemeral: true);

            try
            {
                Channel channel = await _twitch.GetChannelFromNameAsync(channelName);
                StringBuilder sb = new("I'll help you choose someone to raid!");
                sb.AppendLine();

                List<ButtonBuilder> buttons = new()
                {
                    new ButtonBuilder()
                        .WithLabel($"{channel.Name}'s followed")
                        .WithCustomId($"followers:{channel.ID}")
                        .WithStyle(ButtonStyle.Primary)
                };

                foreach (StreamerTeam team in await _twitch.GetTeamsAsync(channel.ID))
                {
                    buttons.Add(new ButtonBuilder()
                        .WithLabel(team.Name)
                        .WithCustomId($"group:{team.ID}")
                        .WithStyle(ButtonStyle.Secondary));
                }

                if (buttons.Count == 1)
                {
                    sb.AppendLine("At the moment, you aren't a member of any teams, " +
                        "so all I can show you is who you follow.");
                }
                else
                {
                    sb.AppendLine("I can show you a list of people online that you " +
                        "**follow**, or a list of people from one of your **stream teams**.");
                }

                sb.AppendLine();
                sb.AppendLine("Which list would you like me to fetch?");

                EmbedBuilder embed = new EmbedBuilder()
                    .WithColor(145, 70, 255) // "Twitch Purple"
                    .WithTitle($"Raid suggestions for {channel.Name}:")
                    .WithDescription(sb.ToString());

                ComponentBuilder components = new ComponentBuilder();
                ActionRowBuilder buttonRow = new ActionRowBuilder();
                buttons.ForEach(b => buttonRow.WithButton(b));
                components.AddRow(buttonRow);

                await ModifyOriginalResponseAsync(p =>
                {
                    p.Embed = embed.Build();
                    p.Components = components.Build();
                });
            }
            catch (TwitchChannelNotFoundException)
            {
                await FollowupAsync($"Whoops! I couldn't find the channel '{channelName}'.", ephemeral: true);
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Whoops! An error occurred: '{ex.Message}'.", ephemeral: true);
            }
        }

        [ComponentInteraction("followers:*",
            ignoreGroupNames: true,
            runMode: RunMode.Async)]
        public async Task GetFollowedChannelsAsync(string channelId)
        {
            await DeferAsync();
            await FollowupAsync("Be with you in a sec...", ephemeral: true);

            try
            {
                StringBuilder sb = new();

                Channel channel = await _twitch.GetChannelAsync(channelId);
                IEnumerable<OnlineChannel> followedOnline =
                    (await _twitch.GetFollowedOnlineChannels(channelId))
                    .OrderByDescending(o => o.Viewers);

                sb.AppendLine($"**{channel.Name.ToUpper()}'S FOLLOWED CHANNELS**");
                sb.AppendLine();

                if (followedOnline.Count() > 0)
                {
                    foreach (OnlineChannel streamer in followedOnline)
                    {
                        TimeSpan duration = DateTime.UtcNow - streamer.StartedAt;
                        sb.AppendLine(
                            $"`{streamer.Name}` [{streamer.Viewers}]: " +
                            $"**{streamer.Game}** for {duration.Humanize(2)}");
                    }
                }
                else
                {
                    sb.AppendLine("*No followed channels online right now.*");
                }

                await FollowupAsync(sb.ToString());
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Whoops! An error occurred: '{ex.Message}'.", ephemeral: true);
            }
        }

        [ComponentInteraction("group:*",
            ignoreGroupNames: true,
            runMode: RunMode.Async)]
        public async Task GetOnlineGroupMembers(string groupId)
        {
            await DeferAsync();

            try
            {
                StringBuilder sb = new();

                StreamerTeam team = await _twitch.GetTeam(groupId);

                sb.AppendLine($"**{team.Name.ToUpper()}**");
                sb.AppendLine();

                if (team.Streamers.Count > 0)
                {
                    foreach (OnlineChannel streamer in team.Streamers)
                    {
                        TimeSpan duration = DateTime.UtcNow - streamer.StartedAt;
                        sb.AppendLine(
                            $"`{streamer.Name}` [{streamer.Viewers}]: " +
                            $"**{streamer.Game}** for {duration.Humanize(2)}");
                    }
                }
                else
                {
                    sb.AppendLine("*No streamers from this team online right now.*");
                }

                await FollowupAsync(sb.ToString());
            }
            catch (Exception ex)
            {
                await FollowupAsync($"Whoops! An error occurred: '{ex.Message}'.", ephemeral: true);
            }
        }
    }
}
