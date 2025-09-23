namespace Zabota.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public FamilyRole? Role { get; set; }
    public DateTime? DateOfBirth  { get; set; }
}
