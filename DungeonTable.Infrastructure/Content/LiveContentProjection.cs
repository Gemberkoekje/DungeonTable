using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Links;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Content;

/// <summary>
/// The <see cref="IContentProjection"/> the pages see: briefings, cards and search, answered by the
/// projection built with the content showing now (<see cref="LiveContent.Current"/>).
/// </summary>
public sealed class LiveContentProjection : IContentProjection
{
    private readonly LiveContent live;

    /// <summary>Creates the projection over the live content.</summary>
    /// <param name="live">The live content.</param>
    public LiveContentProjection(LiveContent live)
    {
        ArgumentNullException.ThrowIfNull(live);
        this.live = live;
    }

    private AuthoredProjection Now => live.Current.Projection;

    /// <inheritdoc />
    public Result<RoomBriefing> GetBriefing(string nodeId) => Now.GetBriefing(nodeId);

    /// <inheritdoc />
    public IReadOnlyList<SearchMatch> SearchNodes(string query, int limit) => Now.SearchNodes(query, limit);

    /// <inheritdoc />
    public Result<ReferenceCard> GetReferenceCard(string nodeId) => Now.GetReferenceCard(nodeId);

    /// <inheritdoc />
    public IReadOnlyList<RelationLine> RelationsOf(string id) => Now.RelationsOf(id);

    /// <inheritdoc />
    public string StatBlockOf(string id) => Now.StatBlockOf(id);

    /// <inheritdoc />
    public Result<ReferenceCard> GetBookCard(string id) => Now.GetBookCard(id);
}
