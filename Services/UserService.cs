using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using mypetpal.Data.Common;
using mypetpal.Services.Contracts;
using mypetpal.Models;
using mypetpal.dbContext;

namespace mypetpal.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IProfilePictureStorageService _profilePictureStorageService;

        public UserService(
            ApplicationDbContext context,
            ILogger<UserService> logger,
            IProfilePictureStorageService profilePictureStorageService)
        {
            _context = context;
            _logger = logger;
            _profilePictureStorageService = profilePictureStorageService;
        }

        public async Task<User> CreateNewUser(string? username, string? email, string password)
        {
            if (_context.Users.Any(u => u.Username == username || u.Email == email))
            {
                throw new ArgumentException("Username or Email already exists.");
            }

            var userMetadata = new UserMetadata();
            userMetadata.Metadata_createdUtc = DateTime.Now;
            userMetadata.HasLocalPassword = true;

            var user = new User
            {
                PublicId = await GenerateUniqueUserPublicIdAsync(),
                Username = username,
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(password),
                Metadata = System.Text.Json.JsonSerializer.Serialize(userMetadata)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<IEnumerable<User>> GetAllUsers()
        {
            var users = await _context.Users.Select(u => new User
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email
            }).ToListAsync();

            foreach (var user in users)
            {
                await PopulateTransientUserFieldsAsync(user);
            }

            return users;
        }

        public async Task<IEnumerable<LeaderboardEntry>> GetLeaderboard(int top = 10)
        {
            var safeTop = Math.Clamp(top, 1, 50);

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => !string.IsNullOrWhiteSpace(u.Username))
                .OrderByDescending(u => u.TotalExperience)
                .ThenBy(u => u.Username)
                .Take(safeTop)
                .ToListAsync();

            var leaderboard = new List<LeaderboardEntry>(users.Count);

            foreach (var user in users)
            {
                var metadata = user.GetUserMetadata();
                var signedAvatarUrl = await _profilePictureStorageService
                    .CreateSignedReadUrlAsync(metadata?.ProfilePictureUrl);

                leaderboard.Add(new LeaderboardEntry
                {
                    UserId = user.UserId,
                    PublicId = user.PublicId,
                    Username = user.Username ?? "Unknown",
                    ProfilePictureUrl = signedAvatarUrl,
                    Experience = user.TotalExperience,
                    Level = User.CalculateLevel(user.TotalExperience)
                });
            }

            return leaderboard
                .OrderByDescending(entry => entry.Level)
                .ThenByDescending(entry => entry.Experience)
                .ThenBy(entry => entry.Username)
                .ToList();
        }

        public async Task<User?> GetUserById(long userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            await PopulateTransientUserFieldsAsync(user);
            return user;
        }

        public async Task<User?> GetUserByPublicId(string publicId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == publicId);
            await PopulateTransientUserFieldsAsync(user);
            return user;
        }

        public async Task<User?> GetUserByUsername(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            await PopulateTransientUserFieldsAsync(user);
            return user;
        }


        public async Task<User?> GetUserByEmail(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            await PopulateTransientUserFieldsAsync(user);
            return user;
        }

        public async Task<User> UpdateUser(long userId, string username, string email, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            user.Username = username ?? user.Username;
            user.Email = email ?? user.Email;

            if (!string.IsNullOrEmpty(password))
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(password);
            }

            _logger.LogInformation($"Update User {userId}");

            await _context.SaveChangesAsync();

            return user;
        }

        public async Task<User> UpdateProfilePicture(long userId, IFormFile file)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("File must be 5MB or less.");
            }

            await _profilePictureStorageService.DeleteAllForUserAsync(userId);

            await using var stream = file.OpenReadStream();
            var objectPath = await _profilePictureStorageService.UploadAsync(
                userId,
                stream,
                file.ContentType,
                file.FileName);

            var metadata = user.GetUserMetadata() ?? new UserMetadata();
            metadata.ProfilePictureUrl = null;
            metadata.ProfilePictureUrl = objectPath;
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            user.SetUserMetadata(metadata);

            _logger.LogInformation("Updated profile picture for User {UserId}", userId);

            await _context.SaveChangesAsync();

            await PopulateTransientUserFieldsAsync(user);
            return user;
        }

        public async Task<bool> DeleteUser(long userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return false;
            }

            _logger.LogInformation($"Deleting User {userId}");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task SaveRefreshToken(long userId, string refreshToken) 
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.UserId == userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;

                _logger.LogInformation("Saving refresh token");

                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateUserMetadata(long userId, UserMetadata metadata)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return;
            }

            user.SetUserMetadata(metadata);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        private async Task<string> GenerateUniqueUserPublicIdAsync()
        {
            string id;
            do
            {
                id = PublicIdGenerator.NewId();
            }
            while (await _context.Users.AnyAsync(u => u.PublicId == id));

            return id;
        }

        private async Task PopulateTransientUserFieldsAsync(User? user)
        {
            if (user == null)
            {
                return;
            }

            var metadata = user.GetUserMetadata();
            user.AuthProvider = metadata?.Provider;
            user.HasLocalPassword = metadata?.HasLocalPassword
                ?? !string.Equals(metadata?.Provider, "Google", StringComparison.OrdinalIgnoreCase);
            user.ProfilePictureUrl = await _profilePictureStorageService.CreateSignedReadUrlAsync(metadata?.ProfilePictureUrl);
        }
    }
}
