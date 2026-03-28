using mypetpal.Models;

namespace mypetpal.Services.Contracts
{
    public interface IDecorService
    {
        Task<List<DecorInstance>> GetDecorByUserIdAsync(long userId);
        Task SaveDecorForUserAsync(long userId, List<DecorInstance> instances);
    }
}
