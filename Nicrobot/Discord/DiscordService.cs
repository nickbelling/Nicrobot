using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Nicrobot.Discord
{
    public class DiscordService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _interactionService;
        private readonly IServiceProvider _services;
        private readonly ILogger<DiscordService> _logger;

        private readonly string _token;

        public DiscordService(
            DiscordSocketClient client,
            InteractionService interactionService,
            IServiceProvider services,
            IConfiguration config,
            ILogger<DiscordService> logger)
        {
            _client = client;
            _interactionService = interactionService;
            _services = services;
            _logger = logger;

            string discordToken = config.GetValue<string>(Constants.DISCORD_BOT_TOKEN);
            ArgumentNullException.ThrowIfNull(discordToken);
            _token = discordToken;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Add event handlers
            _client.Log += Log;
            _client.Ready += Ready;
            _client.InteractionCreated += InteractionCreated;
            _interactionService.Log += Log;

            // Login using the token
            await _client.LoginAsync(TokenType.Bot, _token);

            // Start the client
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.SetGameAsync(null);
            await _client.SetStatusAsync(UserStatus.Offline);

            // Stop the client
            await _client.StopAsync();

            // Remove all the event handlers
            _client.Log -= Log;
            _client.Ready -= Ready;
            _client.InteractionCreated -= InteractionCreated;
            _interactionService.Log -= Log;
        }

        /// <summary>
        /// Fired when the client is online and the bot is ready.
        /// </summary>
        /// <returns></returns>
        private async Task Ready()
        {
            await _client.SetStatusAsync(UserStatus.Online);

            // Find all of the interaction modules (i.e. Commands) in this assembly
            await _interactionService.AddModulesAsync(GetType().Assembly, _services);

            // Register all of the found modules with Discord
            foreach (SocketGuild guild in _client.Guilds)
            {
                await _interactionService.RegisterCommandsToGuildAsync(guild.Id);
            }
        }

        /// <summary>
        /// Fired every time an interaction occurs. Directs it to the appropriate module.
        /// </summary>
        /// <param name="interaction"></param>
        /// <returns></returns>
        private async Task InteractionCreated(SocketInteraction interaction)
        {
            try
            {
                // Direct interaction to the appropriate module
                SocketInteractionContext context = new SocketInteractionContext(_client, interaction);
                await _interactionService.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create interaction.");
            }
        }

        /// <summary>
        /// Logs a message from the Discord client.
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private Task Log(LogMessage arg)
        {
            // Convert a Discord LogSeverity to an ILogger LogLevel
            LogLevel logLevel = arg.Severity switch
            {
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Debug => LogLevel.Debug,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Info => LogLevel.Information,
                _ => throw new NotImplementedException()
            };

            _logger.Log(logLevel, message: arg.Message ?? arg.Exception.Message, args: null);
            return Task.CompletedTask;
        }
    }
}
