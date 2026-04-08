using Microsoft.AspNetCore.Mvc;
using mypetpal.Services.Contracts;
using mypetpal.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace mypetpal.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IExperienceService _experienceService;

        private UserDto MapToDto(User user, bool includeEmail = false)
        {
            return new UserDto
            {
                UserId = user.UserId,
                Id = user.Id,
                Username = user.Username,
                ProfilePictureUrl = user.ProfilePictureUrl,
                CurrentLevel = user.CurrentLevel,
                TotalExperience = user.TotalExperience,
                LastActive = user.LastActive,
                Email = includeEmail ? user.Email : null,
                AuthProvider = user.AuthProvider,
                HasLocalPassword = user.HasLocalPassword
            };
        }

        public UsersController(IUserService userService, IExperienceService experienceService)
        {
            _userService = userService;
            _experienceService = experienceService;
        }

        // POST: User (Create new user)
        [AllowAnonymous]
        [HttpPost("signup")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (!IsPasswordValid(user.Password))
            {
                return BadRequest("Password must be at least 12 characters and include both letters and numbers.");
            }

            try
            {
                var createdUser = await _userService.CreateNewUser(user.Username, user.Email, user.Password);
                return CreatedAtAction(nameof(GetUserById), new { userId = createdUser.UserId }, MapToDto(createdUser, true));
            }
            catch (ArgumentException ex)
            {
                return Conflict(ex.Message); // Handle conflict if username or email already exists
            }
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

        // GET: User 
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsers();
            var dtos = users.Select(u => MapToDto(u, false));
            return Ok(dtos);
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10)
        {
            var leaderboard = await _userService.GetLeaderboard(top);
            return Ok(leaderboard);
        }

        // GET: User/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserById(long userId)
        {
            var user = await _userService.GetUserById(userId);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(user, false));
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetUserByStringId(string id)
        {
            var user = await _userService.GetUserById(id);

            if (user == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(user, false));
        }

        // PATCH: User/{userId} (Edit username, email or password)
        [HttpPatch("{userId}")]
        public async Task<IActionResult> UpdateUser(long userId, [FromBody] User updatedUser)
        {
            try
            {
                if(updatedUser.Username != null && updatedUser.Email != null)
                {
                    var updated = await _userService.UpdateUser(userId, updatedUser.Username, updatedUser.Email, updatedUser.Password);
                    return Ok(MapToDto(updated, true));
                }
                else
                {
                    return NotFound();
                }
                
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("{userId}/profile-picture")]
        public async Task<IActionResult> UpdateProfilePicture(long userId, IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Profile image file is required.");
            }

            try
            {
                var updated = await _userService.UpdateProfilePicture(userId, file);
                return Ok(MapToDto(updated, true));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: User/{userId}
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(long userId)
        {
            var success = await _userService.DeleteUser(userId);

            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("activity")]
        public async Task<IActionResult> UpdateActivity([FromQuery] long? userId, [FromQuery] string? id)
        {
            long resolvedUserId;

            if (userId.HasValue)
            {
                resolvedUserId = userId.Value;
            }
            else if (!string.IsNullOrWhiteSpace(id))
            {
                var user = await _userService.GetUserById(id);
                if (user == null)
                {
                    return NotFound();
                }

                resolvedUserId = user.UserId;
            }
            else
            {
                return BadRequest("Provide either userId or id.");
            }

            var updated = await _experienceService.TouchLastActiveAsync(resolvedUserId);
            if (!updated)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}
