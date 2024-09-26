using mypetpal.Models;

namespace mypetpal.Services.Contracts
{
    public interface IPetService
    {
        Task<PetAttributes> CreatePetAsync(PetAttributes petAttributes, string userId);

        Task<List<PetAttributes>> GetAllPetsAsync(string userId);

        Task<PetAttributes?> GetPetByIdAsync(string petId);

        Task<PetAttributes?> UpdatePetAsync(string petId, PetAttributes updatedPet);

        Task<bool> DeletePetAsync(string petId);
    }
}
