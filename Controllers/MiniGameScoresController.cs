using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Models;

namespace mypetpal.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MiniGameScoresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MiniGameScoresController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<MiniGameScore>> Get(long userId)
        {
            var score = await _context.MiniGameScores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (score == null)
            {
                return Ok(new MiniGameScore { UserId = userId });
            }

            return Ok(score);
        }

        [HttpPost("{userId}/save-the-junk")]
        public async Task<IActionResult> UpdateSaveTheJunk(long userId, [FromBody] HighScoreRequest request)
        {
            var score = await _context.MiniGameScores
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (score == null)
            {
                score = new MiniGameScore { UserId = userId, SaveTheJunkHighScore = request.Score };
                _context.MiniGameScores.Add(score);
            }
            else if (request.Score > score.SaveTheJunkHighScore)
            {
                score.SaveTheJunkHighScore = request.Score;
            }

            await _context.SaveChangesAsync();
            return Ok(new { highScore = score.SaveTheJunkHighScore });
        }
    }

    public class HighScoreRequest
    {
        public long Score { get; set; }
    }
}
