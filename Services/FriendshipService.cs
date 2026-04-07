using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Hubs;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Services
{
    public class FriendshipService : IFriendshipService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<GameHub> _gameHubContext;

        public FriendshipService(ApplicationDbContext db, IHubContext<GameHub> gameHubContext)
        {
            _db = db;
            _gameHubContext = gameHubContext;
        }

        public async Task<FriendshipRespondResult> RespondToRequestAsync(long requestId, bool accept, CancellationToken cancellationToken = default)
        {
            var request = await _db.Friendships.FindAsync([requestId], cancellationToken);
            if (request == null)
            {
                return new FriendshipRespondResult { IsNotFound = true };
            }

            if (!accept)
            {
                _db.Friendships.Remove(request);
                await _db.SaveChangesAsync(cancellationToken);
                return new FriendshipRespondResult
                {
                    IsAccepted = false,
                    RequesterUserId = request.UserId,
                    ReceiverUserId = request.FriendId
                };
            }

            var countU = await _db.Friendships.CountAsync(f =>
                (f.UserId == request.UserId || f.FriendId == request.UserId) && f.Status == FriendshipStatus.Accepted,
                cancellationToken);
            var countF = await _db.Friendships.CountAsync(f =>
                (f.UserId == request.FriendId || f.FriendId == request.FriendId) && f.Status == FriendshipStatus.Accepted,
                cancellationToken);

            if (countU >= 25 || countF >= 25)
            {
                return new FriendshipRespondResult { IsFriendLimitReached = true };
            }

            var requester = await _db.Users.FindAsync([request.UserId], cancellationToken);
            var receiver = await _db.Users.FindAsync([request.FriendId], cancellationToken);
            if (requester == null || receiver == null)
            {
                return new FriendshipRespondResult { IsNotFound = true };
            }

            var requesterOldLevel = User.CalculateLevel(requester.TotalExperience);
            var receiverOldLevel = User.CalculateLevel(receiver.TotalExperience);

            request.Status = FriendshipStatus.Accepted;
            request.UpdatedAt = DateTime.UtcNow;

            // Friendship confirmation grants +25 EXP to both players.
            requester.TotalExperience += 25;
            receiver.TotalExperience += 25;
            requester.LastActive = DateTime.UtcNow;
            receiver.LastActive = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            await _gameHubContext.Clients.Group(requester.UserId.ToString())
                .SendAsync("ExperienceUpdated", requester.UserId.ToString(), requester.TotalExperience, cancellationToken);
            await _gameHubContext.Clients.Group(receiver.UserId.ToString())
                .SendAsync("ExperienceUpdated", receiver.UserId.ToString(), receiver.TotalExperience, cancellationToken);

            var requesterNewLevel = User.CalculateLevel(requester.TotalExperience);
            var receiverNewLevel = User.CalculateLevel(receiver.TotalExperience);

            if (requesterNewLevel > requesterOldLevel)
            {
                await _gameHubContext.Clients.Group(requester.UserId.ToString())
                    .SendAsync("LeveledUp", requester.UserId.ToString(), requesterNewLevel, requester.TotalExperience, cancellationToken);
            }

            if (receiverNewLevel > receiverOldLevel)
            {
                await _gameHubContext.Clients.Group(receiver.UserId.ToString())
                    .SendAsync("LeveledUp", receiver.UserId.ToString(), receiverNewLevel, receiver.TotalExperience, cancellationToken);
            }

            return new FriendshipRespondResult
            {
                IsAccepted = true,
                RequesterUserId = request.UserId,
                ReceiverUserId = request.FriendId,
                ReceiverUsername = receiver.Username
            };
        }
    }
}
