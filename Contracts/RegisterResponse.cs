using Zabota.Contracts;

namespace Zabota.Endpoints;

public sealed class RegisterResponse
{
    // user
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Role { get; set; }
    public bool IsVerified { get; set; }

    // tokens
    public string Token { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }

    // meta
    public int Code { get; set; }          // 0 = Ok
    public string CodeTitle { get; set; } = "Ok";
    public string Description { get; set; } = "";
    public string? RequestId { get; set; }
}
