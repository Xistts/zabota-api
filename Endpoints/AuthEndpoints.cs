using System.ComponentModel.DataAnnotations;
using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Contracts;
using Zabota.Data;
using Zabota.Models;

namespace Zabota.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/Auth");

        // ========== POST /Auth/Registration ==========
        group.MapPost("/Registration", async (
    [FromBody] RegisterRequest req,
    AppDb db) =>
{
    var roleStr = string.IsNullOrWhiteSpace(req.Role) ? null : req.Role!.Trim();
    // 1) Базовая валидация обязательных полей
    if (string.IsNullOrWhiteSpace(req.LastName) ||
        string.IsNullOrWhiteSpace(req.FirstName) ||
        string.IsNullOrWhiteSpace(req.Email) ||
        string.IsNullOrWhiteSpace(req.Password))
    {
        return Results.BadRequest(new ProblemDetails { Title = "Фамилия, имя, логин и пароль обязательны." });
    }

    var lastName = req.LastName.Trim();
    var firstName = req.FirstName.Trim();
    var middleName = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName!.Trim();
    var email = req.Email.Trim().ToLowerInvariant();
    var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone!.Trim();
    var role = string.IsNullOrWhiteSpace(req.Role) ? null : req.Role!.Trim();

    // 2) Валидация логина и пароля

    if (req.Password.Length < 8 || req.Password.Length > 128)
        return Results.BadRequest(new ProblemDetails { Title = "Пароль должен быть 8–128 символов." });

    // Email: если задан — формат
    if (!new EmailAddressAttribute().IsValid(email))
        return Results.BadRequest(new ProblemDetails { Title = "Некорректный формат email." });

    // Телефон (упрощённая проверка, при желании ужесточите)
    if (phone is not null && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+\-\s()]{6,20}$"))
        return Results.BadRequest(new ProblemDetails { Title = "Некорректный формат телефона." });

    // Дата рождения — не в будущем и реалистичная
    DateOnly? birthDate = req.BirthDate;
    if (birthDate is not null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birthDate > today || birthDate < new DateOnly(1900, 1, 1))
            return Results.BadRequest(new ProblemDetails { Title = "Некорректная дата рождения." });
    }

    // 3) Уникальность и email
    if (email is not null)
    {
        var emailExists = await db.Users.AnyAsync(u => u.Email == email);
        if (emailExists)
            return Results.Conflict(new ProblemDetails { Title = "Такой email уже зарегистрирован." });
    }

    // 4) Хеш пароля
    var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
FamilyRole? roleEnum = null;
if (!string.IsNullOrEmpty(roleStr))
{
    if (FamilyRoleRu.TryParseRussian(roleStr, out var parsed))
        roleEnum = parsed;
    else
        return Results.BadRequest(new ProblemDetails { Title = $"Неизвестная роль: '{roleStr}'." });
}
    // 5) Создание пользователя
    var user = new User
    {
        PasswordHash = passwordHash,
        Email = email,
        Phone = phone,
        LastName = lastName,
        FirstName = firstName,
        MiddleName = middleName,
        DateOfBirth = birthDate,
        Role = role,
        IsActive = true,
        IsVerified = false,
        CreatedAtUtc = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    // 6) Ответ
    var response = new RegisterResponse
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        MiddleName = user.MiddleName,
        Phone = user.Phone,
        BirthDate = user.DateOfBirth,
        Role = user.Role,
        IsVerified = user.IsVerified,
        Message = "Пользователь создан."
    };

    return Results.Created($"/users/{user.Id}", response);
})
.WithName("AuthRegistration")
.Produces<RegisterResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status409Conflict);

        // ========== POST /Auth/Login ==========
        group.MapPost("/Login", async ([FromBody] LoginRequest req, AppDb db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.Ok(new LoginResponse { Code = 3, Message = "Email и пароль обязательны." });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

            if (user is null)
                return Results.Ok(new LoginResponse { Code = 1, Message = "Пользователь не найден." });

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Ok(new LoginResponse { Code = 2, Message = "Неверный пароль." });

            return Results.Ok(new LoginResponse { Code = 0, Message = "Успешный вход.", Id = user.Id });
        })
        .WithName("AuthLogin")
        .Produces<LoginResponse>(StatusCodes.Status200OK);

        return app;
    }
}
