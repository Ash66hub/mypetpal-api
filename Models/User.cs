using System.ComponentModel.DataAnnotations;
using mypetpal.Data.Common.Interface;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace mypetpal.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long UserId { get; set; }

        [MaxLength(50)]
        public string? Id { get; set; }

        [MaxLength(50)]
        public string? Username { get; set; } 

        [MaxLength(100)]
        public string? Email { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;

        [JsonIgnore]
        public string? Metadata { get; set; }

        [NotMapped]
        public string? AuthProvider { get; set; }

        [NotMapped]
        public bool HasLocalPassword { get; set; } = true;

        [NotMapped]
        public string? ProfilePictureUrl { get; set; }

        public string? RefreshToken { get; set; }

        public long TotalExperience { get; set; } = 0;

        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public int CurrentLevel => CalculateLevel(TotalExperience);

        public static int CalculateLevel(long totalExperience)
        {
            if (totalExperience < 10)
            {
                return 1;
            }

            // Level n threshold uses triangular scaling: 10 * n * (n + 1) / 2
            var normalized = totalExperience / 5.0;
            var level = (int)Math.Floor((Math.Sqrt(1 + (4 * normalized)) - 1) / 2);
            return Math.Max(1, level + 1);
        }


        public UserMetadata? GetUserMetadata()
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

        public bool? HasLocalPassword { get; set; }

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


