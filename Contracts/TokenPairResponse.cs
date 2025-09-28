namespace Zabota.Contracts;

public sealed class TokenPairResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpiresAtUtc { get; set; }
}
