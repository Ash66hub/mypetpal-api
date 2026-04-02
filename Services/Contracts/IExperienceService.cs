namespace mypetpal.Services.Contracts
{
    public interface IExperienceService
    {
        Task<bool> TouchLastActiveAsync(long userId, CancellationToken cancellationToken = default);
    }
}
