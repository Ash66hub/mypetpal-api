using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class UserSettings
    {
        [Key]
        public long UserId { get; set; }

        public float LastPetX { get; set; } = 1000f; // Default to room center
        public float LastPetY { get; set; } = 1027f; // Default to room center + offset

        public float ZoomLevel { get; set; } = 5.0f; // Default zoom

        public bool IsMuted { get; set; } = false;
        public float MusicVolume { get; set; } = 0.5f;
        public float SoundVolume { get; set; } = 0.5f;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
