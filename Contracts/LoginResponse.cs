namespace Zabota.Contracts;

public class LoginResponse
{
    public Guid? Id { get; set; }
    public int Code { get; set; }
    public string Description { get; set; } = "";
    public string? RequestId { get; set; }
}
