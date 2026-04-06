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

            static async Task<string?> ResolveSignedProfilePictureUrl(
                IProfilePictureStorageService storageService,
                string? metadataJson)
            {
                if (string.IsNullOrWhiteSpace(metadataJson))
                {
                    return null;
                }

                UserMetadata? metadata;
                try
                {
                    metadata = System.Text.Json.JsonSerializer.Deserialize<UserMetadata>(metadataJson);
                }
                catch
                {
                    return null;
                }

                return await storageService.CreateSignedReadUrlAsync(metadata?.ProfilePictureUrl);
            }

            // Search users
            social.MapGet("search", async (string query, long currentUserId, ApplicationDbContext db, IProfilePictureStorageService storageService) => {
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
                var result = new List<UserSearchResult>(users.Count);

                foreach (var u in users)
                {
                    var isFriend = friendIds.Contains(u.UserId);
                    var isActive = u.LastActive >= now - onlineThreshold;
                    var profilePictureUrl = await ResolveSignedProfilePictureUrl(storageService, u.Metadata);

                    result.Add(new UserSearchResult {
                        UserId = u.UserId,
                        Username = u.Username!,
                        ProfilePictureUrl = profilePictureUrl,
                        IsFriend = isFriend,
                        IsPending = pendingSent.Contains(u.UserId),
                        // Only reveal online status to confirmed friends, not strangers/pending
                        IsOnline = isFriend && isActive && SocialHub.IsUserConnected(u.UserId.ToString())
                    });
                }

                return result;
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
            social.MapGet("list", async (long userId, ApplicationDbContext db, IProfilePictureStorageService storageService) => {
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
                        u.LastActive,
                        u.Metadata
                    })
                    .ToListAsync();

                var result = new List<object>(friends.Count);

                foreach (var u in friends)
                {
                    var profilePictureUrl = await ResolveSignedProfilePictureUrl(storageService, u.Metadata);

                    result.Add(new
                    {
                        u.UserId,
                        u.Username,
                        ProfilePictureUrl = profilePictureUrl,
                        IsOnline = u.LastActive >= now - onlineThreshold && SocialHub.IsUserConnected(u.UserId.ToString())
                    });
                }

                return result;
            });

            // Pending requests (incoming)
            social.MapGet("pending", async (long userId, ApplicationDbContext db, IProfilePictureStorageService storageService) => {
                var requests = await db.Friendships
                    .Where(f => f.FriendId == userId && f.Status == FriendshipStatus.Pending)
                    .ToListAsync();

                var senderIds = requests.Select(f => f.UserId).ToList();
                var senders = await db.Users
                    .Where(u => senderIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.Username, u.Metadata })
                    .ToListAsync();

                var senderMap = senders.ToDictionary(u => u.UserId, u => u);
                var result = new List<object>(requests.Count);

                foreach (var request in requests)
                {
                    senderMap.TryGetValue(request.UserId, out var sender);
                    var profilePictureUrl = await ResolveSignedProfilePictureUrl(storageService, sender?.Metadata);

                    result.Add(new
                    {
                        request.Id,
                        request.UserId,
                        SenderUsername = sender?.Username ?? "Unknown",
                        ProfilePictureUrl = profilePictureUrl
                    });
                }

                return result;
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
            social.MapGet("visit-invites", async (long userId, ApplicationDbContext db, IProfilePictureStorageService storageService) => {
                var invites = await db.VisitInvitations
                    .Where(v => v.ReceiverId == userId && v.Status == InvitationStatus.Pending)
                    .ToListAsync();

                var senderIds = invites.Select(v => v.SenderId).ToList();
                var senders = await db.Users
                    .Where(u => senderIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.Username, u.Metadata })
                    .ToListAsync();

                var senderMap = senders.ToDictionary(u => u.UserId, u => u);
                var result = new List<object>(invites.Count);

                foreach (var invite in invites)
                {
                    senderMap.TryGetValue(invite.SenderId, out var sender);
                    var profilePictureUrl = await ResolveSignedProfilePictureUrl(storageService, sender?.Metadata);

                    result.Add(new
                    {
                        invite.Id,
                        invite.SenderId,
                        SenderUsername = sender?.Username ?? "Unknown",
                        ProfilePictureUrl = profilePictureUrl,
                        invite.CreatedAt
                    });
                }

                return result;
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
