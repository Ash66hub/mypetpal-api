using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace mypetpal.Hubs
{
    public class SocialHub : Hub
    {
        private readonly mypetpal.dbContext.ApplicationDbContext _db;

        public SocialHub(mypetpal.dbContext.ApplicationDbContext db)
        {
            _db = db;
        }

        // Tracks active connections per user
        private static readonly ConcurrentDictionary<string, int> UserPresence = new();
        // Tracks which userId is associated with a connectionId for disconnection cleanup
        private static readonly ConcurrentDictionary<string, string> ConnectionToUser = new();

        public static bool IsUserOnline(string userId) => UserPresence.TryGetValue(userId, out var count) && count > 0;

        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            
            // Track Presence
            ConnectionToUser[Context.ConnectionId] = userId;
            UserPresence.AddOrUpdate(userId, 1, (_, count) => count + 1);

            if (long.TryParse(userId, out var uid))
            {
                // Only broadcast to friends
                var friendIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    _db.Friendships
                        .Where(f => (f.UserId == uid || f.FriendId == uid) && f.Status == mypetpal.Models.FriendshipStatus.Accepted)
                        .Select(f => f.UserId == uid ? f.FriendId.ToString() : f.UserId.ToString())
                );

                await Clients.Groups(friendIds).SendAsync("UserStatusChanged", userId, true);
            }
        }

        public async Task LeaveUserGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            if (ConnectionToUser.TryRemove(Context.ConnectionId, out var userId))
            {
                UserPresence.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));
                
                if (!IsUserOnline(userId) && long.TryParse(userId, out var uid))
                {
                    // Only broadcast to friends
                    var friendIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                        _db.Friendships
                            .Where(f => (f.UserId == uid || f.FriendId == uid) && f.Status == mypetpal.Models.FriendshipStatus.Accepted)
                            .Select(f => f.UserId == uid ? f.FriendId.ToString() : f.UserId.ToString())
                    );

                    await Clients.Groups(friendIds).SendAsync("UserStatusChanged", userId, false);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task NotifyFriendRequest(string targetUserId, string senderUsername)
        {
            await Clients.Group(targetUserId).SendAsync("ReceiveFriendRequest", senderUsername);
        }

        public async Task NotifyRequestAccepted(string targetUserId, string friendUsername)
        {
            await Clients.Group(targetUserId).SendAsync("FriendRequestAccepted", friendUsername);
        }
    }
}
