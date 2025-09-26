using System.Text.Json.Serialization;

namespace Zabota.Contracts;

public class BaseResponse
{
    public ResponseCode Code { get; set; } = ResponseCode.Ok;

    // Короткое описание/сообщение для пользователя
    public string? Description { get; set; }

    // Корреляционный id (по желанию)
    public string? RequestId { get; set; }

    // Полезно для валидации форм: { "Email": ["Некорректный формат"] }
    public Dictionary<string, List<string>>? Errors { get; set; }
}
