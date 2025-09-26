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

        // ========== POST /Auth/Registration ==========
        group.MapPost("/Registration", async ([FromBody] RegisterRequest req, AppDb db) =>
        {
            var errors = new Dictionary<string, List<string>>();
            static void AddErr(Dictionary<string, List<string>> e, string field, string msg)
            {
                if (!e.TryGetValue(field, out var list)) e[field] = list = new();
                list.Add(msg);
            }

            // 1) Базовая валидация
            if (string.IsNullOrWhiteSpace(req.LastName)) AddErr(errors, nameof(req.LastName), "Фамилия обязательна.");
            if (string.IsNullOrWhiteSpace(req.FirstName)) AddErr(errors, nameof(req.FirstName), "Имя обязательно.");
            if (string.IsNullOrWhiteSpace(req.Email)) AddErr(errors, nameof(req.Email), "Email обязателен.");
            if (string.IsNullOrWhiteSpace(req.Password)) AddErr(errors, nameof(req.Password), "Пароль обязателен.");

            if (errors.Count > 0)
                return Results.Ok(new RegisterResponse
                {
                    Code = (int)ResponseCode.ValidationError,
                    Description = "Запрос содержит ошибки.",
                });

            var lastName = req.LastName!.Trim();
            var firstName = req.FirstName!.Trim();
            var middle = string.IsNullOrWhiteSpace(req.MiddleName) ? null : req.MiddleName!.Trim();
            var email = req.Email!.Trim().ToLowerInvariant();
            var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone!.Trim();
            var roleStr = string.IsNullOrWhiteSpace(req.Role) ? null : req.Role!.Trim();

            // 2) Форматы
            if (req.Password!.Length < 8 || req.Password!.Length > 128)
                AddErr(errors, nameof(req.Password), "Пароль должен быть 8–128 символов.");

            if (!new EmailAddressAttribute().IsValid(email))
                AddErr(errors, nameof(req.Email), "Некорректный формат email.");

            if (phone is not null && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+\-\s()]{6,20}$"))
                AddErr(errors, nameof(req.Phone), "Некорректный формат телефона.");

            DateOnly? birthDate = req.BirthDate;
            if (birthDate is not null)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (birthDate > today || birthDate < new DateOnly(1900, 1, 1))
                    AddErr(errors, nameof(req.BirthDate), "Некорректная дата рождения.");
            }

            if (errors.Count > 0)
                return Results.Ok(new RegisterResponse
                {
                    Code = (int)ResponseCode.ValidationError,
                    Description = "Запрос содержит ошибки.",
                });

            // 3) Уникальность email
            var emailExists = await db.Users.AnyAsync(u => u.Email == email);
            if (emailExists)
                return Results.Ok(new RegisterResponse
                {
                    Code = (int)ResponseCode.ValidationError,
                    Description = "Пользователь уже зарегистрирован.",
                });

            // 4) Роль (если пришла строкой по-русски)
            FamilyRole? roleEnum = null;
            if (!string.IsNullOrWhiteSpace(roleStr))
            {
                if (!FamilyRoleRu.TryParseRussian(roleStr!, out var parsed))
                    return Results.Ok(new RegisterResponse
                    {
                        Code = (int)ResponseCode.ValidationError,
                        Description = $"Неизвестная роль: '{roleStr}'.",
                    });
                roleEnum = parsed;
            }

            // 5) Создание пользователя
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var user = new User
            {
                PasswordHash = passwordHash,
                Email = email,
                Phone = phone,
                LastName = lastName,
                FirstName = firstName,
                MiddleName = middle,
                DateOfBirth = birthDate,
                Role = roleStr,
                IsActive = true,
                IsVerified = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // 6) Ответ 200 + Code = Ok
            return Results.Ok(new RegisterResponse
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
                Code = (int)ResponseCode.Ok,
                Description = "Пользователь создан.",
                RequestId = Guid.NewGuid().ToString()
            });
        })
        .WithName("AuthRegistration")
        .Produces<RegisterResponse>(StatusCodes.Status200OK);

        // ========== POST /Auth/Login ==========
        group.MapPost("/Login", async ([FromBody] LoginRequest req, AppDb db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Results.Ok(new LoginResponse
                {
                    Code = (int)ResponseCode.ValidationError,
                    Description = "Email и пароль обязательны.",
                });

            var email = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

            if (user is null)
                return Results.Ok(new LoginResponse
                {
                    Code = (int)ResponseCode.NotFound,
                    Description = "Пользователь не найден."
                });

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Results.Ok(new LoginResponse
                {
                    Code = (int)ResponseCode.InvalidCredentials,
                    Description = "Неверный пароль."
                });

            return Results.Ok(new LoginResponse
            {
                Code = (int)ResponseCode.Ok,
                Description = "Успешный вход.",
                Id = user.Id
            });
        })
        .WithName("AuthLogin")
        .Produces<LoginResponse>(StatusCodes.Status200OK);

        return app;
    }
}
