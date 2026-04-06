namespace mypetpal.Models
{
    public class LeaderboardEntry
    {
        public long UserId { get; set; }

        public string? PublicId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string? ProfilePictureUrl { get; set; }

        public int Level { get; set; }

        public long Experience { get; set; }
    }
}
