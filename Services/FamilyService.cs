using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Zabota.Contracts;
using Zabota.Data;
using Zabota.Models;

public class FamilyService
{
    private readonly AppDb _db;

    public FamilyService(AppDb db)
    {
        _db = db;
    }

    private static string GenerateInviteCode12()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new byte[12];
        RandomNumberGenerator.Fill(bytes);

        var sb = new StringBuilder(12);
        foreach (var b in bytes)
            sb.Append(chars[b % chars.Length]);

        return sb.ToString();
    }

    public async Task<Family> CreateFamilyAsync(string name, CancellationToken ct = default)
    {
        for (var i = 0; i < 5; i++) // несколько попыток на случай коллизии
        {
            var code = GenerateInviteCode12();
            var exists = await _db.Families.AnyAsync(f => f.InviteCode == code, ct);

            if (!exists)
            {
                var family = new Family
                {
                    Name = name,
                    InviteCode = code,
                    CreatedAtUtc = DateTime.UtcNow,
                };

                _db.Families.Add(family);
                await _db.SaveChangesAsync(ct);

                return family;
            }
        }

        throw new InvalidOperationException("Не удалось сгенерировать уникальный код семьи.");
    }
}
