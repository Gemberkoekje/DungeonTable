using System.Collections.Generic;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>Small helpers the rules share.</summary>
internal static class Check
{
    /// <summary>Adds a fault when a value is blank.</summary>
    /// <param name="faults">Where the faults go.</param>
    /// <param name="value">The value.</param>
    /// <param name="fault">What to say when it is blank.</param>
    internal static void Written(List<string> faults, string value, string fault)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            faults.Add(fault);
        }
    }

    /// <summary>The ids that appear more than once, blank ones aside, each once, in the order first seen.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The repeated ids.</returns>
    internal static IReadOnlyList<string> Repeated(IEnumerable<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

    /// <summary>A result's messages, as one sentence.</summary>
    /// <typeparam name="T">The result's value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>Its messages.</returns>
    internal static string Messages<T>(Result<T> result) =>
        string.Join(" ", result.Messages.Select(message => message.Message));

    /// <summary>How a fault names an entry: by its id, or by its name or position when it has none.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="fallback">What to call it when the id is blank ("the NPC 'Wenna Brask'", "npcs[2]").</param>
    /// <returns>How to name it.</returns>
    internal static string Named(string id, string fallback) =>
        string.IsNullOrWhiteSpace(id) ? fallback : $"'{id}'";
}
