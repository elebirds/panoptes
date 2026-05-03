namespace Panoptes.Core.Domain
{
    public class RoomPlayerDto
    {
        public string PlayerId { get; set; }
        public string Username { get; set; }
        public bool IsHost { get; set; }
        public bool IsReady { get; set; }
        public bool IsBot { get; set; }
    }
}
