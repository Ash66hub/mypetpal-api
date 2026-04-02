namespace mypetpal.Services.Contracts
{
    public interface IFriendshipService
    {
        Task<FriendshipRespondResult> RespondToRequestAsync(long requestId, bool accept, CancellationToken cancellationToken = default);
    }

    public sealed class FriendshipRespondResult
    {
        public bool IsNotFound { get; set; }
        public bool IsFriendLimitReached { get; set; }
        public bool IsAccepted { get; set; }
        public long RequesterUserId { get; set; }
        public long ReceiverUserId { get; set; }
        public string? ReceiverUsername { get; set; }
    }
}
