using Zabota.Contracts;

namespace Zabota.Endpoints;

public sealed class RegisterResponse
{
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Role { get; set; }
    public bool IsVerified { get; set; } // bool пишется всегда

    public int Code { get; set; } // ← число
    public string Description { get; set; } = ""; // текст описания
    public string? RequestId { get; set; } // опционально
}
