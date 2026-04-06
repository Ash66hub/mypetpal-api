using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using mypetpal.Services.Contracts;

namespace mypetpal.Services
{
    public class ProfilePictureStorageService : IProfilePictureStorageService
    {
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ProfilePictureStorageService> _logger;

        public ProfilePictureStorageService(
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<ProfilePictureStorageService> logger)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task DeleteAllForUserAsync(long userId, CancellationToken cancellationToken = default)
        {
            ValidateSupabaseStorageConfiguration();

            var prefix = $"avatars/{userId}/";
            var objectPaths = await ListObjectPathsByPrefixAsync(prefix, cancellationToken);
            if (objectPaths.Count == 0)
            {
                return;
            }

            var client = _httpClientFactory.CreateClient();
            foreach (var objectPath in objectPaths)
            {
                var deleteUrl = BuildObjectEndpoint(objectPath);
                using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                AddSupabaseHeaders(request.Headers);

                var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var details = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Failed deleting prior avatar {ObjectPath} for user {UserId}. Status: {StatusCode}. Details: {Details}",
                        objectPath,
                        userId,
                        response.StatusCode,
                        details);
                }
            }
        }

        public async Task<string> UploadAsync(
            long userId,
            Stream fileStream,
            string contentType,
            string originalFileName,
            CancellationToken cancellationToken = default)
        {
            ValidateSupabaseStorageConfiguration();

            if (!AllowedMimeTypes.Contains(contentType))
            {
                throw new InvalidOperationException("Unsupported image type. Allowed types: JPG, PNG, WEBP.");
            }

            var extension = ResolveExtension(contentType, originalFileName);
            var objectPath = $"avatars/{userId}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{extension}";
            var uploadUrl = BuildObjectEndpoint(objectPath);

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            AddSupabaseHeaders(request.Headers);
            request.Headers.Add("x-upsert", "true");

            var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            request.Content = content;

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Supabase upload failed for user {UserId}. Status: {StatusCode}. Details: {Details}", userId, response.StatusCode, details);
                throw new InvalidOperationException("Failed to upload profile image.");
            }

            return objectPath;
        }

        public async Task<string?> CreateSignedReadUrlAsync(
            string? storedValue,
            int expiresInSeconds = 3600,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(storedValue))
            {
                return null;
            }

            if (Uri.TryCreate(storedValue, UriKind.Absolute, out _))
            {
                return storedValue;
            }

            ValidateSupabaseStorageConfiguration();

            var requestUrl = BuildSignEndpoint(storedValue);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            AddSupabaseHeaders(request.Headers);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { expiresIn = expiresInSeconds }),
                Encoding.UTF8,
                "application/json");

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Unable to create signed profile picture URL. Status: {StatusCode}. Details: {Details}", response.StatusCode, details);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var signedPath = TryGetSignedPath(jsonDoc.RootElement);

            if (string.IsNullOrWhiteSpace(signedPath))
            {
                return null;
            }

            if (signedPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return signedPath;
            }

            var baseUrl = GetSupabaseUrl().TrimEnd('/');
            if (signedPath.StartsWith("/storage/v1/", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}{signedPath}";
            }

            if (signedPath.StartsWith("/object/", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/storage/v1{signedPath}";
            }

            if (signedPath.StartsWith("object/", StringComparison.OrdinalIgnoreCase))
            {
                return $"{baseUrl}/storage/v1/{signedPath}";
            }

            return $"{baseUrl}/storage/v1/{signedPath.TrimStart('/')}";
        }

        private string BuildObjectEndpoint(string objectPath)
        {
            var baseUrl = GetSupabaseUrl().TrimEnd('/');
            var bucket = GetAvatarBucket();
            return $"{baseUrl}/storage/v1/object/{bucket}/{objectPath}";
        }

        private string BuildSignEndpoint(string objectPath)
        {
            var baseUrl = GetSupabaseUrl().TrimEnd('/');
            var bucket = GetAvatarBucket();
            return $"{baseUrl}/storage/v1/object/sign/{bucket}/{objectPath}";
        }

        private string BuildListEndpoint()
        {
            var baseUrl = GetSupabaseUrl().TrimEnd('/');
            var bucket = GetAvatarBucket();
            return $"{baseUrl}/storage/v1/object/list/{bucket}";
        }

        private void AddSupabaseHeaders(HttpRequestHeaders headers)
        {
            var serviceRole = GetServiceRoleKey();
            headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRole);
            headers.Add("apikey", serviceRole);
        }

        private void ValidateSupabaseStorageConfiguration()
        {
            _ = GetSupabaseUrl();
            _ = GetServiceRoleKey();
            _ = GetAvatarBucket();
        }

        private string GetSupabaseUrl()
        {
            return _configuration["Supabase:Url"]
                ?? throw new InvalidOperationException("Supabase:Url is not configured.");
        }

        private string GetServiceRoleKey()
        {
            return _configuration["Supabase:ServiceRoleKey"]
                ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");
        }

        private string GetAvatarBucket()
        {
            return _configuration["Supabase:AvatarBucket"]
                ?? throw new InvalidOperationException("Supabase:AvatarBucket is not configured.");
        }

        private static string ResolveExtension(string contentType, string originalFileName)
        {
            var cleanedExt = Path.GetExtension(originalFileName)?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(cleanedExt) && cleanedExt is ".jpg" or ".jpeg" or ".png" or ".webp")
            {
                return cleanedExt;
            }

            return contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        private static string? TryGetSignedPath(JsonElement root)
        {
            if (root.TryGetProperty("signedURL", out var signedUrlValue) && signedUrlValue.ValueKind == JsonValueKind.String)
            {
                return signedUrlValue.GetString();
            }

            if (root.TryGetProperty("signedUrl", out var signedUrlAltValue) && signedUrlAltValue.ValueKind == JsonValueKind.String)
            {
                return signedUrlAltValue.GetString();
            }

            return null;
        }

        private async Task<List<string>> ListObjectPathsByPrefixAsync(string prefix, CancellationToken cancellationToken)
        {
            var requestUrl = BuildListEndpoint();
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            AddSupabaseHeaders(request.Headers);

            var payload = new
            {
                prefix,
                limit = 1000,
                offset = 0,
                sortBy = new { column = "name", order = "asc" }
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Failed to list existing avatars for prefix {Prefix}. Status: {StatusCode}. Details: {Details}",
                    prefix,
                    response.StatusCode,
                    details);
                return new List<string>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var jsonDoc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<string>();
            }

            var objectPaths = new List<string>();
            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var fileName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                objectPaths.Add($"{prefix}{fileName}");
            }

            return objectPaths;
        }
    }
}
