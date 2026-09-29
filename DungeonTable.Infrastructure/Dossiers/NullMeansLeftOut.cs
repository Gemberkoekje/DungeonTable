using System.Text.Json.Serialization.Metadata;

namespace DungeonTable.Infrastructure.Dossiers;

/// <summary>
/// Makes a JSON <c>null</c> written for a text or object field read as if the field were left out:
/// the field keeps the value its Core type starts it with (an empty string, an empty object), rather
/// than holding a <c>null</c> for the first page that reads it to trip over.
/// </summary>
/// <remarks>
/// <para>
/// Every Core type the documents are read into starts its text at <see cref="string.Empty"/> and its
/// objects at an empty instance, and the app relies on that: nothing checks a title or a name for
/// <c>null</c>. An explicit <c>null</c> in the JSON overwrote that start value, and the deserializer
/// still called the document read, so <c>"title": null</c> on an area or <c>"name": null</c> on an
/// NPC answered the DM screen with an error. LLMs write <c>null</c> for "nothing here" readily.
/// </para>
/// <para>
/// This is a contract modifier rather than a converter because "left out" is a property of the
/// field, not of its type: a converter can only return a value, and the right value is whatever the
/// field already holds. So the property's setter simply declines a <c>null</c>. Lists, numbers, flags
/// and enums are the converters' part (<see cref="LenientListConverter"/>,
/// <see cref="NullTolerantValueConverter"/>); a whole document that is <c>null</c> is still no
/// document.
/// </para>
/// </remarks>
internal static class NullMeansLeftOut
{
    /// <summary>A resolver for a store's reading options that applies the rule to every type it reads.</summary>
    /// <returns>A new resolver.</returns>
    internal static IJsonTypeInfoResolver Resolver() =>
        new DefaultJsonTypeInfoResolver { Modifiers = { Modify } };

    // Wraps the setter of every reference-typed property so that a null leaves it as it was. Value
    // types never reach a setter as null.
    private static void Modify(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            Action<object, object> set = property.Set;
            if (set is null || property.PropertyType.IsValueType)
            {
                continue;
            }

            property.Set = (target, value) =>
            {
                if (value is not null)
                {
                    set(target, value);
                }
            };
        }
    }
}
