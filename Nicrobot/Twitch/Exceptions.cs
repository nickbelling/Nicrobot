namespace Nicrobot.Twitch
{
    public class TwitchChannelNotFoundException : Exception
    {
        public TwitchChannelNotFoundException(string channel)
            : base($"The channel '{channel}' could not be found.")
        { }
    }
}
