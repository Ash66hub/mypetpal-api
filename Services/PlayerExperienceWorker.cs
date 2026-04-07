using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Hubs;
using mypetpal.Models;

namespace mypetpal.Services
{
    public class PlayerExperienceWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<GameHub> _gameHubContext;
        private readonly IHubContext<SocialHub> _socialHubContext;
        private readonly ILogger<PlayerExperienceWorker> _logger;
        private readonly HashSet<long> _lastKnownOnlineUsers = new();
        private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(1);

        public PlayerExperienceWorker(
            IServiceScopeFactory scopeFactory,
            IHubContext<GameHub> gameHubContext,
            IHubContext<SocialHub> socialHubContext,
            ILogger<PlayerExperienceWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _gameHubContext = gameHubContext;
            _socialHubContext = socialHubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessTickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing experience heartbeat tick.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ProcessTickAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            var threshold = now - OnlineThreshold;

            var connectedIds = SocialHub.GetConnectedUserIds()
                .Select(id => long.TryParse(id, out var parsed) ? parsed : (long?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            if (connectedIds.Count == 0)
            {
                await BroadcastPresenceTransitionsAsync(db, new HashSet<long>(), cancellationToken);
                return;
            }

            var activeBefore = await db.Users
                .Where(u => connectedIds.Contains(u.UserId) && u.LastActive >= threshold)
                .Select(u => new { u.UserId, u.TotalExperience })
                .ToListAsync(cancellationToken);

            var currentlyOnlineUsers = activeBefore
                .Select(u => u.UserId)
                .ToHashSet();

            await BroadcastPresenceTransitionsAsync(db, currentlyOnlineUsers, cancellationToken);

            if (activeBefore.Count == 0)
            {
                return;
            }

            var previousLevels = activeBefore.ToDictionary(
                x => x.UserId,
                x => User.CalculateLevel(x.TotalExperience));

            await db.Users
                .Where(u => connectedIds.Contains(u.UserId) && u.LastActive >= threshold)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(u => u.TotalExperience, u => u.TotalExperience + 2), cancellationToken);

            var activeIds = activeBefore.Select(x => x.UserId).ToList();
            var activeAfter = await db.Users
                .Where(u => activeIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.TotalExperience })
                .ToListAsync(cancellationToken);

            foreach (var player in activeAfter)
            {
                if (!previousLevels.TryGetValue(player.UserId, out var previousLevel))
                {
                    continue;
                }

                await _gameHubContext.Clients.Group(player.UserId.ToString())
                    .SendAsync("ExperienceUpdated", player.UserId.ToString(), player.TotalExperience, cancellationToken);

                var newLevel = User.CalculateLevel(player.TotalExperience);
                if (newLevel > previousLevel)
                {
                    await _gameHubContext.Clients.Group(player.UserId.ToString())
                        .SendAsync("LeveledUp", player.UserId.ToString(), newLevel, player.TotalExperience, cancellationToken);
                }
            }
        }

        private async Task BroadcastPresenceTransitionsAsync(
            ApplicationDbContext db,
            HashSet<long> currentOnlineUsers,
            CancellationToken cancellationToken)
        {
            var wentOffline = _lastKnownOnlineUsers.Except(currentOnlineUsers).ToList();
            var cameOnline = currentOnlineUsers.Except(_lastKnownOnlineUsers).ToList();

            foreach (var offlineUserId in wentOffline)
            {
                await BroadcastStatusChangedAsync(db, offlineUserId, false, cancellationToken);
            }

            foreach (var onlineUserId in cameOnline)
            {
                await BroadcastStatusChangedAsync(db, onlineUserId, true, cancellationToken);
            }

            _lastKnownOnlineUsers.Clear();
            foreach (var userId in currentOnlineUsers)
            {
                _lastKnownOnlineUsers.Add(userId);
            }
        }

        private async Task BroadcastStatusChangedAsync(
            ApplicationDbContext db,
            long userId,
            bool isOnline,
            CancellationToken cancellationToken)
        {
            var friendIds = await db.Friendships
                .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == FriendshipStatus.Accepted)
                .Select(f => f.UserId == userId ? f.FriendId.ToString() : f.UserId.ToString())
                .ToListAsync(cancellationToken);

            if (friendIds.Count == 0)
            {
                return;
            }

            await _socialHubContext.Clients.Groups(friendIds)
                .SendAsync("UserStatusChanged", userId.ToString(), isOnline, cancellationToken);
        }
    }
}
