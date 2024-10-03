using System.ComponentModel.DataAnnotations;
using mypetpal.Data.Common.Interface;
using System.Text.Json.Serialization;
using static mypetpal.Data.Common.Enums.PetEnums;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class PetAttributes
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PetId { get; set; }

        [Required]
        [MaxLength(50)]
        public string? PetName { get; set; }

        [Required]
        public PetTypes PetType { get; set; }

        public int? PetLevel { get; set; }

        public int? Age { get; set; }

        public PetStatus? PetStatus { get; set; }


        [MaxLength(1000)]
        [JsonIgnore]
        public string? Metadata { get; set; }

        [MaxLength(250)]
        public string? PetAvatar { get; set; }

        // Pet Stats

        public int Xp { get; set; }

        public int Health { get; set; }

        public int Happiness { get; set; }

        // Deserialize JSON string to PetMetadata object
        public PetMetadata? GetPetMetadata()
        {
            if (string.IsNullOrEmpty(Metadata))
            {
                return new PetMetadata();
            }
            return System.Text.Json.JsonSerializer.Deserialize<PetMetadata>(Metadata);
        }

        // Serialize PetMetadata object to JSON string
        public void SetPetMetadata(PetMetadata metadata)
        {
            Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);
        }
    }


    public class PetMetadata : IMetadata
    {
        public DateTime? Metadata_createdUtc { get; set; }

        public DateTime? Metadata_deletedUtc { get; set; }

        public DateTime? Metadata_updatedUtc { get; set; }
    }
}

