using System.ComponentModel.DataAnnotations;

namespace mypetpal.Models
{
    public class DecorInstance
    {
        [Key]
        public long Id { get; set; }
        public long UserId { get; set; }
        public string DecorId { get; set; } = string.Empty; // Matches DecorItem.id
        public float X { get; set; }
        public float Y { get; set; }
        public string Rotation { get; set; } = "SE"; // 'SE' or 'SW'
    }
}
