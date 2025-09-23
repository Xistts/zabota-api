namespace Zabota.Contracts;

public record UpdateUserRoleRequest
{
    public int UserId { get; init; }
    public FamilyRole? Role { get; init; }
    public DateTime? DateOfBirth { get; init; }
}