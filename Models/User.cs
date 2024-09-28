using System.ComponentModel.DataAnnotations;
using mypetpal.Data.Common.Interface;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;

namespace mypetpal.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long UserId { get; set; }

        [Required]
        [MaxLength(50)]
        public string? Username { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        public string? Password { get; set; }

        [JsonIgnore]
        public string? Metadata { get; set; }

        public string? RefreshToken { get; set; }


        public UserMetadata GetUserMetadata()
        {
            if (string.IsNullOrEmpty(Metadata))
            {
                return new UserMetadata();
            }
            return System.Text.Json.JsonSerializer.Deserialize<UserMetadata>(Metadata);
        }

        public void SetUserMetadata(UserMetadata metadata)
        {
            Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);
        }
    }


    public class UserMetadata : IMetadata
    {
        public string? ProfilePictureUrl { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? Location { get; set; }

        public string? Provider { get; set; } // Social media login provider. Create enum for this if needed [Gmail,Meta,etc]

        public string? ProviderUserId { get; set; }

        public DateTime? LastLogin { get; set; }

        public string? AccountStatus { get; set; }

        public bool? TwoFactorEnabled { get; set; }

        public string? LanguagePreference { get; set; }

        public int? NumberOfPetsCreated { get; set; }


        public DateTime? Metadata_createdUtc { get; set; }

        public DateTime? Metadata_deletedUtc { get; set; }

        public DateTime? Metadata_updatedUtc { get; set; }
    }
}


