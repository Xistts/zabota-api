using System.Collections.Frozen;

namespace Zabota.Models;

public static class FamilyRoleRu
{
    private static readonly Dictionary<FamilyRole, string> _toRu = new()
    {
        { FamilyRole.Grandma,  "Бабушка" },
        { FamilyRole.Grandpa,  "Дедушка" },
        { FamilyRole.Mom,      "Мама" },
        { FamilyRole.Dad,      "Папа" },
        { FamilyRole.Daughter, "Дочь" },
        { FamilyRole.Son,      "Сын" },
    };

    private static readonly Dictionary<string, FamilyRole> _fromRu =
        _toRu.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static readonly FrozenDictionary<FamilyRole, string> ToRu = _toRu.ToFrozenDictionary();
    public static readonly FrozenDictionary<string, FamilyRole> FromRu = _fromRu.ToFrozenDictionary();

    public static string ToRussian(FamilyRole r) => ToRu[r];

    public static bool TryParseRussian(string? s, out FamilyRole role)
        => FromRu.TryGetValue((s ?? "").Trim(), out role);

    public static IEnumerable<(FamilyRole Enum, string Ru)> All() =>
        ToRu.Select(kv => (kv.Key, kv.Value));
}
