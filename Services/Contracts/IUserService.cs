using mypetpal.Models;

namespace mypetpal.Services.Contracts
{
    public interface IUserService
    {
        Task<User> CreateNewUser(string username, string email, string password);

        Task<IEnumerable<User>> GetAllUsers();

        Task<User> GetUserById(long userId);

        Task<User> GetUserByUsername(string username);

        Task<User> GetUserByEmail(string email);

        Task<User> UpdateUser(long userId, string username, string email, string password);

        Task<bool> DeleteUser(long userId);

        Task SaveRefreshToken(long userId, string refreshToken);
    }
}
