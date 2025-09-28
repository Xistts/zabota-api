namespace Zabota.Contracts;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = default!;
}
