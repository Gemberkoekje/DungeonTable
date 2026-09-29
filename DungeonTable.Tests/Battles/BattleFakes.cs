using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Battle;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Battles;

/// <summary>In-memory party roster, so the battle's party seeding can be tested without a file.</summary>
internal sealed class FakePartyRoster : IPartyRoster
{
    /// <summary>The members <see cref="GetParty"/> serves.</summary>
    public List<PartyMember> Members { get; } = new List<PartyMember>();

    /// <summary>When true, <see cref="GetParty"/> reports an unreadable roster.</summary>
    public bool Broken { get; set; }

    /// <summary>The last roster handed to <see cref="Save"/>, or null.</summary>
    public Party Saved { get; private set; }

    public Result<Party> GetParty() => Broken
        ? Result.WithMessages<Party>(ValidationMessage.Error("Unreadable.", nameof(Broken)))
        : Result.For(new Party { Members = Members.ToArray() });

    public Result Save(Party party)
    {
        Saved = party;
        return Result.OK;
    }
}

/// <summary>In-memory ally roster, mirroring <see cref="FakePartyRoster"/>.</summary>
internal sealed class FakeAllyRoster : IAllyRoster
{
    /// <summary>The members <see cref="GetAllies"/> serves.</summary>
    public List<AllyMember> Members { get; } = new List<AllyMember>();

    /// <summary>The last roster handed to <see cref="Save"/>, or null.</summary>
    public AllyRoster Saved { get; private set; }

    public Result<AllyRoster> GetAllies() => Result.For(new AllyRoster { Members = Members.ToArray() });

    public Result Save(AllyRoster roster)
    {
        Saved = roster;
        return Result.OK;
    }
}
