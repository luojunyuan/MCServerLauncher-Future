using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Linq;

namespace MCServerLauncher.Common.ProtoType.Serialization;

/// <summary>
/// Guid converter that falls back to Guid.Empty on invalid strings.
/// </summary>
public sealed class GuidStjConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return Guid.TryParse(str, out var result) ? result : Guid.Empty;
        }
        throw new JsonException($"Cannot convert {reader.TokenType} to Guid");
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    public override Guid ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Guid.TryParse(reader.GetString(), out var result) ? result : Guid.Empty;
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
    }
}

/// <summary>
/// Encoding converter using WebName for serialization/deserialization.
/// </summary>
public sealed class EncodingStjConverter : JsonConverter<Encoding>
{
    public override Encoding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var webName = reader.GetString();
            return Encoding.GetEncoding(webName!);
        }
        throw new JsonException($"Cannot convert {reader.TokenType} to Encoding");
    }

    public override void Write(Utf8JsonWriter writer, Encoding value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.WebName);
    }
}

/// <summary>
/// PlaceHolderString converter: serializes as pattern string, deserializes from string or null.
/// </summary>
public sealed class PlaceHolderStringStjConverter : JsonConverter<PlaceHolderString>
{
    public override PlaceHolderString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var pattern = reader.GetString();
            return string.IsNullOrEmpty(pattern) ? null : new PlaceHolderString(pattern);
        }

        throw new JsonException($"Cannot convert {reader.TokenType} to PlaceHolderString");
    }

    public override void Write(Utf8JsonWriter writer, PlaceHolderString? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(value.Pattern);
    }
}

/// <summary>
/// A source-generator and Native AOT friendly string enum converter using the
/// protocol's lower snake-case representation.
/// </summary>
public sealed class SnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly JsonNamingPolicy NamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    private static readonly IReadOnlyDictionary<string, TEnum> Values = CreateValues();

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (text is not null && Values.TryGetValue(text, out var value))
                return value;

            throw new JsonException($"Unknown {typeof(TEnum).Name} value '{text}'.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numericValue))
            return (TEnum)(object)numericValue;

        throw new JsonException($"Cannot convert {reader.TokenType} to {typeof(TEnum).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var name = value.ToString();
        if (name.Contains(", ", StringComparison.Ordinal))
        {
            name = string.Join(", ", name
                .Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(NamingPolicy.ConvertName));
        }
        else if (Enum.IsDefined(value))
        {
            name = NamingPolicy.ConvertName(name);
        }
        else
        {
            writer.WriteNumberValue((int)(object)value);
            return;
        }

        writer.WriteStringValue(name);
    }

    private static IReadOnlyDictionary<string, TEnum> CreateValues()
    {
        var values = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var name = value.ToString();
            values[name] = value;
            values[NamingPolicy.ConvertName(name)] = value;
        }

        return values;
    }
}
