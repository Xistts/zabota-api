using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Contracts;
using Zabota.Data;
using Zabota.Models;

namespace Zabota.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Users");

        // POST /Users/Info — обновить роль/дату рождения
group.MapPost("/Info", async ([FromBody] UpdateUserRoleRequest req, AppDb db) =>
{
    if (req.UserId == Guid.Empty)
        return Results.BadRequest(new { Message = "Некорректный UserId" });

    var user = await db.Users.FindAsync(req.UserId);
    if (user is null)
        return Results.NotFound(new { Message = "Пользователь не найден" });

    if (req.DateOfBirth is { } dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dob > today)
            return Results.BadRequest(new { Message = "Дата рождения не может быть в будущем" });
        user.DateOfBirth = dob;
    }

    if (!string.IsNullOrWhiteSpace(req.Role))
        user.Role = req.Role!.Trim();

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.Role,        // будет сериализовано конвертером как «Мама»/«Папа»/...
        user.DateOfBirth,
        Message = "Данные пользователя обновлены"
    });
});


    // GET /Users/Roles — список всех ролей
        group.MapGet("/Roles", () =>
        {
            var requestId = Guid.NewGuid().ToString();

            var roles = FamilyRoleRu.All()
                .Select(x => new RoleItem { Name = x.Ru })
                .ToList();

            var response = new RolesResponse
            {
                RoleList = roles,
                Code = (int)ResponseCode.Ok,
                Description = "Список ролей",
                RequestId = requestId
            };

            return Results.Ok(response);
        })
        .WithName("GetRoles")
        .Produces<RolesResponse>(StatusCodes.Status200OK);

        return app;
    }

    private static int CalculateAge(DateTime dob)
    {
        var today = DateTime.Today;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return age;
    }
}
