using mypetpal.Models;

namespace mypetpal.Services.Contracts
{
    public interface IUserService
    {
        Task<User> CreateNewUser(string username, string email, string password);

        Task<IEnumerable<User>> GetAllUsers();

        Task<User> GetUserById(string userId);

        Task<User> GetUserByUsername(string userId);

        Task<User> UpdateUser(string userId, string username, string email, string password);

        Task<bool> DeleteUser(string userId);

        Task SaveRefreshToken(string userId, string refreshToken);
    }
}
