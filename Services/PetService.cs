using Microsoft.EntityFrameworkCore;
using mypetpal.dbContext;
using mypetpal.Models;
using mypetpal.Services.Contracts;
using static mypetpal.Data.Common.Enums.PetEnums;

namespace mypetpal.Services
{
    public class PetService : IPetService
    {
        private readonly ApplicationDbContext _context;

        public PetService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PetAttributes> CreatePetAsync(PetAttributes petAttributes, long userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found.");
            }

            var newPet = new PetAttributes
            {
                PetName = petAttributes.PetName,
                PetType = petAttributes.PetType,
                PetLevel = 1,
                Age = petAttributes.Age,
                PetStatus = PetStatus.Neutral,
                Health = 100,
                Happiness = 50,
                Xp = 0
            };

            var petMetadata = new PetMetadata
            {
                Metadata_createdUtc = DateTime.UtcNow,
                Metadata_updatedUtc = DateTime.UtcNow
            };
            newPet.SetPetMetadata(petMetadata);

            _context.PetAttributes.Add(newPet);
            await _context.SaveChangesAsync();  

        
            var userPet = new UserPet
            {
                PetId = newPet.PetId, 
                UserId = userId,
                PetAttributes = newPet
            };

            // Add the UserPet relationship
            _context.UserPets.Add(userPet);
            await _context.SaveChangesAsync();

            return newPet;
        }

        public async Task<List<PetAttributes>> GetAllPetsAsync(long userId)
        {
            return await _context.UserPets
                .Where(up => up.UserId == userId)
                .Include(up => up.PetAttributes)
                .Select(up => up.PetAttributes)
                .ToListAsync();
        }

        public async Task<PetAttributes?> GetPetByIdAsync(long petId)
        {
            return await _context.PetAttributes.FindAsync(petId);
        }

        public async Task<PetAttributes?> UpdatePetAsync(long petId, PetAttributes updatedPet)
        {
            var pet = await _context.PetAttributes.FindAsync(petId);
            if (pet == null)
            {
                return null;
            }
            
            if (!string.IsNullOrEmpty(updatedPet.PetName))
            {
                pet.PetName = updatedPet.PetName;
            }

            if (updatedPet.Age > 0)
            {
                pet.Age = updatedPet.Age;
            }

            if (updatedPet.Health >= 0)
            {
                pet.Health = updatedPet.Health;
            }

            pet.Happiness = updatedPet.Happiness;
            pet.PetStatus = GetPetStatusBasedOnHappiness(pet.Happiness);
            pet.Xp = updatedPet.Xp;
            pet.PetLevel = CalculatePetLevel(pet.Xp);

            var metadata = pet.GetPetMetadata() ?? new PetMetadata(); 
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            pet.SetPetMetadata(metadata);

            await _context.SaveChangesAsync();
            return pet;
        }

        public async Task<bool> DeletePetAsync(long petId)
        {
            var pet = await _context.PetAttributes.FindAsync(petId);
            if (pet == null)
            {
                return false;
            }

            var userPet = await _context.UserPets.FirstOrDefaultAsync(up => up.PetId == petId);
            if (userPet != null)
            {
                _context.UserPets.Remove(userPet);
            }

            _context.PetAttributes.Remove(pet);
            await _context.SaveChangesAsync();

            return true;
        }

        private static PetStatus GetPetStatusBasedOnHappiness(int happiness)
        {
            if (happiness > 70)
            {
                return PetStatus.Happy;
            }
            if (happiness > 40)
            {
                return PetStatus.Neutral;
            }
            if (happiness == 0)
            {
                return PetStatus.Dead;
            }
            return PetStatus.Sad;
        }

        private static int CalculatePetLevel(int xp)
        {
            int baseXp = 10; // XP for level 2
            if (xp < baseXp)
            {
                return 1;
            }

            return (int)Math.Floor(Math.Log(xp / baseXp, 2)) + 2;
        }
    }
}
