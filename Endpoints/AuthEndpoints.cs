using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

                    // базовая валидация
                    if (string.IsNullOrWhiteSpace(req.LastName))
                        AddErr(errors, nameof(req.LastName), "Фамилия обязательна.");
                    if (string.IsNullOrWhiteSpace(req.FirstName))
                        AddErr(errors, nameof(req.FirstName), "Имя обязательно.");
                    if (string.IsNullOrWhiteSpace(req.Email))
                        AddErr(errors, nameof(req.Email), "Email обязателен.");
                    if (string.IsNullOrWhiteSpace(req.Password))
                        AddErr(errors, nameof(req.Password), "Пароль обязателен.");

                    if (errors.Count > 0)
                        return ResponseUtils.BadResp(
                            "Запрос содержит ошибки.",
                            ResponseCode.ValidationError,
                            errors
                        );

                    var lastName = req.LastName!.Trim();
                    var firstName = req.FirstName!.Trim();
                    var middle = string.IsNullOrWhiteSpace(req.MiddleName)
                        ? null
                        : req.MiddleName!.Trim();
                    var email = req.Email!.Trim().ToLowerInvariant();
                    var phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone!.Trim();
                    var roleStr = string.IsNullOrWhiteSpace(req.Role) ? null : req.Role!.Trim();

                    // форматы
                    if (req.Password!.Length is < 8 or > 128)
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

                    if (req.BirthDate is { } birth)
                    {
                        var today = DateOnly.FromDateTime(DateTime.UtcNow);
                        if (birth > today || birth < new DateOnly(1900, 1, 1))
                            AddErr(errors, nameof(req.BirthDate), "Некорректная дата рождения.");
                    }

                    if (errors.Count > 0)
                        return ResponseUtils.BadResp(
                            "Запрос содержит ошибки.",
                            ResponseCode.ValidationError,
                            errors
                        );

                    // уникальность email
                    var emailExists = await db.Users.AnyAsync(u => u.Email == email);
                    if (emailExists)
                        return ResponseUtils.BadResp(
                            "Пользователь уже зарегистрирован.",
                            ResponseCode.Conflict
                        );

                    // роль (из русской строки, опционально)
                    if (
                        !string.IsNullOrWhiteSpace(roleStr)
                        && !FamilyRoleRu.TryParseRussian(roleStr!, out _)
                    )
                        return ResponseUtils.BadResp(
                            $"Неизвестная роль: '{roleStr}'.",
                            ResponseCode.ValidationError
                        );

                    // создание пользователя
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
                    var user = new User
                    {
                        PasswordHash = passwordHash,
                        Email = email,
                        Phone = phone,
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = middle,
                        DateOfBirth = req.BirthDate,
                        Role = roleStr,
                        IsActive = true,
                        IsVerified = false,
                        CreatedAtUtc = DateTime.UtcNow,
                    };

                    db.Users.Add(user);
                    await db.SaveChangesAsync();

                    // токены
                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var (rt, rtExp) = await tokens.IssueRefreshTokenAsync(user);

                    var data = new AuthData
                    {
                        User = new
                        {
                            user.Id,
                            user.Email,
                            user.FirstName,
                            user.LastName,
                            user.MiddleName,
                            user.Phone,
                            BirthDate = user.DateOfBirth,
                            user.Role,
                            user.IsVerified,
                        },
                        Token = at,
                        TokenExpiresAt = atExp,
                        RefreshToken = rt,
                        RefreshTokenExpiresAt = rtExp,
                    };

                    return ResponseUtils.OkResp(data, "Пользователь создан.");
                }
            )
            .AllowAnonymous()
            .WithName("AuthRegistration");

        // ========== POST /Auth/Login ==========
        group
            .MapPost(
                "/Login",
                async (
                    [FromBody] LoginRequest req,
                    AppDb db,
                    TokenService tokens,
                    IMemoryCache cache
                ) =>
                {
                    const int MaxAttempts = 3;
                    var window = TimeSpan.FromMinutes(10);

                    var emailRaw = req.Email ?? "";
                    var email = emailRaw.Trim().ToLowerInvariant();
                    var cacheKey = $"login:fail:{email}";

                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
                        return ResponseUtils.BadResp(
                            "Неудачная попытка входа. Осталось попыток: 3",
                            ResponseCode.Error
                        );

                    var attempts = cache.GetOrCreate(
                        cacheKey,
                        e =>
                        {
                            e.AbsoluteExpirationRelativeToNow = window;
                            return 0;
                        }
                    );

                    if (attempts >= MaxAttempts)
                        return ResponseUtils.BadResp(
                            "Превышено число попыток. Повторите позже.",
                            ResponseCode.Error
                        );

                    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
                    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                    {
                        attempts++;
                        cache.Set(
                            cacheKey,
                            attempts,
                            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = window }
                        );
                        var remaining = Math.Max(0, MaxAttempts - attempts);
                        return ResponseUtils.BadResp(
                            $"Неудачная попытка входа. Осталось попыток: {remaining}",
                            ResponseCode.Error
                        );
                    }

                    cache.Remove(cacheKey);

                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var (rt, rtExp) = await tokens.IssueRefreshTokenAsync(user);

                    var data = new AuthData
                    {
                        User = new
                        {
                            user.Id,
                            user.Email,
                            user.FirstName,
                            user.LastName,
                        },
                        Token = at,
                        TokenExpiresAt = atExp,
                        RefreshToken = rt,
                        RefreshTokenExpiresAt = rtExp,
                    };

                    return ResponseUtils.OkResp(data, "Успешная аутентификация");
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
                        return ResponseUtils.BadResp(
                            "RefreshToken обязателен.",
                            ResponseCode.ValidationError
                        );

                    var rt = await db
                        .RefreshTokens.Include(x => x.User)
                        .SingleOrDefaultAsync(x => x.Token == req.RefreshToken);

                    if (rt is null || rt.IsRevoked || rt.ExpiresAtUtc <= DateTime.UtcNow)
                        return ResponseUtils.BadResp(
                            "Недействительный или просроченный refresh-токен.",
                            ResponseCode.Error
                        );

                    // ротация
                    rt.RevokedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();

                    var user = rt.User;
                    var (at, atExp) = tokens.CreateAccessToken(user);
                    var (newRt, newRtExp) = await tokens.IssueRefreshTokenAsync(user);

                    var data = new
                    {
                        Token = at,
                        TokenExpiresAt = atExp,
                        RefreshToken = newRt,
                        RefreshTokenExpiresAt = newRtExp,
                    };

                    return ResponseUtils.OkResp(data, "Токены обновлены.");
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
                    return ResponseUtils.OkResp(
                        new
                        {
                            valid = true,
                            userId = sub,
                            email,
                        },
                        "Токен валиден."
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
                        return ResponseUtils.OkResp<object?>(
                            null,
                            "Выход выполнен. Refresh-токен отозван."
                        );
                    }

                    var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (!Guid.TryParse(sub, out var userId))
                        return ResponseUtils.BadResp(
                            "Не удалось определить пользователя.",
                            ResponseCode.ValidationError
                        );

                    await tokens.InvalidateAllUserRefreshTokensAsync(userId);
                    return ResponseUtils.OkResp<object?>(
                        null,
                        "Выход выполнен. Все refresh-токены пользователя отозваны."
                    );
                }
            )
            .RequireAuthorization()
            .WithName("AuthLogout");

        return app;
    }
}
