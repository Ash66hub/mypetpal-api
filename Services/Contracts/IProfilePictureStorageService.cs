namespace mypetpal.Services.Contracts
{
    public interface IProfilePictureStorageService
    {
        Task DeleteAllForUserAsync(long userId, CancellationToken cancellationToken = default);
        Task<string> UploadAsync(long userId, Stream fileStream, string contentType, string originalFileName, CancellationToken cancellationToken = default);
        Task<string?> CreateSignedReadUrlAsync(string? storedValue, int expiresInSeconds = 3600, CancellationToken cancellationToken = default);
    }
}
