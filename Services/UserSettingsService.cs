using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Services
{
    public class UserSettingsService : IUserSettingsService
    {
        private readonly ApplicationDbContext _context;

        public UserSettingsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserSettings> GetSettingsAsync(long userId)
        {
            var settings = await _context.UserSettings.FirstOrDefaultAsync(u => u.UserId == userId);
            
            if (settings == null)
            {
                // Return default settings for new users (room centers)
                return new UserSettings { UserId = userId };
            }
            
            return settings;
        }

        public async Task UpdateSettingsAsync(UserSettings settings)
        {
            var existing = await _context.UserSettings.FirstOrDefaultAsync(u => u.UserId == settings.UserId);
            
            if (existing == null)
            {
                _context.UserSettings.Add(settings);
            }
            else
            {
                existing.LastPetX = settings.LastPetX;
                existing.LastPetY = settings.LastPetY;
                existing.ZoomLevel = settings.ZoomLevel;
                existing.IsMuted = settings.IsMuted;
                existing.MusicVolume = settings.MusicVolume;
                existing.SoundVolume = settings.SoundVolume;
            }
            
            await _context.SaveChangesAsync();
        }
    }
}
