using System.IdentityModel.Tokens.Jwt;
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

        public AuthenticationController(IConfiguration configuration, IUserService userService)
        {
            _config = configuration;
            _userService = userService;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(User user)
        {
            IActionResult response = Unauthorized();
            var _user = await AuthenticateUser(user);

            if(_user != null && _user.UserId != null)
            {
                var token = GenerateToken();

                var refreshToken = GenerateRefreshToken();

                await _userService.SaveRefreshToken(_user.UserId, refreshToken);
                response = Ok(new { token , refreshToken});
            }
            return response;
        }

        [AllowAnonymous]
        [HttpPost("refreshToken")]
        public async Task<IActionResult> Refresh(string userId, string refreshToken)
        {
            // Validate refresh token and return new token pair
            var user = await _userService.GetUserById(userId);

            if (user == null || user.UserId == null || user.RefreshToken != refreshToken)
            {
                return Unauthorized("Invalid refresh token or user");
            }

            var newToken = GenerateToken();
            var newRefreshToken = GenerateRefreshToken();

            await _userService.SaveRefreshToken(user.UserId, newRefreshToken);

            return Ok(new { token = newToken, refreshToken = newRefreshToken });
        }


        private async Task<User?> AuthenticateUser(User user)
        {
            if(user.Username != null)
            {
                var existingUser = await _userService.GetUserByUsername(user.Username);
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

        private string GenerateToken()
        {
            var secret = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
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
    }
}
