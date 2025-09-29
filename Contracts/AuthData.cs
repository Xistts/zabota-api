namespace Zabota.Contracts;

public sealed class AuthData
{
    public object User { get; set; } = default!;
    public string Token { get; set; } = default!;
    public DateTime TokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpiresAt { get; set; }
}
