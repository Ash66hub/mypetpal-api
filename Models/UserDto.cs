using System;

namespace mypetpal.Models
{
    public class UserDto
    {
        public long UserId { get; set; }
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public long TotalExperience { get; set; }
        public int CurrentLevel { get; set; }
        public DateTime LastActive { get; set; }
        public string? Email { get; set; } // Only populated if authorized
        public string? AuthProvider { get; set; }
        public bool HasLocalPassword { get; set; }
    }
}
