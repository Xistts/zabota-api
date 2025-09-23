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
            // базовая валидация
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new ProblemDetails { Title = "Email и пароль обязательны." });

            var email = req.Email.Trim().ToLowerInvariant();

            if (!new EmailAddressAttribute().IsValid(email))
                return Results.BadRequest(new ProblemDetails { Title = "Некорректный формат email." });

            if (req.Password.Length < 8 || req.Password.Length > 128)
                return Results.BadRequest(new ProblemDetails { Title = "Пароль должен быть 8–128 символов." });

            // уникальность email
            var exists = await db.Users.AnyAsync(u => u.Email == email);
            if (exists)
                return Results.Conflict(new ProblemDetails { Title = "Такой email уже зарегистрирован." });

            // хеш пароля (BCrypt)
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                IsActive = true,
                IsVerified = false, // на будущее, если введёшь подтверждение почты
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var response = new RegisterResponse
            {
                Id = user.Id,
                Email = user.Email,
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
