namespace mypetpal.Services.Contracts
{
    public interface IEmailService
    {
        Task<bool> SendPasswordResetCodeAsync(string toEmail, string code);
    }
}
