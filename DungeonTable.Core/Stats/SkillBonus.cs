namespace DungeonTable.Core.Stats;

/// <summary>A proficient skill, as printed in the stat block header ("Stealth +6").</summary>
public sealed class SkillBonus
{
    /// <summary>The skill name ("Stealth", "Survival").</summary>
    public string Skill { get; init; } = string.Empty;

    /// <summary>The printed bonus, including proficiency (and expertise, where applicable).</summary>
    public int Bonus { get; init; }
}
