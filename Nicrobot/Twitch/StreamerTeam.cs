namespace Nicrobot.Twitch
{
    public class StreamerTeam
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public IList<OnlineChannel> Streamers { get; set; } = new List<OnlineChannel>();
    }
}
