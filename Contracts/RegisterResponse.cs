namespace Zabota.Contracts;

public record RegisterResponse
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public string Message { get; init; } = string.Empty;
}
