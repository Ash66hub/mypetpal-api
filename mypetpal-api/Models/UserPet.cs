
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class UserPet
    {
        [Key]
        public string? PetId { get; set; }  

        [ForeignKey("User")]
        public string? UserId { get; set; }  

        public PetAttributes? PetAttributes { get; set; }
    }
}

