namespace DungeonTable.Web.Services;

/// <summary>
/// One of a room's ways out, with what is in the room it leads to: a line of the Room tab's Nearby list.
/// </summary>
/// <param name="Exit">The way out, as the room's briefing resolves it.</param>
/// <param name="Glance">What is in the room it leads to; <see cref="RoomGlance.None"/> when that room is not authored (yet).</param>
/// <param name="Badges">That room's badges as they stand now.</param>
public sealed record NearbyRoom(Core.Briefing.ConnectionEntry Exit, RoomGlance Glance, RoomBadges Badges);
