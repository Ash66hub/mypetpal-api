namespace mypetpal.Services.Contracts
{
    public interface IPasswordResetCodeStore
    {
        Task StoreCodeAsync(string email, string code, TimeSpan ttl);
        bool VerifyCode(string email, string code);
        void RemoveCode(string email);
    }
}
