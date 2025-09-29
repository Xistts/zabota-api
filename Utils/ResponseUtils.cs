using System.Diagnostics;
using Zabota.Contracts;

public static class ResponseUtils
{
    public static string MakeRequestId() =>
        Activity.Current?.Id ?? $"{Environment.MachineName}:{Guid.NewGuid():N}".ToUpperInvariant();

    public static string CodeTitle(ResponseCode code) => code == ResponseCode.Ok ? "Ok" : "Error";

    public static IResult OkResp<T>(T data, string description = "OK") =>
        Results.Ok(
            new ApiResponse<T>
            {
                Code = ResponseCode.Ok,
                CodeTitle = CodeTitle(ResponseCode.Ok),
                Description = description,
                RequestId = MakeRequestId(),
                Data = data,
            }
        );

    public static IResult BadResp(
        string description,
        ResponseCode code = ResponseCode.Error,
        Dictionary<string, List<string>>? errors = null
    ) =>
        Results.BadRequest(
            new ApiResponse<object?>
            {
                Code = code,
                CodeTitle = CodeTitle(code),
                Description = description,
                RequestId = MakeRequestId(),
                Data = null,
                Errors = errors,
            }
        );
}
