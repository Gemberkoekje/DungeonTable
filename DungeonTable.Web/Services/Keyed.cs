namespace DungeonTable.Web.Services;

/// <summary>An item of a rendered list, with the <c>@key</c> it is rendered under.</summary>
/// <typeparam name="T">The item's type.</typeparam>
/// <param name="Item">The item.</param>
/// <param name="Key">Its key: unique among its siblings, whatever its authored id.</param>
public readonly record struct Keyed<T>(T Item, object Key);
