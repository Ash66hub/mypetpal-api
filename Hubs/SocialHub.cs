using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace mypetpal.Hubs
{
    public class SocialHub : Hub
    {
        public class RoomDecorSyncItem
        {
            public string DecorId { get; set; } = string.Empty;
            public float X { get; set; }
            public float Y { get; set; }
            public string Rotation { get; set; } = "SE";
        }

        private readonly mypetpal.dbContext.ApplicationDbContext _db;

        public SocialHub(mypetpal.dbContext.ApplicationDbContext db)
        {
            _db = db;
        }

        // Tracks active connections per user
        private static readonly ConcurrentDictionary<string, int> UserPresence = new();
        // Tracks which userId is associated with a connectionId for disconnection cleanup
        private static readonly ConcurrentDictionary<string, string> ConnectionToUser = new();

        public static bool IsUserConnected(string userId) => UserPresence.TryGetValue(userId, out var count) && count > 0;

        public static bool IsUserOnline(string userId) => IsUserConnected(userId);

        public static IReadOnlyCollection<string> GetConnectedUserIds()
        {
            return UserPresence
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => kvp.Key)
                .ToArray();
        }

        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            
            // Track Presence
            ConnectionToUser[Context.ConnectionId] = userId;
            UserPresence.AddOrUpdate(userId, 1, (_, count) => count + 1);

            if (long.TryParse(userId, out var uid))
            {
                var now = DateTime.UtcNow;
                await _db.Users
                    .Where(u => u.UserId == uid)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.LastActive, now));

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
                
                // Always notify all rooms the user might be in that they went offline/disconnected
                // Since we don't track rooms per connection easily here, it's safer to just let the client handle it
                // We'll broadcast a global UserStatusChanged offline event

                if (!IsUserConnected(userId) && long.TryParse(userId, out var uid))
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

        public async Task JoinRoom(string roomOwnerId, string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room_{roomOwnerId}");
            
            // Notify everyone already in the room (especially the host) that a new user joined
            await Clients.GroupExcept($"room_{roomOwnerId}", Context.ConnectionId)
                .SendAsync("UserJoinedRoom", userId);
        }

        public async Task LeaveRoom(string roomOwnerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room_{roomOwnerId}");
            if (ConnectionToUser.TryGetValue(Context.ConnectionId, out var userId))
            {
                await Clients.Group($"room_{roomOwnerId}").SendAsync("UserLeftRoom", userId);
            }
        }

        public async Task SyncPetPosition(string roomOwnerId, double x, double y, string userId)
        {
            await Clients.Group($"room_{roomOwnerId}").SendAsync("PetPositionSynced", userId, x, y);
        }

        public async Task SendRoomMessage(string roomOwnerId, string message, string userId, string username)
        {
            await Clients.Group($"room_{roomOwnerId}").SendAsync("RoomMessageReceived", userId, username, message);
        }

        public async Task SyncRoomDecor(string roomOwnerId, string userId, List<RoomDecorSyncItem> instances)
        {
            await Clients.Group($"room_{roomOwnerId}").SendAsync("RoomDecorSynced", userId, instances);
        }

        public async Task KickUser(string roomOwnerId, string userIdToKick)
        {
            // Send directly to the target user's personal group
            await Clients.Group(userIdToKick).SendAsync("KickedFromRoom", roomOwnerId);
        }
    }
}
