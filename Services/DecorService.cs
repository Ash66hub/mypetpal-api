using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Services
{
    public class DecorService : IDecorService
    {
        private readonly ApplicationDbContext _context;

        public DecorService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DecorInstance>> GetDecorByUserIdAsync(long userId)
        {
            return await _context.DecorInstances
                .Where(d => d.UserId == userId)
                .ToListAsync();
        }

        public async Task SaveDecorForUserAsync(long userId, List<DecorInstance> instances)
        {
            // Remove existing decor for this user
            var existingDecor = await _context.DecorInstances
                .Where(d => d.UserId == userId)
                .ToListAsync();
            
            _context.DecorInstances.RemoveRange(existingDecor);

            // Add new decor instances
            foreach (var instance in instances)
            {
                instance.UserId = userId;
                instance.Id = 0; // Ensure new ID is generated
                _context.DecorInstances.Add(instance);
            }

            await _context.SaveChangesAsync();
        }
    }
}
