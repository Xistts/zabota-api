using Zabota.Contracts;

namespace Zabota.Endpoints;

public class RegisterResponse : BaseResponse
{

    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Role { get; set; }
    public bool IsVerified { get; set; }
    
}
