namespace Zabota.Contracts;

public sealed class RolesResponse
{
    public IEnumerable<RoleItem> RoleList { get; set; } = Array.Empty<RoleItem>();

    public int Code { get; set; }
    public string Description { get; set; } = "";
    public string? RequestId { get; set; }
}

public sealed class RoleItem
{
    public string Name { get; set; } = "";
}