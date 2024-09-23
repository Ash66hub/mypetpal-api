using System.ComponentModel.DataAnnotations;
using mypetpal.Data.Common.Interface;
using static mypetpal.Data.Common.Enums.PetEnums;

namespace mypetpal.Models
{
    public class PetAttributes
    {
        [Key]
        public string PetId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PetName { get; set; }

        [Required]
        [MaxLength(50)]
        public PetTypes PetType { get; set; }

        [Required]
        public int PetLevel { get; set; }

        [Required]
        public int Age { get; set; }

        [Required]
        public PetStatus PetStatus { get; set; }

        public PetMetadata Metadata { get; set; } = new PetMetadata();

        [MaxLength(250)]
        public string PetAvatar { get; set; }

        // Pet Stats

        public int Xp { get; set; }

        public int Health { get; set; }

        public int Happiness { get; set; }
    }


    public class PetMetadata : IMetadata
    {
        public DateTime? Metadata_createdUtc { get; set; }

        public DateTime? Metadata_deletedUtc { get; set; }

        public DateTime? Metadata_updatedUtc { get; set; }
    }

    
}

