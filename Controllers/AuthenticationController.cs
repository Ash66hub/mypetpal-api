using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Controllers
{
    [Route("[controller]")]
    [ApiController]

    public class AuthenticationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IPasswordResetCodeStore _passwordResetCodeStore;
        private readonly ILogger<AuthenticationController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthenticationController(
            IConfiguration configuration,
            IUserService userService,
            IEmailService emailService,
            IPasswordResetCodeStore passwordResetCodeStore,
            ILogger<AuthenticationController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = configuration;
            _userService = userService;
            _emailService = emailService;
            _passwordResetCodeStore = passwordResetCodeStore;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(User user)
        {
            _logger.LogInformation("Login attempt for user: {Username},{Email}", user.Username, user.Email);

            IActionResult response = Unauthorized();
            var _user = await AuthenticateUser(user);

            if (_user != null)
            {
                try
                {
                    var token = GenerateToken();
                    var refreshToken = GenerateRefreshToken();
                    await _userService.SaveRefreshToken(_user.UserId, refreshToken);

                    _logger.LogInformation("Authentication successful for user: {Username}", _user.Username);
                    response = Ok(new { token, refreshToken, userId = _user.UserId, id = _user.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while generating tokens for user: {Username}", _user.Username);
                    response = StatusCode(500, "Internal server error");
                }
            }
            else
            {
                _logger.LogWarning("Authentication failed for user: {Username}", user.Username);
            }

            return response;
        }

        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.RefreshToken))
                return BadRequest("Invalid request.");

            if (!long.TryParse(request.UserId, out var userId))
                return BadRequest("Invalid userId.");

            // Validate refresh token and return new token pair
            var user = await _userService.GetUserById(userId);

            if (user == null || user.RefreshToken != request.RefreshToken)
            {
                return Unauthorized("Invalid refresh token or user");
            }

            var newToken = GenerateToken();
            var newRefreshToken = GenerateRefreshToken();

            await _userService.SaveRefreshToken(user.UserId, newRefreshToken);

            return Ok(new { token = newToken, refreshToken = newRefreshToken });
        }

        [AllowAnonymous]
        [HttpPost("google-signin")]
        public async Task<IActionResult> GoogleSignIn([FromBody] SocialSignInRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccessToken))
            {
                return BadRequest("Missing Supabase access token.");
            }

            var supabaseUser = await GetSupabaseUser(request.AccessToken);
            if (supabaseUser == null || string.IsNullOrWhiteSpace(supabaseUser.Email))
            {
                return Unauthorized("Invalid or expired Supabase session.");
            }

            if (!string.Equals(supabaseUser.AppMetadata?.Provider, "google", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("This endpoint supports Google sign-in only.");
            }

            var email = supabaseUser.Email.Trim();
            var user = await _userService.GetUserByEmail(email);
            var isFirstLogin = user == null;

            if (user == null)
            {
                var preferredUsername = ResolvePreferredUsername(supabaseUser, email);
                var uniqueUsername = await BuildUniqueUsername(preferredUsername);
                var randomPassword = GenerateRandomPassword();

                user = await _userService.CreateNewUser(uniqueUsername, email, randomPassword);
            }

            var metadata = user.GetUserMetadata() ?? new UserMetadata();
            metadata.Provider = "Google";
            metadata.ProviderUserId = supabaseUser.Id;
            metadata.HasLocalPassword = isFirstLogin ? false : (metadata.HasLocalPassword ?? false);
            metadata.LastLogin = DateTime.UtcNow;
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            if (isFirstLogin && metadata.Metadata_createdUtc == null)
            {
                metadata.Metadata_createdUtc = DateTime.UtcNow;
            }

            await _userService.UpdateUserMetadata(user.UserId, metadata);

            var token = GenerateToken();
            var refreshToken = GenerateRefreshToken();
            await _userService.SaveRefreshToken(user.UserId, refreshToken);

            return Ok(new { token, refreshToken, userId = user.UserId, id = user.Id });
        }
        
        public class RefreshRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
        }

        public class SocialSignInRequest
        {
            public string AccessToken { get; set; } = string.Empty;
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!IsPasswordValid(request.NewPassword))
            {
                return BadRequest("Password must be at least 12 characters and include both letters and numbers.");
            }

            var user = await _userService.GetUserById(request.UserId);
            var metadata = user?.GetUserMetadata();
            var hasLocalPassword = metadata?.HasLocalPassword
                ?? !string.Equals(metadata?.Provider, "Google", StringComparison.OrdinalIgnoreCase);

            if (user == null)
            {
                return BadRequest("Invalid credentials.");
            }

            if (!hasLocalPassword)
            {
                return BadRequest("This account uses Google sign-in. Set a password first.");
            }

            if (user == null || !VerifyPassword(request.OldPassword, user.Password))
                return BadRequest("Invalid credentials.");

            await _userService.UpdateUser(
                request.UserId,
                user.Username ?? string.Empty,
                user.Email ?? string.Empty,
                request.NewPassword);
            return Ok(new { message = "Password updated successfully." });
        }

        [Authorize]
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
        {
            if (!IsPasswordValid(request.NewPassword))
            {
                return BadRequest("Password must be at least 12 characters and include both letters and numbers.");
            }

            var user = await _userService.GetUserById(request.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var metadata = user.GetUserMetadata() ?? new UserMetadata();
            var provider = metadata.Provider;

            if (!string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Set password is only for social login accounts.");
            }

            await _userService.UpdateUser(
                request.UserId,
                user.Username ?? string.Empty,
                user.Email ?? string.Empty,
                request.NewPassword);

            metadata.HasLocalPassword = true;
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            await _userService.UpdateUserMetadata(request.UserId, metadata);

            return Ok(new { message = "Password set successfully." });
        }

        [Authorize]
        [HttpDelete("delete-account/{userId}")]
        public async Task<IActionResult> DeleteAccount(long userId)
        {
            var success = await _userService.DeleteUser(userId);
            if (!success) return NotFound("User not found.");

            return Ok(new { message = "Account deleted successfully." });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password/request")]
        public async Task<IActionResult> RequestPasswordResetCode([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest("Email is required.");
            }

            var email = request.Email.Trim();
            var user = await _userService.GetUserByEmail(email);

            if (user == null)
            {
                // Always return success to avoid leaking account existence.
                return Ok(new
                {
                    message = "If an account exists for this email, a reset code has been sent."
                });
            }

            var code = GeneratePasswordResetCode();
            await _passwordResetCodeStore.StoreCodeAsync(email, code, TimeSpan.FromMinutes(15));

            var emailSent = await _emailService.SendPasswordResetCodeAsync(email, code);
            if (!emailSent)
            {
                return StatusCode(500, "Unable to send reset email right now. Please try again.");
            }

            return Ok(new
            {
                message = "If an account exists for this email, a reset code has been sent."
            });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPasswordWithCode([FromBody] ResetPasswordWithCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest("Email and reset code are required.");
            }

            if (!IsPasswordValid(request.NewPassword))
            {
                return BadRequest("Password must be at least 12 characters and include both letters and numbers.");
            }

            var email = request.Email.Trim();
            var code = request.Code.Trim();

            var user = await _userService.GetUserByEmail(email);
            if (user == null || !_passwordResetCodeStore.VerifyCode(email, code))
            {
                return BadRequest("Invalid or expired reset code.");
            }

            await _userService.UpdateUser(
                user.UserId,
                user.Username ?? string.Empty,
                user.Email ?? string.Empty,
                request.NewPassword);

            var metadata = user.GetUserMetadata() ?? new UserMetadata();
            metadata.HasLocalPassword = true;
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            await _userService.UpdateUserMetadata(user.UserId, metadata);

            _passwordResetCodeStore.RemoveCode(email);

            return Ok(new { message = "Password reset successful." });
        }

        private async Task<User?> AuthenticateUser(User user)
        {
            if(user.Username != null || user.Email !=null)
            {
                User? existingUser = null;

                if(user.Email != null)
                {
                    existingUser = await _userService.GetUserByEmail(user.Email);
                }
                else if(user.Username != null)
                {
                    existingUser = await _userService.GetUserByUsername(user.Username);
                }
               

                if (existingUser != null && VerifyPassword(user.Password, existingUser.Password))
                {
                    return existingUser;
                }
            }
            return null;
        }

        private static bool VerifyPassword(string inputPassword, string storedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(inputPassword, storedPassword); 
        }

        private static bool IsPasswordValid(string? password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            {
                return false;
            }

            var hasLetter = password.Any(char.IsLetter);
            var hasDigit = password.Any(char.IsDigit);
            return hasLetter && hasDigit;
        }

        private string GenerateToken()
        {
            var secret = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(secret, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(_config["Jwt:Issuer"], _config["Jwt:Audience"], null,
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: credentials
                );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        private async Task<SupabaseUserResponse?> GetSupabaseUser(string accessToken)
        {
            var supabaseUrl = _config["Supabase:Url"];
            if (string.IsNullOrWhiteSpace(supabaseUrl))
            {
                _logger.LogError("Supabase:Url is not configured.");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{supabaseUrl.TrimEnd('/')}/auth/v1/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var anonKey = _config["Supabase:AnonKey"];
            if (!string.IsNullOrWhiteSpace(anonKey))
            {
                request.Headers.Add("apikey", anonKey);
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Supabase user lookup failed with status code: {StatusCode}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<SupabaseUserResponse>(stream);
        }

        private static string ResolvePreferredUsername(SupabaseUserResponse supabaseUser, string email)
        {
            var displayName = GetMetadataValue(supabaseUser.UserMetadata, "name");
            var fullName = GetMetadataValue(supabaseUser.UserMetadata, "full_name");
            var emailPrefix = email.Split('@')[0];

            return NormalizeUsername(displayName ?? fullName ?? emailPrefix);
        }

        private static string? GetMetadataValue(Dictionary<string, JsonElement>? metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private async Task<string> BuildUniqueUsername(string preferredUsername)
        {
            var baseUsername = NormalizeUsername(preferredUsername);
            var candidate = baseUsername;
            var suffix = 1;

            while (await _userService.GetUserByUsername(candidate) != null)
            {
                var suffixValue = suffix.ToString();
                var maxBaseLength = Math.Max(1, 30 - suffixValue.Length);
                var trimmedBase = baseUsername.Length > maxBaseLength
                    ? baseUsername[..maxBaseLength]
                    : baseUsername;

                candidate = $"{trimmedBase}{suffixValue}";
                suffix++;
            }

            return candidate;
        }

        private static string NormalizeUsername(string? rawValue)
        {
            var input = string.IsNullOrWhiteSpace(rawValue) ? "petpaluser" : rawValue.Trim();
            var filtered = new string(input
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '.')
                .ToArray());

            if (string.IsNullOrWhiteSpace(filtered))
            {
                filtered = "petpaluser";
            }

            if (filtered.Length > 30)
            {
                filtered = filtered[..30];
            }

            if (filtered.Length < 5)
            {
                filtered = filtered.PadRight(5, '0');
            }

            return filtered;
        }

        private static string GenerateRandomPassword()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        private static string GeneratePasswordResetCode()
        {
            return RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        }

        public class SupabaseUserResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("app_metadata")]
            public SupabaseAppMetadata? AppMetadata { get; set; }

            [JsonPropertyName("user_metadata")]
            public Dictionary<string, JsonElement>? UserMetadata { get; set; }
        }

        public class SupabaseAppMetadata
        {
            [JsonPropertyName("provider")]
            public string? Provider { get; set; }
        }
    }
}
