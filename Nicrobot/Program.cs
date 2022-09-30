using Discord.Interactions;
using Discord.WebSocket;
using Nicrobot.Discord;
using Nicrobot.Twitch;

namespace Nicrobot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddLogging();

                    services.AddSingleton<TwitchService>();
                    services.AddHostedService(x => x.GetService<TwitchService>());

                    services.AddSingleton<DiscordSocketClient>();
                    services.AddSingleton<InteractionService>();
                    services.AddHostedService<DiscordService>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}