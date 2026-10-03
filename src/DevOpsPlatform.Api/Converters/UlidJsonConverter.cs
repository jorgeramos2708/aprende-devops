namespace DevOpsPlatform.Api.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;

public class UlidJsonConverter : JsonConverter<Ulid>
{
    public override Ulid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (!string.IsNullOrEmpty(s) && Ulid.TryParse(s, out var u))
            return u;
        throw new JsonException("Ulid invalido o nulo.");
    }

    public override void Write(Utf8JsonWriter writer, Ulid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
