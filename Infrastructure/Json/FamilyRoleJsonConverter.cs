using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zabota.Models;

public class FamilyRoleJsonConverter : JsonConverter<FamilyRole>
{
    public override FamilyRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (FamilyRoleRu.TryParseRussian(s, out var role))
            return role;

        throw new JsonException($"Неизвестная роль: '{s}'. Допустимо: {string.Join(", ", FamilyRoleRu.All().Select(x => x.Ru))}");
    }

    public override void Write(Utf8JsonWriter writer, FamilyRole value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(FamilyRoleRu.ToRussian(value));
    }
}
