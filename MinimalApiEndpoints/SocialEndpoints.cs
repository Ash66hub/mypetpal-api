using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using mypetpal.dbContext;
using mypetpal.Hubs;
using mypetpal.Models;

namespace mypetpal.MinimalApiEndpoints
{
    public static class SocialEndpoints
    {
        public static void MapSocialEndpoints(this IEndpointRouteBuilder app)
        {
            var social = app.MapGroup("social");

            // Search users
            social.MapGet("search", async (string query, long currentUserId, ApplicationDbContext db) => {
                var users = await db.Users
                    .Where(u => u.Username == query && u.UserId != currentUserId)
                    .Take(1)
                    .ToListAsync();

                var friendIds = await db.Friendships
                    .Where(f => f.UserId == currentUserId || f.FriendId == currentUserId)
                    .Select(f => f.UserId == currentUserId ? f.FriendId : f.UserId)
                    .ToListAsync();

                var pendingSent = await db.Friendships
                    .Where(f => f.UserId == currentUserId && f.Status == FriendshipStatus.Pending)
                    .Select(f => f.FriendId)
                    .ToListAsync();

                return users.Select(u => new UserSearchResult {
                    UserId = u.UserId,
                    Username = u.Username!,
                    IsFriend = friendIds.Contains(u.UserId),
                    IsPending = pendingSent.Contains(u.UserId),
                    IsOnline = SocialHub.IsUserOnline(u.UserId.ToString())
                });
            });

            // Send request
            social.MapPost("request", async (long userId, long targetId, ApplicationDbContext db, IHubContext<SocialHub> hubContext) => {
                if (userId == targetId) return Results.BadRequest("Self-friending is not allowed.");

                // Check Limit
                var friendCount = await db.Friendships.CountAsync(f => 
                    (f.UserId == userId || f.FriendId == userId) && f.Status == FriendshipStatus.Accepted);
                if (friendCount >= 25) return Results.BadRequest("You have reached the maximum limit of 25 pals.");

                var existing = await db.Friendships.FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == targetId) || 
                    (f.UserId == targetId && f.FriendId == userId));

                if (existing != null) return Results.Conflict("Relationship already exists.");

                var sender = await db.Users.FindAsync(userId);
                if (sender == null) return Results.NotFound("Sender not found.");

                var friendship = new Friendship { UserId = userId, FriendId = targetId, Status = FriendshipStatus.Pending };
                db.Friendships.Add(friendship);
                await db.SaveChangesAsync();

                // SignalR Notification
                await hubContext.Clients.Group(targetId.ToString()).SendAsync("ReceiveFriendRequest", sender.Username);

                return Results.Ok(friendship);
            });

            // List friends
            social.MapGet("list", async (long userId, ApplicationDbContext db) => {
                var friendIds = await db.Friendships
                    .Where(f => (f.UserId == userId || f.FriendId == userId) && f.Status == FriendshipStatus.Accepted)
                    .Select(f => f.UserId == userId ? f.FriendId : f.UserId)
                    .ToListAsync();

                return await db.Users
                    .Where(u => friendIds.Contains(u.UserId))
                    .Select(u => new { 
                        u.UserId, 
                        u.Username,
                        IsOnline = SocialHub.IsUserOnline(u.UserId.ToString())
                    })
                    .ToListAsync();
            });

            // Pending requests (incoming)
            social.MapGet("pending", async (long userId, ApplicationDbContext db) => {
                var requests = await db.Friendships
                    .Where(f => f.FriendId == userId && f.Status == FriendshipStatus.Pending)
                    .ToListAsync();

                var senderIds = requests.Select(f => f.UserId).ToList();
                var senders = await db.Users
                    .Where(u => senderIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.Username);

                return requests.Select(f => new {
                    f.Id,
                    f.UserId,
                    SenderUsername = senders.ContainsKey(f.UserId) ? senders[f.UserId] : "Unknown"
                });
            });

            // Respond to request
            social.MapPost("respond", async (long requestId, bool accept, ApplicationDbContext db, IHubContext<SocialHub> hubContext) => {
                var request = await db.Friendships.FindAsync(requestId);
                if (request == null) return Results.NotFound();

                if (accept) {
                    // Check Limit for both
                    var countU = await db.Friendships.CountAsync(f => (f.UserId == request.UserId || f.FriendId == request.UserId) && f.Status == FriendshipStatus.Accepted);
                    var countF = await db.Friendships.CountAsync(f => (f.UserId == request.FriendId || f.FriendId == request.FriendId) && f.Status == FriendshipStatus.Accepted);
                    
                    if (countU >= 25 || countF >= 25) return Results.BadRequest("One of the pet pals has reached the 25 limit.");

                    request.Status = FriendshipStatus.Accepted;
                    request.UpdatedAt = DateTime.UtcNow;
                    
                    var friend = await db.Users.FindAsync(request.FriendId);
                    // SignalR Notification to the person who SENT the request
                    await hubContext.Clients.Group(request.UserId.ToString()).SendAsync("FriendRequestAccepted", friend?.Username);
                } else {
                    db.Friendships.Remove(request);
                }

                await db.SaveChangesAsync();
                return Results.Ok();
            });

            // Remove friend
            social.MapDelete("remove", async (long userId, long friendId, ApplicationDbContext db) => {
                var friendship = await db.Friendships.FirstOrDefaultAsync(f => 
                    (f.UserId == userId && f.FriendId == friendId) || 
                    (f.UserId == friendId && f.FriendId == userId));

                if (friendship == null) return Results.NotFound();

                db.Friendships.Remove(friendship);
                await db.SaveChangesAsync();
                return Results.Ok();
            });
        }
    }
}
