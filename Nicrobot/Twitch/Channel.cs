namespace Nicrobot.Twitch
{
    public class Channel
    {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    public class OnlineChannel : Channel
    {
        public string Game { get; set; }
        public int Viewers { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
