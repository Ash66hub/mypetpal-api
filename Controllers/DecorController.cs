using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class DecorController : ControllerBase
    {
        private readonly IDecorService _decorService;

        public DecorController(IDecorService decorService)
        {
            _decorService = decorService;
        }

        // GET: /Decor?userId={userId}
        [HttpGet]
        public async Task<IActionResult> GetDecor([FromQuery] long userId)
        {
            var decor = await _decorService.GetDecorByUserIdAsync(userId);
            return Ok(decor);
        }

        // POST: /Decor?userId={userId}
        [HttpPost]
        public async Task<IActionResult> SaveDecor([FromQuery] long userId, [FromBody] List<DecorInstance> instances)
        {
            try
            {
                await _decorService.SaveDecorForUserAsync(userId, instances);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
