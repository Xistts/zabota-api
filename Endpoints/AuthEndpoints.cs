using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        group
            .MapPost(
                "/Registration",
                async ([FromBody] RegisterRequest req, AppDb db, TokenService tokens) =>
                {
                    var errors = new Dictionary<string, List<string>>();
                    static void AddErr(Dictionary<string, List<string>> e, string field, string msg)
                    {
                        if (!e.TryGetValue(field, out var list))
                            e[field] = list = new();
                        list.Add(msg);
                    }

                    // 1) Базовая валидация
                    if (string.IsNullOrWhiteSpace(req.LastName))
                        AddErr(errors, nameof(req.LastName), "Фамилия обязательна.");
                    if (string.IsNullOrWhiteSpace(req.FirstName))
                        AddErr(errors, nameof(req.FirstName), "Имя обязательно.");
                    if (string.IsNullOrWhiteSpace(req.Email))
                        AddErr(errors, nameof(req.Email), "Email обязателен.");
                    if (string.IsNullOrWhiteSpace(req.Password))
                        AddErr(errors, nameof(req.Password), "Пароль обязателен.");

                    if (errors.Count > 0)
                        return Results.BadRequest(
                            new { Message = "Запрос содержит ошибки.", Errors = errors }
                        );

                    var lastName = req.LastName!.Trim();
                    var firstName = req.FirstName!.Trim();
                    var middle = string.IsNullOrWhiteSpace(req.MiddleName)
                        ? null
                        : req.MiddleName!.Trim();
                    var email = req.Email!.Trim().ToLowerInvariant();
                    var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone!.Trim();
                    var roleStr = string.IsNullOrWhiteSpace(req.Role) ? null : req.Role!.Trim();

                    // 2) Форматы
                    if (req.Password!.Length < 8 || req.Password!.Length > 128)
                        AddErr(errors, nameof(req.Password), "Пароль должен быть 8–128 символов.");

                    if (!new EmailAddressAttribute().IsValid(email))
                        AddErr(errors, nameof(req.Email), "Некорректный формат email.");

                    if (
                        phone is not null
                        && !System.Text.RegularExpressions.Regex.IsMatch(
                            phone,
                            @"^[0-9+\-\s()]{6,20}$"
                        )
                    )
                        AddErr(errors, nameof(req.Phone), "Некорректный формат телефона.");

                    DateOnly? birthDate = req.BirthDate;
                    if (birthDate is not null)
                    {
                        var today = DateOnly.FromDateTime(DateTime.UtcNow);
                        if (birthDate > today || birthDate < new DateOnly(1900, 1, 1))
                            AddErr(errors, nameof(req.BirthDate), "Некорректная дата рождения.");
                    }

                    if (errors.Count > 0)
                        return Results.BadRequest(
                            new { Message = "Запрос содержит ошибки.", Errors = errors }
                        );

                    // 3) Уникальность email
                    var emailExists = await db.Users.AnyAsync(u => u.Email == email);
                    if (emailExists)
                        return Results.Conflict(
                            new { Message = "Пользователь уже зарегистрирован." }
                        );

                    // 4) Роль (опц.)
                    if (
                        !string.IsNullOrWhiteSpace(roleStr)
                        && !FamilyRoleRu.TryParseRussian(roleStr!, out _)
                    )
                        return Results.BadRequest(
                            new { Message = $"Неизвестная роль: '{roleStr}'." }
                        );

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
                        CreatedAtUtc = DateTime.UtcNow,
                    };

                    db.Users.Add(user);
                    await db.SaveChangesAsync();

                    // 6) Выдача токенов
                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var refreshRes = await tokens.IssueRefreshTokenAsync(user);
                    var rt = refreshRes.token;
                    var rtExp = refreshRes.expiresAtUtc;

                    var userDto = new RegisterResponse
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
                    };

                    return Results.Created(
                        $"/users/{user.Id}",
                        new
                        {
                            user = userDto,
                            tokens = new TokenPairResponse
                            {
                                AccessToken = at,
                                AccessTokenExpiresAtUtc = atExp,
                                RefreshToken = rt,
                                RefreshTokenExpiresAtUtc = rtExp,
                            },
                        }
                    );
                }
            )
            .AllowAnonymous()
            .WithName("AuthRegistration");

        // ========== POST /Auth/Login ==========
        group
            .MapPost(
                "/Login",
                async ([FromBody] LoginRequest req, AppDb db, TokenService tokens) =>
                {
                    if (
                        string.IsNullOrWhiteSpace(req.Email)
                        || string.IsNullOrWhiteSpace(req.Password)
                    )
                        return Results.BadRequest(new { Message = "Email и пароль обязательны." });

                    var email = req.Email.Trim().ToLowerInvariant();
                    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

                    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                        return Results.Unauthorized();

                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var refreshRes = await tokens.IssueRefreshTokenAsync(user);
                    var rt = refreshRes.token;
                    var rtExp = refreshRes.expiresAtUtc;

                    return Results.Ok(
                        new
                        {
                            user = new
                            {
                                user.Id,
                                user.Email,
                                user.FirstName,
                                user.LastName,
                            },
                            tokens = new TokenPairResponse
                            {
                                AccessToken = at,
                                AccessTokenExpiresAtUtc = atExp,
                                RefreshToken = rt,
                                RefreshTokenExpiresAtUtc = rtExp,
                            },
                        }
                    );
                }
            )
            .AllowAnonymous()
            .WithName("AuthLogin");

        // ========== POST /Auth/Refresh ==========
        group
            .MapPost(
                "/Refresh",
                async ([FromBody] RefreshRequest req, AppDb db, TokenService tokens) =>
                {
                    if (string.IsNullOrWhiteSpace(req.RefreshToken))
                        return Results.BadRequest(new { Message = "RefreshToken обязателен." });

                    var rt = await db
                        .RefreshTokens.Include(x => x.User)
                        .SingleOrDefaultAsync(x => x.Token == req.RefreshToken);

                    if (rt is null || rt.IsRevoked || rt.ExpiresAtUtc <= DateTime.UtcNow)
                        return Results.Unauthorized();

                    // Ротация RT
                    rt.RevokedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    var user = rt.User;
                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var refreshRes = await tokens.IssueRefreshTokenAsync(user);
                    var newRt = refreshRes.token;
                    var newRtExp = refreshRes.expiresAtUtc;

                    return Results.Ok(
                        new TokenPairResponse
                        {
                            AccessToken = at,
                            AccessTokenExpiresAtUtc = atExp,
                            RefreshToken = newRt,
                            RefreshTokenExpiresAtUtc = newRtExp,
                        }
                    );
                }
            )
            .WithName("AuthRefresh");

        // ========== GET /Auth/Validate ==========
        group
            .MapGet(
                "/Validate",
                (ClaimsPrincipal user) =>
                {
                    var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
                    return Results.Ok(
                        new
                        {
                            valid = true,
                            userId = sub,
                            email,
                        }
                    );
                }
            )
            .RequireAuthorization()
            .WithName("AuthValidate");

        // ========== POST /Auth/Logout ==========
        group
            .MapPost(
                "/Logout",
                async (
                    [FromBody] RefreshRequest req,
                    ClaimsPrincipal principal,
                    TokenService tokens
                ) =>
                {
                    if (!string.IsNullOrWhiteSpace(req.RefreshToken))
                    {
                        await tokens.InvalidateRefreshTokenAsync(req.RefreshToken);
                        return Results.Ok(new { message = "Logged out (refresh token revoked)." });
                    }

                    var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (Guid.TryParse(sub, out var userId))
                    {
                        await tokens.InvalidateAllUserRefreshTokensAsync(userId);
                        return Results.Ok(
                            new { message = "Logged out (all refresh tokens revoked)." }
                        );
                    }

                    return Results.BadRequest(
                        new { message = "No refresh token provided and user id missing." }
                    );
                }
            )
            .RequireAuthorization()
            .WithName("AuthLogout");

        return app;
    }
}
