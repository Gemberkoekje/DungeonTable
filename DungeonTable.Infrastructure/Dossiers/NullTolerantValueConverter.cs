using System.Text.Json.Serialization;

namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// Reads a JSON <c>null</c> written where a number, a true/false flag or an enum value belongs as
/// that type's default, which is what the field holds when it is left out: <c>0</c>, <c>false</c>,
/// or the enum's <c>None</c>. Anything else is read, and everything is written, by the converter the
/// serializer would have used without it; enums are read and written in camelCase.
/// </summary>
/// <remarks>
/// <para>
/// Documents written by hand, and above all by an LLM, say "nothing here" with <c>null</c>:
/// <c>"armourClass": null</c> for an NPC who never fights, <c>"secret": null</c>,
/// <c>"kind": null</c>. The serializer cannot put a <c>null</c> in a number, so each of those failed
/// the whole document, and a tab went empty over one field. Every Core type the documents are read
/// into leaves these fields at their default when they are not written, so the default is exactly
/// what leaving the field out means.
/// </para>
/// <para>
/// An enum value the app does not know still fails the document: it is not "nothing", it is a
/// value the app cannot place.
/// </para>
/// </remarks>
internal sealed class NullTolerantValueConverter : JsonConverterFactory
{
    // Enums are written the way every document writes them, and the way the stores have always
    // read them.
    private static readonly JsonStringEnumConverter Enums = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

    private static readonly HashSet<Type> Primitives = new HashSet<Type>
    {
        typeof(int), typeof(long), typeof(double), typeof(float), typeof(decimal), typeof(bool),
    };

    // A number written as a string ("page": "86") is read by the serializer's own converter, where the
    // store's options allow it; the book index's do. Only the number handling matters here.
    private static readonly JsonSerializerOptions NumbersFromStrings = new JsonSerializerOptions
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null && (typeToConvert.IsEnum || Primitives.Contains(typeToConvert));

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        JsonConverter inner = typeToConvert.IsEnum
            ? Enums.CreateConverter(typeToConvert, options)
            : JsonSerializerOptions.Default.GetConverter(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(typeof(NullAsDefault<>).MakeGenericType(typeToConvert), inner);
    }

    // Internal rather than private only because it is made through reflection, which Sonar cannot see.
    internal sealed class NullAsDefault<T> : JsonConverter<T>
        where T : struct
    {
        private readonly JsonConverter<T> inner;

        /// <summary>Wraps the converter the serializer would otherwise use.</summary>
        /// <param name="inner">That converter, for <typeparamref name="T"/>.</param>
        public NullAsDefault(JsonConverter inner)
        {
            this.inner = (JsonConverter<T>)inner;
        }

        public override bool HandleNull => true;

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default;
            }

            // A wrapped converter is not the serializer's own number converter, so the serializer no
            // longer applies the options' number handling to it; this does, for the stores that ask.
            if (reader.TokenType == JsonTokenType.String
                && typeof(T) != typeof(bool)
                && !typeof(T).IsEnum
                && (options.NumberHandling & JsonNumberHandling.AllowReadingFromString) != 0)
            {
                return JsonSerializer.Deserialize<T>(ref reader, NumbersFromStrings);
            }

            return inner.Read(ref reader, typeToConvert, options);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            inner.Write(writer, value, options);
    }
}
