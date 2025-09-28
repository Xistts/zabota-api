using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Data;
using Zabota.Contracts;

namespace Zabota.Endpoints;

public static class FeaturesEndpoints
{
    // --------- Map ----------
    public static IEndpointRouteBuilder MapFeaturesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/features").RequireAuthorization().WithTags("Features");

        // GET /features  (персональный список функций)
        group
            .MapGet(
                "/",
                async (HttpContext http, AppDb db, CancellationToken ct) =>
                {
                    var userId = GetCurrentUserId(http);
                    if (userId == Guid.Empty)
                        return Results.Unauthorized();

                    var user = await db
                        .Users.AsNoTracking()
                        .Where(u => u.Id == userId)
                        .Select(u => new
                        {
                            u.Id,
                            u.IsPremium,
                            u.FamilyId,
                        })
                        .SingleOrDefaultAsync(ct);

                    if (user is null)
                        return Results.Unauthorized();

                    bool hasPremium = user.IsPremium;
                    bool hasFamily = user.FamilyId != null;

                    // Базовый набор фич (можно расширять)
                    var baseList = BuildBaseFeatures();

                    // Рассчитать enabled по условиям премиума/семьи
                    var personalized = baseList
                        .Select(f =>
                            f with
                            {
                                Enabled =
                                    (!f.Premium || hasPremium) && (!f.RequiresFamily || hasFamily),
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

                    return Results.Ok(response);
                }
            )
            .WithName("FeaturesList")
            .Produces<FeaturesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    // --------- Helpers ----------
    private static Guid GetCurrentUserId(HttpContext http)
    {
        var sub =
            http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private static List<FeatureDto> BuildBaseFeatures() =>
        new()
        {
            new FeatureDto(
                Key: "tasks",
                Name: "Задачи",
                Premium: false,
                RequiresFamily: false,
                AssignedToUser: true,
                Enabled: false, // вычислим ниже
                Order: 10,
                Icon: "ic_tasks",
                Route: "app://features/tasks"
            ),
            new FeatureDto(
                Key: "medications",
                Name: "Медикаменты",
                Premium: false,
                RequiresFamily: false,
                AssignedToUser: true,
                Enabled: false,
                Order: 20,
                Icon: "ic_pills",
                Route: "app://features/meds"
            ),
            new FeatureDto(
                Key: "blood_pressure",
                Name: "Давление",
                Premium: false,
                RequiresFamily: false,
                AssignedToUser: true,
                Enabled: false,
                Order: 30,
                Icon: "ic_bp",
                Route: "app://features/bp"
            ),
            new FeatureDto(
                Key: "chat",
                Name: "Чат",
                Premium: false,
                RequiresFamily: true, // только если есть семья
                AssignedToUser: false,
                Enabled: false,
                Order: 40,
                Icon: "ic_chat",
                Route: "app://features/chat"
            ),
            new FeatureDto(
                Key: "password_manager",
                Name: "Менеджер паролей",
                Premium: true, // премиум-фича
                RequiresFamily: false,
                AssignedToUser: true,
                Enabled: false,
                Order: 50,
                Icon: "ic_passwords",
                Route: "app://features/passwords"
            ),
        };
}
