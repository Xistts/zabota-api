using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zabota.Contracts;
using Zabota.Data;
using Zabota.Models;
using System.Security.Claims;

public static class FamiliesEndpoints
{
    private static Guid GetCurrentUserId(HttpContext httpContext)
{
    var sub = httpContext.User.FindFirst("sub")?.Value
           ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
}

private static async Task<bool> IsFamilyAdminAsync(AppDb db, Guid familyId, Guid userId, CancellationToken ct)
{
    if (userId == Guid.Empty) return false;
    return await db.FamilyMembers
        .AnyAsync(m => m.FamilyId == familyId && m.UserId == userId && m.IsAdmin, ct);
}

private static async Task<bool> IsMessageAuthorAsync(AppDb db, Guid familyId, Guid messageId, Guid userId, CancellationToken ct)
{
    if (userId == Guid.Empty) return false;
    return await db.ChatMessages
        .AnyAsync(m => m.Id == messageId && m.FamilyId == familyId && m.AuthorUserId == userId && !m.IsDeleted, ct);
}
    public static IEndpointRouteBuilder MapFamiliesEndpoints(this IEndpointRouteBuilder app)
    {
var group = app.MapGroup("/families").RequireAuthorization().WithTags("Families");

        // POST /families — создание семьи
        group.MapPost("/", async (
            [FromBody] CreateFamilyRequest req,
            FamilyService familyService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new ProblemDetails { Title = "Название семьи обязательно." });

            var family = await familyService.CreateFamilyAsync(req.Name.Trim(), ct);

            return Results.Created($"/families/{family.Id}", new CreateFamilyResponse
            {
                Id = family.Id,
                Name = family.Name,
                InviteCode = family.InviteCode,
                CreatedAtUtc = family.CreatedAtUtc
            });
        })
        .WithName("FamiliesCreate")
        .Produces<CreateFamilyResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // POST /families/join-by-code — вступление по коду
        group.MapPost("/join-by-code", async (
            [FromBody] JoinByCodeRequest req,
            AppDb db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.InviteCode))
                return Results.BadRequest(new ProblemDetails { Title = "Код приглашения обязателен." });

            if (req.UserId == Guid.Empty)
                return Results.BadRequest(new ProblemDetails { Title = "UserId обязателен." });

            var code = req.InviteCode.Trim().ToUpperInvariant();

            var family = await db.Families
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.InviteCode == code, ct);

            if (family is null)
                return Results.NotFound(new ProblemDetails { Title = "Семья с таким кодом не найдена." });

            var userExists = await db.Users.AnyAsync(u => u.Id == req.UserId, ct);
            if (!userExists)
                return Results.NotFound(new ProblemDetails { Title = "Пользователь не найден." });

            // проверка, что пользователь ещё не состоит в этой семье
            var alreadyMember = await db.FamilyMembers
                .AnyAsync(x => x.FamilyId == family.Id && x.UserId == req.UserId, ct);

            if (alreadyMember)
                return Results.Conflict(new ProblemDetails { Title = "Пользователь уже является участником семьи." });

            var member = new FamilyMember
            {
                FamilyId = family.Id,
                UserId = req.UserId,
                RoleInFamily = string.IsNullOrWhiteSpace(req.RoleInFamily) ? "Member" : req.RoleInFamily!.Trim(),
                IsAdmin = req.IsAdmin
            };

            db.FamilyMembers.Add(member);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new JoinByCodeResponse
            {
                FamilyId = family.Id,
                FamilyName = family.Name,
                UserId = req.UserId,
                RoleInFamily = member.RoleInFamily,
                IsAdmin = member.IsAdmin
            });
        })
        .WithName("FamiliesJoinByCode")
        .Produces<JoinByCodeResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /families/{familyId}/members — список участников
        group.MapGet("/{familyId:guid}/members", async (
            Guid familyId,
            AppDb db,
            CancellationToken ct) =>
        {
            var exists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
            if (!exists)
                return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

            var items = await db.FamilyMembers
                .Where(m => m.FamilyId == familyId)
                .Include(m => m.User)
                .OrderByDescending(m => m.IsAdmin)
                .ThenBy(m => m.User.LastName)
                .ThenBy(m => m.User.FirstName)
                .Select(m => new FamilyMemberDto
                {
                    Id = m.Id,
                    FamilyId = m.FamilyId,
                    UserId = m.UserId,
                    FullName = $"{m.User.LastName} {m.User.FirstName}{(m.User.MiddleName != null ? " " + m.User.MiddleName : "")}",
                    RoleInFamily = m.RoleInFamily,
                    IsAdmin = m.IsAdmin
                })
                .ToListAsync(ct);

            return Results.Ok(items);
        })
        .WithName("FamiliesMembersList")
        .Produces<List<FamilyMemberDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /families/{familyId}/messages?beforeUtc=&take=
        group.MapGet("/{familyId:guid}/messages", async (
            Guid familyId,
            [FromQuery] DateTime? beforeUtc,
            [FromQuery] int? take,
            AppDb db,
            CancellationToken ct) =>
        {
            var exists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
            if (!exists)
                return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

            var limit = Math.Clamp(take ?? 50, 1, 200);

            var q = db.ChatMessages
                .AsNoTracking()
                .Where(x => x.FamilyId == familyId && !x.IsDeleted);

            if (beforeUtc is not null)
                q = q.Where(x => x.SentAtUtc < beforeUtc);

            var items = await q
                .OrderByDescending(x => x.SentAtUtc)
                .Take(limit)
                .Select(x => new ChatMessageDto
                {
                    Id = x.Id,
                    FamilyId = x.FamilyId,
                    AuthorUserId = x.AuthorUserId,
                    Text = x.Text,
                    SentAtUtc = x.SentAtUtc,
                    EditedAtUtc = x.EditedAtUtc
                })
                .ToListAsync(ct);

            // возвращаем в хронологическом порядке (по возрастанию)
            items.Reverse();
            return Results.Ok(items);
        })
        .WithName("FamiliesMessagesList")
        .Produces<List<ChatMessageDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // POST /families/{familyId}/messages — отправить сообщение
        group.MapPost("/{familyId:guid}/messages", async (
            Guid familyId,
            [FromBody] SendMessageRequest req,
            AppDb db,
            CancellationToken ct) =>
        {
            if (req.AuthorUserId == Guid.Empty)
                return Results.BadRequest(new ProblemDetails { Title = "AuthorUserId обязателен." });

            if (string.IsNullOrWhiteSpace(req.Text))
                return Results.BadRequest(new ProblemDetails { Title = "Текст сообщения обязателен." });

            var familyExists = await db.Families.AnyAsync(f => f.Id == familyId, ct);
            if (!familyExists)
                return Results.NotFound(new ProblemDetails { Title = "Семья не найдена." });

            // можно дополнительно проверить, что автор состоит в семье
            var isMember = await db.FamilyMembers.AnyAsync(m => m.FamilyId == familyId && m.UserId == req.AuthorUserId, ct);
            if (!isMember)
                return Results.Forbid();

            var msg = new ChatMessage
            {
                FamilyId = familyId,
                AuthorUserId = req.AuthorUserId,
                Text = req.Text.Trim(),
                SentAtUtc = DateTime.UtcNow
            };

            db.ChatMessages.Add(msg);
            await db.SaveChangesAsync(ct);

            var dto = new ChatMessageDto
            {
                Id = msg.Id,
                FamilyId = msg.FamilyId,
                AuthorUserId = msg.AuthorUserId,
                Text = msg.Text,
                SentAtUtc = msg.SentAtUtc,
                EditedAtUtc = msg.EditedAtUtc
            };

            return Results.Created($"/families/{familyId}/messages/{msg.Id}", dto);
        })
        .WithName("FamiliesMessagesSend")
        .Produces<ChatMessageDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
// DELETE /families/{familyId}/members/{memberId} — удалить участника
group.MapDelete("/{familyId:guid}/members/{memberId:guid}", async (
    Guid familyId,
    Guid memberId,
    AppDb db,
    HttpContext http,
    CancellationToken ct) =>
{
    var member = await db.FamilyMembers
        .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct);

    if (member is null)
        return Results.NotFound(new ProblemDetails { Title = "Участник не найден." });

    var currentUserId = GetCurrentUserId(http);
    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);
    if (!isAdmin)
        return Results.Forbid();

    db.FamilyMembers.Remove(member);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
})
.WithName("FamiliesMemberDelete")
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);

// PATCH /families/{familyId}/members/{memberId} — обновить роль/флаги участника
group.MapPatch("/{familyId:guid}/members/{memberId:guid}", async (
    Guid familyId,
    Guid memberId,
    [FromBody] UpdateMemberRequest req,
    AppDb db,
    HttpContext http,
    CancellationToken ct) =>
{
    var member = await db.FamilyMembers
        .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == familyId, ct);

    if (member is null)
        return Results.NotFound(new ProblemDetails { Title = "Участник не найден." });

    var currentUserId = GetCurrentUserId(http);
    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);
    if (!isAdmin)
        return Results.Forbid();

    if (!string.IsNullOrWhiteSpace(req.RoleInFamily))
        member.RoleInFamily = req.RoleInFamily!.Trim();

    if (req.IsAdmin.HasValue)
        member.IsAdmin = req.IsAdmin.Value;

    await db.SaveChangesAsync(ct);

    // при желании добавь Include(User) для ФИО
    return Results.Ok(new FamilyMemberDto
    {
        Id = member.Id,
        FamilyId = member.FamilyId,
        UserId = member.UserId,
        FullName = "",
        RoleInFamily = member.RoleInFamily,
        IsAdmin = member.IsAdmin
    });
})
.WithName("FamiliesMemberUpdate")
.Produces<FamilyMemberDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);


// POST /families/{familyId}/leave — текущий пользователь покидает семью
group.MapPost("/{familyId:guid}/leave", async (
    Guid familyId,
    [FromBody] LeaveFamilyRequest req,
    AppDb db,
    HttpContext http,
    CancellationToken ct) =>
{
    if (req.UserId == Guid.Empty)
        return Results.BadRequest(new ProblemDetails { Title = "UserId обязателен." });

    var member = await db.FamilyMembers
        .FirstOrDefaultAsync(m => m.FamilyId == familyId && m.UserId == req.UserId, ct);

    if (member is null)
        return Results.NotFound(new ProblemDetails { Title = "Пользователь не состоит в семье." });

    var currentUserId = GetCurrentUserId(http);
    var isSelf = currentUserId == req.UserId;
    var isAdmin = await IsFamilyAdminAsync(db, familyId, currentUserId, ct);

    if (!isSelf && !isAdmin)
        return Results.Forbid();

    db.FamilyMembers.Remove(member);
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
})
.WithName("FamiliesLeave")
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);


// PATCH /families/{familyId}/messages/{messageId} — редактировать сообщение
group.MapPatch("/{familyId:guid}/messages/{messageId:guid}", async (
    Guid familyId,
    Guid messageId,
    [FromBody] EditMessageRequest req,
    AppDb db,
    HttpContext http,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new ProblemDetails { Title = "Текст обязателен." });

    var msg = await db.ChatMessages
        .FirstOrDefaultAsync(m => m.Id == messageId && m.FamilyId == familyId && !m.IsDeleted, ct);

    if (msg is null)
        return Results.NotFound(new ProblemDetails { Title = "Сообщение не найдено." });

    var currentUserId = GetCurrentUserId(http);
    var isAuthor = msg.AuthorUserId == currentUserId;
    if (!isAuthor)
        return Results.Forbid();

    msg.Text = req.Text.Trim();
    msg.EditedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Ok(new ChatMessageDto
    {
        Id = msg.Id,
        FamilyId = msg.FamilyId,
        AuthorUserId = msg.AuthorUserId,
        Text = msg.Text,
        SentAtUtc = msg.SentAtUtc,
        EditedAtUtc = msg.EditedAtUtc
    });
})
.WithName("FamiliesMessageEdit")
.Produces<ChatMessageDto>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);


// DELETE /families/{familyId}/messages/{messageId} — удалить сообщение
group.MapDelete("/{familyId:guid}/messages/{messageId:guid}", async (
    Guid familyId,
    Guid messageId,
    AppDb db,
    HttpContext http,
    CancellationToken ct) =>
{
    var msg = await db.ChatMessages
        .FirstOrDefaultAsync(m => m.Id == messageId && m.FamilyId == familyId && !m.IsDeleted, ct);

    if (msg is null)
        return Results.NotFound(new ProblemDetails { Title = "Сообщение не найдено или уже удалено." });

    var currentUserId = GetCurrentUserId(http);
    var isAuthor = msg.AuthorUserId == currentUserId;
    if (!isAuthor)
        return Results.Forbid();

    msg.IsDeleted = true;
    msg.EditedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.NoContent();
})
.WithName("FamiliesMessageDelete")
.Produces(StatusCodes.Status204NoContent)
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
    public Guid Id { get; set; }
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
    public Guid? AuthorUserId { get; set; } // если будешь проверять автора
    public string Text { get; set; } = default!;
}
