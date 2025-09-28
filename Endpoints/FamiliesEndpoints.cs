// FamiliesEndpoints.cs
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Data;
using Zabota.Models;

namespace Zabota.Endpoints;

public static class FamiliesEndpoints
{
    // ----------------- Helpers -----------------
    private static Guid GetCurrentUserId(HttpContext http)
    {
        var sub =
            http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    private static async Task<bool> IsFamilyAdminAsync(
        AppDb db,
        Guid familyId,
        Guid userId,
        CancellationToken ct
    )
    {
        if (userId == Guid.Empty)
            return false;
        return await db.Users.AnyAsync(
            u => u.Id == userId && u.FamilyId == familyId && u.IsFamilyAdmin,
            ct
        );
    }

    private static string GenerateInviteCode(int length = 12)
    {
        // Генерируем только верхний регистр/цифры (исключим похожие символы)
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var data = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(alphabet[data[i] % alphabet.Length]);
        return sb.ToString();
    }

    // ----------------- Map -----------------
    public static IEndpointRouteBuilder MapFamiliesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/families").RequireAuthorization().WithTags("Families");

        // POST /families — создать семью и назначить текущего пользователя админом
        group
            .MapPost(
                "/",
                async (
                    [FromBody] CreateFamilyRequest req,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(req.Name))
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Название семьи обязательно." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId, ct);
                    if (user is null)
                        return Results.Unauthorized();

                    if (user.FamilyId != null)
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Пользователь уже состоит в семье." }
                        );

                    var family = new Family
                    {
                        Name = req.Name.Trim(),
                        InviteCode = GenerateInviteCode(12),
                        CreatedAtUtc = DateTime.UtcNow,
                    };

                    db.Families.Add(family);
                    await db.SaveChangesAsync(ct);

                    user.FamilyId = family.Id;
                    user.RoleInFamily = "Admin";
                    user.IsFamilyAdmin = true;

                    await db.SaveChangesAsync(ct);

                    return Results.Created(
                        $"/families/{family.Id}",
                        new CreateFamilyResponse
                        {
                            Id = family.Id,
                            Name = family.Name,
                            InviteCode = family.InviteCode,
                            CreatedAtUtc = family.CreatedAtUtc,
                        }
                    );
                }
            )
            .WithName("FamiliesCreate")
            .Produces<CreateFamilyResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // POST /families/join-by-code — вступление по коду
        group
            .MapPost(
                "/join-by-code",
                async ([FromBody] JoinByCodeRequest req, AppDb db, CancellationToken ct) =>
                {
                    if (string.IsNullOrWhiteSpace(req.InviteCode))
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Код приглашения обязателен." }
                        );

                    if (req.UserId == Guid.Empty)
                        return Results.BadRequest(
                            new ProblemDetails { Title = "UserId обязателен." }
                        );

                    var code = req.InviteCode.Trim().ToUpperInvariant();

                    var family = await db
                        .Families.AsNoTracking()
                        .FirstOrDefaultAsync(f => f.InviteCode == code, ct);

                    if (family is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Семья с таким кодом не найдена." }
                        );

                    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
                    if (user is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Пользователь не найден." }
                        );

                    if (user.FamilyId != null)
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Пользователь уже состоит в семье." }
                        );

                    user.FamilyId = family.Id;
                    user.RoleInFamily = string.IsNullOrWhiteSpace(req.RoleInFamily)
                        ? "Member"
                        : req.RoleInFamily!.Trim();
                    user.IsFamilyAdmin = req.IsAdmin;

                    await db.SaveChangesAsync(ct);

                    return Results.Ok(
                        new JoinByCodeResponse
                        {
                            FamilyId = family.Id,
                            FamilyName = family.Name,
                            UserId = user.Id,
                            RoleInFamily = user.RoleInFamily,
                            IsAdmin = user.IsFamilyAdmin,
                        }
                    );
                }
            )
            .WithName("FamiliesJoinByCode")
            .Produces<JoinByCodeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /families/{familyId}/members — список участников семьи
        group
            .MapGet(
                "/{familyId:guid}/members",
                async (Guid familyId, AppDb db, CancellationToken ct) =>
                {
                    var exists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
                    if (!exists)
                        return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

                    var items = await db
                        .Users.Where(u => u.FamilyId == familyId)
                        .OrderByDescending(u => u.IsFamilyAdmin)
                        .ThenBy(u => u.LastName)
                        .ThenBy(u => u.FirstName)
                        .Select(u => new FamilyMemberDto
                        {
                            Id = u.Id, // возвращаем Id пользователя
                            FamilyId = familyId,
                            UserId = u.Id,
                            FullName =
                                $"{u.LastName} {u.FirstName}{(u.MiddleName != null ? " " + u.MiddleName : "")}",
                            RoleInFamily = u.RoleInFamily,
                            IsAdmin = u.IsFamilyAdmin,
                        })
                        .ToListAsync(ct);

                    return Results.Ok(items);
                }
            )
            .WithName("FamiliesMembersList")
            .Produces<List<FamilyMemberDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /families/{familyId}/messages?beforeUtc=&take=
        group
            .MapGet(
                "/{familyId:guid}/messages",
                async (
                    Guid familyId,
                    [FromQuery] DateTime? beforeUtc,
                    [FromQuery] int? take,
                    AppDb db,
                    CancellationToken ct
                ) =>
                {
                    var exists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
                    if (!exists)
                        return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

                    var limit = Math.Clamp(take ?? 50, 1, 200);

                    var q = db
                        .ChatMessages.AsNoTracking()
                        .Where(x => x.FamilyId == familyId && !x.IsDeleted);

                    if (beforeUtc is not null)
                        q = q.Where(x => x.SentAtUtc < beforeUtc);

                    var items = await q.OrderByDescending(x => x.SentAtUtc)
                        .Take(limit)
                        .Select(x => new ChatMessageDto
                        {
                            Id = x.Id,
                            FamilyId = x.FamilyId,
                            AuthorUserId = x.AuthorUserId,
                            Text = x.Text,
                            SentAtUtc = x.SentAtUtc,
                            EditedAtUtc = x.EditedAtUtc,
                        })
                        .ToListAsync(ct);

                    // Возвращаем по возрастанию времени
                    items.Reverse();
                    return Results.Ok(items);
                }
            )
            .WithName("FamiliesMessagesList")
            .Produces<List<ChatMessageDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /families/{familyId}/messages — отправить сообщение
        group
            .MapPost(
                "/{familyId:guid}/messages",
                async (
                    Guid familyId,
                    [FromBody] SendMessageRequest req,
                    AppDb db,
                    CancellationToken ct
                ) =>
                {
                    if (req.AuthorUserId == Guid.Empty)
                        return Results.BadRequest(
                            new ProblemDetails { Title = "AuthorUserId обязателен." }
                        );

                    if (string.IsNullOrWhiteSpace(req.Text))
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Текст сообщения обязателен." }
                        );

                    var familyExists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
                    if (!familyExists)
                        return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

                    // Проверка членства
                    var isMember = await db.Users.AnyAsync(
                        u => u.Id == req.AuthorUserId && u.FamilyId == familyId,
                        ct
                    );
                    if (!isMember)
                        return Results.Forbid();

                    var msg = new ChatMessage
                    {
                        FamilyId = familyId,
                        AuthorUserId = req.AuthorUserId,
                        Text = req.Text.Trim(),
                        SentAtUtc = DateTime.UtcNow,
                    };

                    db.ChatMessages.Add(msg);
                    await db.SaveChangesAsync(ct);

                    return Results.Created(
                        $"/families/{familyId}/messages/{msg.Id}",
                        new ChatMessageDto
                        {
                            Id = msg.Id,
                            FamilyId = msg.FamilyId,
                            AuthorUserId = msg.AuthorUserId,
                            Text = msg.Text,
                            SentAtUtc = msg.SentAtUtc,
                            EditedAtUtc = msg.EditedAtUtc,
                        }
                    );
                }
            )
            .WithName("FamiliesMessagesSend")
            .Produces<ChatMessageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /families/{familyId}/messages/{messageId} — редактировать сообщение (только автор)
        group
            .MapPatch(
                "/{familyId:guid}/messages/{messageId:guid}",
                async (
                    Guid familyId,
                    Guid messageId,
                    [FromBody] EditMessageRequest req,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (string.IsNullOrWhiteSpace(req.Text))
                        return Results.BadRequest(
                            new ProblemDetails { Title = "Текст обязателен." }
                        );

                    var msg = await db.ChatMessages.FirstOrDefaultAsync(
                        m => m.Id == messageId && m.FamilyId == familyId && !m.IsDeleted,
                        ct
                    );

                    if (msg is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Сообщение не найдено." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var isAuthor = msg.AuthorUserId == currentUserId;
                    if (!isAuthor)
                        return Results.Forbid();

                    msg.Text = req.Text.Trim();
                    msg.EditedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    return Results.Ok(
                        new ChatMessageDto
                        {
                            Id = msg.Id,
                            FamilyId = msg.FamilyId,
                            AuthorUserId = msg.AuthorUserId,
                            Text = msg.Text,
                            SentAtUtc = msg.SentAtUtc,
                            EditedAtUtc = msg.EditedAtUtc,
                        }
                    );
                }
            )
            .WithName("FamiliesMessageEdit")
            .Produces<ChatMessageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // DELETE /families/{familyId}/messages/{messageId} — пометить удалённым (только автор)
        group
            .MapDelete(
                "/{familyId:guid}/messages/{messageId:guid}",
                async (
                    Guid familyId,
                    Guid messageId,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    var msg = await db.ChatMessages.FirstOrDefaultAsync(
                        m => m.Id == messageId && m.FamilyId == familyId && !m.IsDeleted,
                        ct
                    );

                    if (msg is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Сообщение не найдено или уже удалено." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var isAuthor = msg.AuthorUserId == currentUserId;
                    if (!isAuthor)
                        return Results.Forbid();

                    msg.IsDeleted = true;
                    msg.EditedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    return Results.NoContent();
                }
            )
            .WithName("FamiliesMessageDelete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // DELETE /families/{familyId}/members/{memberId} — удалить участника (обнулить связь)
        group
            .MapDelete(
                "/{familyId:guid}/members/{memberId:guid}",
                async (
                    Guid familyId,
                    Guid memberId,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    var user = await db.Users.FirstOrDefaultAsync(
                        u => u.Id == memberId && u.FamilyId == familyId,
                        ct
                    );
                    if (user is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Участник не найден." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);
                    if (!isAdmin)
                        return Results.Forbid();

                    user.FamilyId = null;
                    user.RoleInFamily = "Member";
                    user.IsFamilyAdmin = false;

                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
            )
            .WithName("FamiliesMemberDelete")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /families/{familyId}/members/{memberId} — обновить роль/флаги участника
        group
            .MapPatch(
                "/{familyId:guid}/members/{memberId:guid}",
                async (
                    Guid familyId,
                    Guid memberId,
                    [FromBody] UpdateMemberRequest req,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    var user = await db.Users.FirstOrDefaultAsync(
                        u => u.Id == memberId && u.FamilyId == familyId,
                        ct
                    );
                    if (user is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Участник не найден." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);
                    if (!isAdmin)
                        return Results.Forbid();

                    if (!string.IsNullOrWhiteSpace(req.RoleInFamily))
                        user.RoleInFamily = req.RoleInFamily!.Trim();

                    if (req.IsAdmin.HasValue)
                        user.IsFamilyAdmin = req.IsAdmin.Value;

                    await db.SaveChangesAsync(ct);

                    return Results.Ok(
                        new FamilyMemberDto
                        {
                            Id = user.Id,
                            FamilyId = familyId,
                            UserId = user.Id,
                            FullName =
                                $"{user.LastName} {user.FirstName}{(user.MiddleName != null ? " " + user.MiddleName : "")}",
                            RoleInFamily = user.RoleInFamily,
                            IsAdmin = user.IsFamilyAdmin,
                        }
                    );
                }
            )
            .WithName("FamiliesMemberUpdate")
            .Produces<FamilyMemberDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /families/{familyId}/leave — пользователь покидает семью
        group
            .MapPost(
                "/{familyId:guid}/leave",
                async (
                    Guid familyId,
                    [FromBody] LeaveFamilyRequest req,
                    AppDb db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (req.UserId == Guid.Empty)
                        return Results.BadRequest(
                            new ProblemDetails { Title = "UserId обязателен." }
                        );

                    var user = await db.Users.FirstOrDefaultAsync(
                        u => u.Id == req.UserId && u.FamilyId == familyId,
                        ct
                    );
                    if (user is null)
                        return Results.NotFound(
                            new ProblemDetails { Title = "Пользователь не состоит в семье." }
                        );

                    var currentUserId = GetCurrentUserId(http);
                    var isSelf = currentUserId == req.UserId;
                    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);

                    if (!isSelf && !isAdmin)
                        return Results.Forbid();

                    user.FamilyId = null;
                    user.RoleInFamily = "Member";
                    user.IsFamilyAdmin = false;

                    await db.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
            )
            .WithName("FamiliesLeave")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}

// ------------------- DTOs -------------------

public sealed class CreateFamilyRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;
}

public sealed class CreateFamilyResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string InviteCode { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class JoinByCodeRequest
{
    [Required, MaxLength(12)]
    public string InviteCode { get; set; } = default!;

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string? RoleInFamily { get; set; }

    public bool IsAdmin { get; set; } = false;
}

public sealed class JoinByCodeResponse
{
    public Guid FamilyId { get; set; }
    public string FamilyName { get; set; } = default!;
    public Guid UserId { get; set; }
    public string RoleInFamily { get; set; } = default!;
    public bool IsAdmin { get; set; }
}

public sealed class FamilyMemberDto
{
    public Guid Id { get; set; } // теперь равен UserId
    public Guid FamilyId { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string RoleInFamily { get; set; } = default!;
    public bool IsAdmin { get; set; }
}

public sealed class SendMessageRequest
{
    [Required]
    public Guid AuthorUserId { get; set; }

    [Required, MaxLength(4000)]
    public string Text { get; set; } = default!;
}

public sealed class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Text { get; set; } = default!;
    public DateTime SentAtUtc { get; set; }
    public DateTime? EditedAtUtc { get; set; }
}

public sealed class UpdateMemberRequest
{
    public string? RoleInFamily { get; set; }
    public bool? IsAdmin { get; set; }
}

public sealed class LeaveFamilyRequest
{
    public Guid UserId { get; set; }
}

public sealed class EditMessageRequest
{
    public Guid? AuthorUserId { get; set; } // оставлен для совместимости, фактически не нужен
    public string Text { get; set; } = default!;
}
