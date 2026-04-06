using mypetpal.Models;
using Microsoft.AspNetCore.Http;

namespace mypetpal.Services.Contracts
{
    public interface IUserService
    {
        Task<User> CreateNewUser(string? username, string? email, string password);

        Task<IEnumerable<User>> GetAllUsers();

        Task<IEnumerable<LeaderboardEntry>> GetLeaderboard(int top = 10);

        Task<User?> GetUserById(long userId);

        Task<User?> GetUserByPublicId(string publicId);

        Task<User?> GetUserByUsername(string username);

        Task<User?> GetUserByEmail(string email);

        Task<User> UpdateUser(long userId, string username, string email, string password);

        Task<User> UpdateProfilePicture(long userId, IFormFile file);

        Task<bool> DeleteUser(long userId);

        Task SaveRefreshToken(long userId, string refreshToken);

        Task UpdateUserMetadata(long userId, UserMetadata metadata);
    }
}
