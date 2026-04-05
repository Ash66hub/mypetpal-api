using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mypetpal.Models;
using mypetpal.Services.Contracts;

namespace mypetpal.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class PetsController : ControllerBase
    {
        private readonly IPetService _petService;
        private readonly IUserService _userService;

        public PetsController(IPetService petService, IUserService userService)
        {
            _petService = petService;
            _userService = userService;
        }

        // POST: /Pets 
        [HttpPost]
        public async Task<IActionResult> CreatePet(
            [FromBody] PetAttributes petAttributes,
            [FromQuery] long? userId,
            [FromQuery] string? userPublicId)
        {
            try
            {
                var resolvedUserId = await ResolveUserId(userId, userPublicId);
                if (resolvedUserId == null)
                {
                    return BadRequest("Provide either userId or userPublicId.");
                }

                var newPet = await _petService.CreatePetAsync(petAttributes, resolvedUserId.Value);
                return CreatedAtAction(nameof(GetPetById), new { petId = newPet.PetId }, newPet);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: /Pets?userId={userId}
        [HttpGet]
        public async Task<IActionResult> GetAllPets([FromQuery] long? userId, [FromQuery] string? userPublicId)
        {
            var resolvedUserId = await ResolveUserId(userId, userPublicId);
            if (resolvedUserId == null)
            {
                return BadRequest("Provide either userId or userPublicId.");
            }

            var pets = await _petService.GetAllPetsAsync(resolvedUserId.Value);
            return Ok(pets);
        }

        // GET: /Pets/{petId}
        [HttpGet("{petId}")]
        public async Task<IActionResult> GetPetById(long petId)
        {
            var pet = await _petService.GetPetByIdAsync(petId);
            if (pet == null)
            {
                return NotFound();
            }
            return Ok(pet);
        }

        // GET: /Pets/public/{petPublicId}
        [HttpGet("public/{petPublicId}")]
        public async Task<IActionResult> GetPetByPublicId(string petPublicId)
        {
            var pet = await _petService.GetPetByPublicIdAsync(petPublicId);
            if (pet == null)
            {
                return NotFound();
            }
            return Ok(pet);
        }

        // PATCH: /Pets/{petId} (Update pet attributes)
        [HttpPatch("{petId}")]
        public async Task<IActionResult> UpdatePet(long petId, [FromBody] PetAttributes updatedPet)
        {
            var pet = await _petService.UpdatePetAsync(petId, updatedPet);
            if (pet == null)
            {
                return NotFound();
            }

            return Ok(pet);
        }

        // DELETE: /Pets/{petId}
        [HttpDelete("{petId}")]
        public async Task<IActionResult> DeletePet(long petId)
        {
            var result = await _petService.DeletePetAsync(petId);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private async Task<long?> ResolveUserId(long? userId, string? userPublicId)
        {
            if (userId.HasValue)
            {
                return userId.Value;
            }

            if (string.IsNullOrWhiteSpace(userPublicId))
            {
                return null;
            }

            var user = await _userService.GetUserByPublicId(userPublicId);
            return user?.UserId;
        }
    }
}
