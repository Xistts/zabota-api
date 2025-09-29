namespace Zabota.Contracts;

public class LoginResponse : BaseResponse
{
    // при успехе
    public string? Token { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // заголовок кода (по твоему примеру)
    public string CodeTitle { get; set; } = "";
}
