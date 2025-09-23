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
    if (req.UserId <= 0)
        return Results.BadRequest(new { Message = "Некорректный UserId" });

    var user = await db.Users.FindAsync(req.UserId);
    if (user is null)
        return Results.NotFound(new { Message = "Пользователь не найден" });

    if (req.DateOfBirth is { } dob && dob > DateTime.UtcNow.Date)
        return Results.BadRequest(new { Message = "Дата рождения не может быть в будущем" });

    if (req.Role is not null)       user.Role = req.Role;
    if (req.DateOfBirth is not null) user.DateOfBirth = req.DateOfBirth!.Value.Date;

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        user.Id,
        user.Email,
        user.Role,         // ← сериализуется как "Мама"/"Папа"/...
        user.DateOfBirth,
        // Age = user.DateOfBirth is null ? null : CalculateAge(user.DateOfBirth.Value),
        Message = "Данные пользователя обновлены"
    });
});


    // GET /Users/Roles — список всех ролей
group.MapGet("/Roles", () =>
{
    var roles = FamilyRoleRu.All()
        .Select(x => x.Ru); // ["Бабушка","Дедушка","Мама","Папа","Дочь","Сын"]
    return Results.Ok(roles);
});

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
