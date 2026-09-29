using System.Text.Json.Serialization;

namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// Reads a JSON string that may arrive as a bare number or a true/false, so a text field written as
/// <c>4</c> loads as "4" instead of failing the whole document. Used by every store that reads content.
/// </summary>
/// <remarks>
/// <para>
/// The dossiers need it on purpose: an area's creature count is a number or a dice expression
/// (<c>"count": 4</c>, <c>"count": "1d4+1"</c>), and an author should be able to write the first as
/// the number it is. A document is deserialized in one pass, so one loosely typed field would
/// otherwise take the whole document, and everything it describes, down with it; a stat block's
/// <c>"challengeRating": 2</c> did exactly that.
/// </para>
/// <para>
/// A <c>null</c> never reaches it: the serializer hands a string converter a <c>null</c> only when
/// it asks for one, and this one does not, so a <c>null</c> text field goes to the property's setter,
/// where <see cref="NullMeansLeftOut"/> leaves the field as it was.
/// </para>
/// </remarks>
internal sealed class LenientStringConverter : JsonConverter<string>
{
    /// <inheritdoc />
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;

            case JsonTokenType.Null:
                return string.Empty;

            case JsonTokenType.Number:
                return reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

            case JsonTokenType.True:
                return bool.TrueString;

            case JsonTokenType.False:
                return bool.FalseString;

            default:
                throw new JsonException(
                    $"Expected a string, number or boolean for a text field, found {reader.TokenType}.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
