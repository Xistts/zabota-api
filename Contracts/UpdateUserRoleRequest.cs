namespace Zabota.Contracts;

public record UpdateUserRoleRequest
{
    public Guid UserId { get; init; }
    public string? Role { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}