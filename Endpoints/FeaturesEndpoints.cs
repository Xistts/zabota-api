using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Data;

namespace Zabota.Endpoints;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController : ControllerBase
{
    private readonly AppDb _db;

    public FeaturesController(AppDb db)
    {
        _db = db;
    }

    [HttpGet("get")]
    public async Task<ActionResult<FeaturesResponse>> Get(
        [FromQuery] Guid userId,
        CancellationToken ct
    )
    {
        if (userId == Guid.Empty)
            return BadRequest(new { message = "userId обязателен" });

        var user = await _db
            .Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.IsPremium,
                u.FamilyId,
            })
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return NotFound(new { message = "Пользователь не найден" });

        bool hasPremium = user.IsPremium;
        bool hasFamily = user.FamilyId != null;

        var baseList = BuildBaseFeatures();

        var personalized = baseList
            .Select(f =>
                f with
                {
                    Enabled = (!f.Premium || hasPremium) && (!f.RequiresFamily || hasFamily),
                }
            )
            .OrderBy(f => f.Order)
            .ToList();

        var response = new FeaturesResponse(
            FeatureList: personalized,
            Code: 0,
            Description: "Список функций",
            RequestId: Guid.NewGuid()
        );

        return Ok(response);
    }

    // ---------- DTOs ----------
    public sealed record FeatureDto(
        string Key,
        string Name,
        bool Premium,
        bool RequiresFamily,
        bool AssignedToUser,
        bool Enabled,
        int Order,
        string Icon,
        string Route
    );

    public sealed record FeaturesResponse(
        List<FeatureDto> FeatureList,
        int Code,
        string Description,
        Guid RequestId
    );

    private static List<FeatureDto> BuildBaseFeatures() =>
        new()
        {
            new FeatureDto(
                "tasks",
                "Задачи",
                false,
                false,
                true,
                false,
                10,
                "ic_tasks",
                "app://features/tasks"
            ),
            new FeatureDto(
                "medications",
                "Медикаменты",
                false,
                false,
                true,
                false,
                20,
                "ic_pills",
                "app://features/meds"
            ),
            new FeatureDto(
                "blood_pressure",
                "Давление",
                false,
                false,
                true,
                false,
                30,
                "ic_bp",
                "app://features/bp"
            ),
            new FeatureDto(
                "chat",
                "Чат",
                false,
                true,
                false,
                false,
                40,
                "ic_chat",
                "app://features/chat"
            ),
            new FeatureDto(
                "password_manager",
                "Менеджер паролей",
                true,
                false,
                true,
                false,
                50,
                "ic_passwords",
                "app://features/passwords"
            ),
        };
}
