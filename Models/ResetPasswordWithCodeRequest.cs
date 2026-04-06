namespace mypetpal.Models
{
    public class ResetPasswordWithCodeRequest
    {
        public required string Email { get; set; }
        public required string Code { get; set; }
        public required string NewPassword { get; set; }
    }
}
