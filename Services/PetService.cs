using Microsoft.EntityFrameworkCore;
using mypetpal.Data.Common;
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

            var selectedPetAssetKey = NormalizePetAssetKey(
                petAttributes.Selection?.PetAssetKey ?? petAttributes.PetAvatar ?? petAttributes.PetType.ToString()
            );
            var selectedRoomKey = NormalizeRoomKey(petAttributes.Selection?.RoomKey);

            var newPet = new PetAttributes
            {
                PublicId = await GenerateUniquePetPublicIdAsync(),
                PetName = petAttributes.PetName,
                PetType = ParsePetType(selectedPetAssetKey),
                PetLevel = 1,
                Age = petAttributes.Age,
                PetStatus = PetStatus.Neutral,
                Health = 100,
                Happiness = 50,
                Xp = 0,
                PetAvatar = selectedPetAssetKey,
                Selection = new PetSelection
                {
                    PetAssetKey = selectedPetAssetKey,
                    RoomKey = selectedRoomKey
                }
            };

            var petMetadata = new PetMetadata
            {
                SelectedPetAssetKey = selectedPetAssetKey,
                SelectedRoomKey = selectedRoomKey,
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

            HydrateSelection(newPet);
            return newPet;
        }

        public async Task<List<PetAttributes>> GetAllPetsAsync(long userId)
        {
            var pets = await _context.UserPets
                .Where(up => up.UserId == userId)
                .Include(up => up.PetAttributes)
                .Select(up => up.PetAttributes)
                .ToListAsync();

            pets.ForEach(HydrateSelection);
            return pets;
        }

        public async Task<PetAttributes?> GetPetByIdAsync(long petId)
        {
            var pet = await _context.PetAttributes.FindAsync(petId);
            HydrateSelection(pet);
            return pet;
        }

        public async Task<PetAttributes?> GetPetByPublicIdAsync(string petPublicId)
        {
            var pet = await _context.PetAttributes
                .FirstOrDefaultAsync(p => p.PublicId == petPublicId);
            HydrateSelection(pet);
            return pet;
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

            var selectedPetAssetKey = NormalizePetAssetKey(
                updatedPet.Selection?.PetAssetKey ?? updatedPet.PetAvatar ?? pet.PetAvatar ?? pet.PetType.ToString()
            );
            var selectedRoomKey = NormalizeRoomKey(
                updatedPet.Selection?.RoomKey ?? GetPetMetadata(pet)?.SelectedRoomKey
            );

            pet.PetType = ParsePetType(selectedPetAssetKey);
            pet.PetAvatar = selectedPetAssetKey;
            pet.Selection = new PetSelection
            {
                PetAssetKey = selectedPetAssetKey,
                RoomKey = selectedRoomKey
            };

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
            metadata.SelectedPetAssetKey = selectedPetAssetKey;
            metadata.SelectedRoomKey = selectedRoomKey;
            metadata.Metadata_updatedUtc = DateTime.UtcNow;
            pet.SetPetMetadata(metadata);

            await _context.SaveChangesAsync();
            HydrateSelection(pet);
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

        private static PetMetadata? GetPetMetadata(PetAttributes? pet)
        {
            return pet?.GetPetMetadata();
        }

        private static void HydrateSelection(PetAttributes? pet)
        {
            if (pet == null)
            {
                return;
            }

            var metadata = pet.GetPetMetadata();
            var selectedPetAssetKey = NormalizePetAssetKey(
                metadata?.SelectedPetAssetKey ?? pet.PetAvatar ?? pet.PetType.ToString()
            );
            var selectedRoomKey = NormalizeRoomKey(metadata?.SelectedRoomKey);

            pet.PetType = ParsePetType(selectedPetAssetKey);
            pet.PetAvatar = selectedPetAssetKey;
            pet.Selection = new PetSelection
            {
                PetAssetKey = selectedPetAssetKey,
                RoomKey = selectedRoomKey
            };
        }

        private static string NormalizePetAssetKey(string? petAssetKey)
        {
            return petAssetKey switch
            {
                nameof(PetTypes.GoldenRetriever_spritesheet) => nameof(PetTypes.GoldenRetriever_spritesheet),
                nameof(PetTypes.Cat_spritesheet) => nameof(PetTypes.Cat_spritesheet),
                _ => nameof(PetTypes.GoldenRetriever_spritesheet)
            };
        }

        private static string NormalizeRoomKey(string? roomKey)
        {
            return roomKey switch
            {
                "room2" => "room2",
                "room3" => "room3",
                _ => "room1"
            };
        }

        private static PetTypes ParsePetType(string? petAssetKey)
        {
            return petAssetKey switch
            {
                nameof(PetTypes.Cat_spritesheet) => PetTypes.Cat_spritesheet,
                _ => PetTypes.GoldenRetriever_spritesheet
            };
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

        private async Task<string> GenerateUniquePetPublicIdAsync()
        {
            string id;
            do
            {
                id = PublicIdGenerator.NewId();
            }
            while (await _context.PetAttributes.AnyAsync(p => p.PublicId == id));

            return id;
        }
    }
}
