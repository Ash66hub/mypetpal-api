using Microsoft.AspNetCore.Mvc;
using mypetpal.Services.Contracts;
using mypetpal.Models;
using Microsoft.AspNetCore.Authorization;

namespace mypetpal.Controllers
{
    //[Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // POST: User (Create new user)
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            try
            {
                var createdUser = await _userService.CreateNewUser(user.Username, user.Email, user.Password);
                return CreatedAtAction(nameof(GetUserById), new { userId = createdUser.UserId }, createdUser);
            }
            catch (ArgumentException ex)
            {
                return Conflict(ex.Message); // Handle conflict if username or email already exists
            }
        }

        // GET: User 
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsers();
            return Ok(users);
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

            return Ok(user);
        }

        // PATCH: User/{userId} (Edit username, email or password)
        [HttpPatch("{userId}")]
        public async Task<IActionResult> UpdateUser(long userId, [FromBody] User updatedUser)
        {
            try
            {
                var updated = await _userService.UpdateUser(userId, updatedUser.Username, updatedUser.Email, updatedUser.Password);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
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
    }
}
