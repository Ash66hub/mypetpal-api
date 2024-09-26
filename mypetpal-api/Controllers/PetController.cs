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

        public PetsController(IPetService petService)
        {
            _petService = petService;
        }

        // POST: /Pets 
        [HttpPost]
        public async Task<IActionResult> CreatePet([FromBody] PetAttributes petAttributes, [FromQuery] string userId)
        {
            try
            {
                var newPet = await _petService.CreatePetAsync(petAttributes, userId);
                return CreatedAtAction(nameof(GetPetById), new { petId = newPet.PetId }, newPet);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: /Pets?userId={userId}
        [HttpGet]
        public async Task<IActionResult> GetAllPets([FromQuery] string userId)
        {
            var pets = await _petService.GetAllPetsAsync(userId);
            if (!pets.Any())
            {
                return NotFound("No pets found for the user.");
            }

            return Ok(pets);
        }

        // GET: /Pets/{petId}
        [HttpGet("{petId}")]
        public async Task<IActionResult> GetPetById(string petId)
        {
            var pet = await _petService.GetPetByIdAsync(petId);
            if (pet == null)
            {
                return NotFound();
            }
            return Ok(pet);
        }

        // PATCH: /Pets/{petId} (Update pet attributes)
        [HttpPatch("{petId}")]
        public async Task<IActionResult> UpdatePet(string petId, [FromBody] PetAttributes updatedPet)
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
        public async Task<IActionResult> DeletePet(string petId)
        {
            var result = await _petService.DeletePetAsync(petId);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
