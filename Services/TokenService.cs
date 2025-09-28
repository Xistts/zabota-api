using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Zabota.Data;
using Zabota.Models;

public class TokenService
{
    private readonly IConfiguration _cfg;
    private readonly AppDb _db;

    public TokenService(IConfiguration cfg, AppDb db)
    {
        _cfg = cfg;
        _db = db;
    }

    public (string token, DateTime expiresAtUtc) CreateAccessToken(User user)
    {
        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var minutes = int.Parse(jwt["AccessMinutes"] ?? "15");
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            // Если у тебя нет свойства Login — не добавляем
            // new("login", user.Login),
            new("firstName", user.FirstName),
            new("lastName", user.LastName),
        };

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds
        );

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return (encoded, expires);
    }

    public async Task<(string token, DateTime expiresAtUtc)> IssueRefreshTokenAsync(
        User user,
        CancellationToken ct = default
    )
    {
        var days = int.Parse(_cfg.GetSection("Jwt")["RefreshDays"] ?? "90");
        var expires = DateTime.UtcNow.AddDays(days);

        var raw =
            Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            + "."
            + Guid.NewGuid().ToString("N");

        var rt = new RefreshToken
        {
            UserId = user.Id,
            Token = raw,
            ExpiresAtUtc = expires,
        };

        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync(ct);

        return (raw, expires);
    }

    public async Task InvalidateRefreshTokenAsync(string token, CancellationToken ct = default)
    {
        var rt = await _db.RefreshTokens.SingleOrDefaultAsync(x => x.Token == token, ct);
        if (rt is null)
            return;
        rt.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task InvalidateAllUserRefreshTokensAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var tokens = await _db
            .RefreshTokens.Where(x =>
                x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow
            )
            .ToListAsync(ct);

        foreach (var t in tokens)
            t.RevokedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
