
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class UserPet
    {
        [Key]
        public long PetId { get; set; }  

        [ForeignKey("User")]
        public long UserId { get; set; }

        public PetAttributes PetAttributes { get; set; } = new PetAttributes();
    }
}

