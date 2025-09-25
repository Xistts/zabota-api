namespace Zabota.Contracts;

public sealed class RegisterResponse
{
    public Guid Id { get; set; }
    public string Login { get; set; } = default!;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Role { get; set; }
    public bool IsVerified { get; set; }
    public string Message { get; set; } = default!;
}