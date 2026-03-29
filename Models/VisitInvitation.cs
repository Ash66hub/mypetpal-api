using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class VisitInvitation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public long SenderId { get; set; }

        [Required]
        public long ReceiverId { get; set; }

        [Required]
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum InvitationStatus
    {
        Pending,
        Accepted,
        Declined,
        Expired
    }
}
