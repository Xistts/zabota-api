namespace Zabota.Contracts;

public record LoginResponse
{
    // 0=ok, 1=not found, 2=wrong password, 3=bad input
    public int Code { get; init; }
    public string Message { get; init; } = "";
    public Guid? Id { get; init; } // только при успехе; иначе null
}
