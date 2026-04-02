using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Hubs;
using mypetpal.Services.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace mypetpal.Services
{
    public class ExperienceService : IExperienceService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<SocialHub> _socialHubContext;
        private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(1);

        public ExperienceService(ApplicationDbContext db, IHubContext<SocialHub> socialHubContext)
        {
            _db = db;
            _socialHubContext = socialHubContext;
        }

        public async Task<bool> TouchLastActiveAsync(long userId, CancellationToken cancellationToken = default)
        {
            var before = await _db.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.UserId, u.LastActive })
                .FirstOrDefaultAsync(cancellationToken);

            if (before == null)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var threshold = now - OnlineThreshold;
            var wasOnline = before.LastActive >= threshold && SocialHub.IsUserConnected(userId.ToString());

            await _db.Users
                .Where(u => u.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.LastActive, now), cancellationToken);

            var isOnline = SocialHub.IsUserConnected(userId.ToString());
            if (!wasOnline && isOnline)
            {
                var friendIds = await _db.Friendships
                    .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == Models.FriendshipStatus.Accepted)
                    .Select(f => f.UserId == userId ? f.FriendId.ToString() : f.UserId.ToString())
                    .ToListAsync(cancellationToken);

                if (friendIds.Count > 0)
                {
                    await _socialHubContext.Clients.Groups(friendIds)
                        .SendAsync("UserStatusChanged", userId.ToString(), true, cancellationToken);
                }
            }

            return true;
        }
    }
}
