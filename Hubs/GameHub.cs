using Microsoft.AspNetCore.SignalR;
using mypetpal.Services.Contracts;

namespace mypetpal.Hubs
{
    public class GameHub : Hub
    {
        private readonly IExperienceService _experienceService;

        public GameHub(IExperienceService experienceService)
        {
            _experienceService = experienceService;
        }

        public async Task JoinPlayerGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }

        public async Task LeavePlayerGroup(string userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        }

        public async Task UpdateLastActive(string userId)
        {
            if (!long.TryParse(userId, out var parsedUserId))
            {
                return;
            }

            await _experienceService.TouchLastActiveAsync(parsedUserId);
        }
    }
}
