using Microsoft.AspNetCore.Mvc;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserSettingsController : ControllerBase
    {
        private readonly IUserSettingsService _settingsService;

        public UserSettingsController(IUserSettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<UserSettings>> Get(long userId)
        {
            var settings = await _settingsService.GetSettingsAsync(userId);
            return Ok(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Post(UserSettings settings)
        {
            await _settingsService.UpdateSettingsAsync(settings);
            return Ok();
        }
    }
}
