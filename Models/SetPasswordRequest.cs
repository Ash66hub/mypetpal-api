namespace mypetpal.Models
{
    public class SetPasswordRequest
    {
        public long UserId { get; set; }
        public required string NewPassword { get; set; }
    }
}