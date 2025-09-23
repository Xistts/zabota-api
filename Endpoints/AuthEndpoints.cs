using System.ComponentModel.DataAnnotations;
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

        // POST /Auth/Registration
        group.MapPost("/Registration", async (
            [FromBody] RegisterRequest? req,
            AppDb db,
            ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Auth.Registration");

            if (req is null)
                return Results.BadRequest(new ProblemDetails { Title = "Пустое тело запроса." });

            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new ProblemDetails { Title = "Email и пароль обязательны." });

            var email = req.Email.Trim().ToLowerInvariant();

            if (!new EmailAddressAttribute().IsValid(email))
                return Results.BadRequest(new ProblemDetails { Title = "Некорректный формат email." });

            if (req.Password.Length is < 8 or > 128)
                return Results.BadRequest(new ProblemDetails { Title = "Пароль должен быть 8–128 символов." });

            try
            {
                // уникальность (и всё равно ловим 23505 на всякий случай)
                var exists = await db.Users.AsNoTracking().AnyAsync(u => u.Email == email);
                if (exists)
                    return Results.Conflict(new ProblemDetails { Title = "Такой email уже зарегистрирован." });

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

                var user = new User
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    IsActive = true,
                    IsVerified = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.Users.Add(user);
                await db.SaveChangesAsync();

                return Results.Created($"/users/{user.Id}", new RegisterResponse
                {
                    Id = user.Id,
                    Email = user.Email,
                    IsVerified = user.IsVerified,
                    Message = "Пользователь создан."
                });
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
            {
                // уникальный индекс (на случай гонки)
                return Results.Conflict(new ProblemDetails { Title = "Email уже занят." });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Registration failed");
                return Results.Problem(title: "Внутренняя ошибка", statusCode: 500);
            }
        })
        .WithName("AuthRegistration")
        .Produces<RegisterResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        // POST /Auth/Login
        group.MapPost("/Login", async ([FromBody] LoginRequest? req, AppDb db, ILoggerFactory lf) =>
        {
            var log = lf.CreateLogger("Auth.Login");

            if (req is null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.Ok(new LoginResponse { Code = 3, Message = "Email и пароль обязательны." });

            var email = req.Email.Trim().ToLowerInvariant();

            try
            {
                var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
                if (user is null)
                    return Results.Ok(new LoginResponse { Code = 1, Message = "Пользователь не найден." });

                if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                    return Results.Ok(new LoginResponse { Code = 2, Message = "Неверный пароль." });

                return Results.Ok(new LoginResponse { Code = 0, Message = "Успешный вход.", Id = user.Id });
            }
            catch (InvalidOperationException ex)
            {
                // на случай, если в базе оказались дубликаты email
                log.LogError(ex, "Multiple users with same email {Email}", email);
                return Results.Problem(title: "Дубликаты email в базе", statusCode: 500);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Login failed");
                return Results.Problem(title: "Внутренняя ошибка", statusCode: 500);
            }
        })
        .WithName("AuthLogin")
        .Produces<LoginResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
