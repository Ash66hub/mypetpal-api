using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using mypetpal.dbContext;
using mypetpal.Hubs;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.MinimalApiEndpoints
{
    public static class SocialEndpoints
    {
        public static void MapSocialEndpoints(this IEndpointRouteBuilder app)
        {
            var social = app.MapGroup("social");
            var onlineThreshold = TimeSpan.FromMinutes(1);

            // Search users
            social.MapGet("search", async (string query, long currentUserId, ApplicationDbContext db) => {
                var users = await db.Users
                    .Where(u => u.Username == query && u.UserId != currentUserId)
                    .Take(1)
                    .ToListAsync();

                var friendIds = await db.Friendships
                    .Where(f => (f.UserId == currentUserId || f.FriendId == currentUserId) && f.Status == FriendshipStatus.Accepted)
                    .Select(f => f.UserId == currentUserId ? f.FriendId : f.UserId)
                    .ToListAsync();

                var pendingSent = await db.Friendships
                    .Where(f => f.UserId == currentUserId && f.Status == FriendshipStatus.Pending)
                    .Select(f => f.FriendId)
                    .ToListAsync();

                var now = DateTime.UtcNow;
                return users.Select(u => {
                    var isFriend = friendIds.Contains(u.UserId);
                    var isActive = u.LastActive >= now - onlineThreshold;
                    return new UserSearchResult {
                        UserId = u.UserId,
                        Username = u.Username!,
                        IsFriend = isFriend,
                        IsPending = pendingSent.Contains(u.UserId),
                        // Only reveal online status to confirmed friends, not strangers/pending
                        IsOnline = isFriend && isActive && SocialHub.IsUserConnected(u.UserId.ToString())
                    };
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

                var now = DateTime.UtcNow;
                var friends = await db.Users
                    .Where(u => friendIds.Contains(u.UserId))
                    .Select(u => new { 
                        u.UserId, 
                        u.Username,
                        u.LastActive
                    })
                    .ToListAsync();

                return friends.Select(u => new
                {
                    u.UserId,
                    u.Username,
                    IsOnline = u.LastActive >= now - onlineThreshold && SocialHub.IsUserConnected(u.UserId.ToString())
                });
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
            social.MapPost("respond", async (long requestId, bool accept, IFriendshipService friendshipService, IHubContext<SocialHub> hubContext) => {
                var result = await friendshipService.RespondToRequestAsync(requestId, accept);
                if (result.IsNotFound) return Results.NotFound();
                if (result.IsFriendLimitReached) return Results.BadRequest("One of the pet pals has reached the 25 limit.");

                if (result.IsAccepted)
                {
                    // SignalR Notification to the person who SENT the request
                    await hubContext.Clients.Group(result.RequesterUserId.ToString()).SendAsync("FriendRequestAccepted", result.ReceiverUsername);
                }

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

            // Send space visit invitation
            social.MapPost("visit-invite", async (long senderId, long receiverId, ApplicationDbContext db, IHubContext<SocialHub> hubContext) => {
                if (senderId == receiverId) return Results.BadRequest("Self-inviting is not allowed.");

                var existing = await db.VisitInvitations.FirstOrDefaultAsync(v => 
                    v.SenderId == senderId && v.ReceiverId == receiverId && v.Status == InvitationStatus.Pending);

                if (existing != null) return Results.Conflict("A pending invitation already exists.");

                var sender = await db.Users.FindAsync(senderId);
                var invitation = new VisitInvitation { SenderId = senderId, ReceiverId = receiverId, Status = InvitationStatus.Pending };
                db.VisitInvitations.Add(invitation);
                await db.SaveChangesAsync();

                // SignalR Notification
                await hubContext.Clients.Group(receiverId.ToString()).SendAsync("ReceiveVisitInvite", sender?.Username);

                return Results.Ok(invitation);
            });

            // Get pending visit invitations for a user
            social.MapGet("visit-invites", async (long userId, ApplicationDbContext db) => {
                var invites = await db.VisitInvitations
                    .Where(v => v.ReceiverId == userId && v.Status == InvitationStatus.Pending)
                    .ToListAsync();

                var senderIds = invites.Select(v => v.SenderId).ToList();
                var senders = await db.Users
                    .Where(u => senderIds.Contains(u.UserId))
                    .ToDictionaryAsync(u => u.UserId, u => u.Username);

                return invites.Select(v => new {
                    v.Id,
                    v.SenderId,
                    SenderUsername = senders.ContainsKey(v.SenderId) ? senders[v.SenderId] : "Unknown",
                    v.CreatedAt
                });
            });

            // Respond to visit invitation
            social.MapPost("visit-respond", async (long inviteId, bool accept, ApplicationDbContext db, IHubContext<SocialHub> hubContext) => {
                var invite = await db.VisitInvitations.FindAsync(inviteId);
                if (invite == null) return Results.NotFound();

                invite.Status = accept ? InvitationStatus.Accepted : InvitationStatus.Declined;
                
                if (accept) {
                    var receiver = await db.Users.FindAsync(invite.ReceiverId);
                    // Notify sender that invitation was accepted
                    await hubContext.Clients.Group(invite.SenderId.ToString()).SendAsync("VisitInviteAccepted", receiver?.Username);
                }

                await db.SaveChangesAsync();
                return Results.Ok();
            });
        }
    }
}
