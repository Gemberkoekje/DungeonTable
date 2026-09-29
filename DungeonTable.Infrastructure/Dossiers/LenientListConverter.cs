using System.Text.Json.Serialization;

namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// Reads an <see cref="IReadOnlyList{T}"/> whose JSON value is <c>null</c> as an empty list, and drops
/// a <c>null</c> entry inside one, so a hand-edited document that clears a list with <c>null</c>
/// instead of <c>[]</c>, or leaves a <c>null</c> where an entry was, loads rather than taking the
/// whole store down. Used by every store that reads content.
/// </summary>
/// <remarks>
/// <para>
/// The documents are <b>hand-edited</b>, and every Core type they are read into defaults its lists to
/// <see cref="Array.Empty{T}"/> — but that default is only a starting value: an explicit JSON
/// <c>null</c> overwrites it, leaving a genuinely null list behind on an object the deserializer
/// still reports as read successfully. Every consumer then dereferences it (<c>quests.Quests</c> in
/// the merge loop, <c>deck.Cards.Count</c> in the Rules tab), so one stray <c>null</c> threw out of
/// the store's constructor — breaking startup — or out of a tab's render, in direct contradiction
/// of the store's documented promise that a corrupt document is skipped rather than fatal.
/// </para>
/// <para>
/// A <c>null</c> entry did the same one level down: the dossier store reads every area's and quest's
/// id, the link index every NPC's, the Rules tab every card. It holds nothing to show, so it is dropped
/// here, whatever the list holds: an object, a string (a prerequisite's quest ids), or a value such as
/// a map region's corner. The entry is skipped before the entry's own converter sees it, so a
/// <c>null</c> in a list of strings is dropped rather than read as an empty string. The content tests
/// say where each one is.
/// </para>
/// <para>
/// Fixed at the loader for the same reason as <see cref="NullMeansLeftOut"/>: this is the trust
/// boundary where hand-authored JSON enters, and guarding it here covers every list on every type the
/// stores read at once instead of a null check at each of the couple of dozen places one is read.
/// </para>
/// </remarks>
internal sealed class LenientListConverter : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null
        && typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        Type item = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(NullTolerantList<>).MakeGenericType(item));
    }

    // Reads the array one entry at a time, so a null entry is passed over before the entry's own
    // converter could turn it into something (an empty string, an exception for a value type).
    private sealed class NullTolerantList<T> : JsonConverter<IReadOnlyList<T>>
    {
        /// <summary>
        /// Opts this converter in to seeing a JSON <c>null</c> at all. Without it
        /// <see cref="JsonSerializer"/> short-circuits null for any reference type and assigns it
        /// straight to the property, never calling <see cref="Read"/> — which is precisely the case
        /// this converter exists to handle.
        /// </summary>
        public override bool HandleNull => true;

        public override IReadOnlyList<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Array.Empty<T>();
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected a list, found {reader.TokenType}.");
            }

            var read = new List<T>();
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.EndArray:
                        return read;

                    case JsonTokenType.Null:
                        continue;

                    default:
                        T item = JsonSerializer.Deserialize<T>(ref reader, options);
                        if (item is not null)
                        {
                            read.Add(item);
                        }

                        break;
                }
            }

            throw new JsonException("The list is not closed.");
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value is null ? new List<T>() : value.ToList(), options);
    }
}
