using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct CharacterProficiencyId : IEquatable<CharacterProficiencyId>
{
    public CharacterProficiencyId(string value) =>
        Value = value?.Trim() ?? string.Empty;

    public string Value { get; }
    public bool IsValid => Value?.StartsWith(
        "proficiency:",
        StringComparison.Ordinal) == true;

    public bool Equals(CharacterProficiencyId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is CharacterProficiencyId other && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
    public override string ToString() => Value;
    public static bool operator ==(
        CharacterProficiencyId left,
        CharacterProficiencyId right) => left.Equals(right);
    public static bool operator !=(
        CharacterProficiencyId left,
        CharacterProficiencyId right) => !left.Equals(right);
}

public static class BuiltInCharacterProficiencyIds
{
    public static readonly CharacterProficiencyId Fieldwork =
        new("proficiency:fieldwork");
    public static readonly CharacterProficiencyId ConstructionEngineering =
        new("proficiency:construction-engineering");
    public static readonly CharacterProficiencyId Crafting =
        new("proficiency:crafting");
    public static readonly CharacterProficiencyId FoodProduction =
        new("proficiency:food-production");
    public static readonly CharacterProficiencyId Scholarship =
        new("proficiency:scholarship");
    public static readonly CharacterProficiencyId Medicine =
        new("proficiency:medicine");
    public static readonly CharacterProficiencyId Social =
        new("proficiency:social");
    public static readonly CharacterProficiencyId MeleeCombat =
        new("proficiency:melee-combat");
    public static readonly CharacterProficiencyId RangedCombat =
        new("proficiency:ranged-combat");

    public static readonly IReadOnlyList<CharacterProficiencyId> All =
        new[]
        {
            Fieldwork,
            ConstructionEngineering,
            Crafting,
            FoodProduction,
            Scholarship,
            Medicine,
            Social,
            MeleeCombat,
            RangedCombat
        };
}

public enum CharacterProficiencyRank
{
    Apprentice = 0,
    Skilled = 1,
    Technician = 2,
    Expert = 3,
    Master = 4
}

public enum ProficiencyCombinationMode
{
    PrimaryOnly = 0,
    Weighted = 1,
    Higher = 2
}

[Serializable]
public sealed class ProficiencyWorkProfileAuthoring
{
    [SerializeField] private string primaryProficiencyId = string.Empty;
    [SerializeField] private string secondaryProficiencyId = string.Empty;
    [Range(0f, 1f), SerializeField] private float primaryWeight = 1f;
    [SerializeField] private ProficiencyCombinationMode combinationMode;
    [SerializeField] private CharacterProficiencyRank recommendedRank;
    [SerializeField] private CharacterProficiencyRank minimumRiskRank;

    public CharacterProficiencyId Primary => new(primaryProficiencyId);
    public CharacterProficiencyId Secondary => new(secondaryProficiencyId);
    public float PrimaryWeight => Mathf.Clamp01(primaryWeight);
    public float SecondaryWeight => Secondary.IsValid ? 1f - PrimaryWeight : 0f;
    public ProficiencyCombinationMode CombinationMode => combinationMode;
    public CharacterProficiencyRank RecommendedRank => recommendedRank;
    public CharacterProficiencyRank MinimumRiskRank => minimumRiskRank;
    public bool IsValid => Primary.IsValid;

#if UNITY_EDITOR
    public void Configure(
        CharacterProficiencyId primary,
        CharacterProficiencyId secondary = default,
        float authoredPrimaryWeight = 1f,
        CharacterProficiencyRank authoredRecommendedRank =
            CharacterProficiencyRank.Apprentice,
        CharacterProficiencyRank authoredMinimumRiskRank =
            CharacterProficiencyRank.Apprentice,
        ProficiencyCombinationMode authoredCombinationMode =
            ProficiencyCombinationMode.Weighted)
    {
        primaryProficiencyId = primary.Value ?? string.Empty;
        secondaryProficiencyId = secondary.Value ?? string.Empty;
        primaryWeight = Mathf.Clamp01(authoredPrimaryWeight);
        combinationMode = secondary.IsValid
            ? authoredCombinationMode
            : ProficiencyCombinationMode.PrimaryOnly;
        recommendedRank = authoredRecommendedRank;
        minimumRiskRank = authoredMinimumRiskRank;
    }
#endif
}
