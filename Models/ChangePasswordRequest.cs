namespace mypetpal.Models
{
    public class ChangePasswordRequest
    {
        public long UserId { get; set; }
        public required string OldPassword { get; set; }
        public required string NewPassword { get; set; }
    }
}
