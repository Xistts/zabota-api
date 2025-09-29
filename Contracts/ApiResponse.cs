namespace Zabota.Contracts;

public class ApiResponse<T> : BaseResponse
{
    public string CodeTitle { get; set; } = "";
    public T? Data { get; set; }
}
