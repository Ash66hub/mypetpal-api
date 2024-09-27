using mypetpal.Models;

namespace mypetpal.Services.Contracts
{
    public interface IPetService
    {
        Task<PetAttributes> CreatePetAsync(PetAttributes petAttributes, long userId);

        Task<List<PetAttributes>> GetAllPetsAsync(long userId);

        Task<PetAttributes?> GetPetByIdAsync(long petId);

        Task<PetAttributes?> UpdatePetAsync(long petId, PetAttributes updatedPet);

        Task<bool> DeletePetAsync(long petId);
    }
}
