using mypetpal.Models;
using System.Threading.Tasks;

namespace mypetpal.Services.Contracts
{
    public interface IUserSettingsService
    {
        Task<UserSettings> GetSettingsAsync(long userId);
        Task UpdateSettingsAsync(UserSettings settings);
    }
}
