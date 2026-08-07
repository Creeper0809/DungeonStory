using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "HeritableTrait",
    menuName = "DungeonStory/Population/Heritable Trait")]
public sealed class HeritableTraitDefinitionSO : ScriptableObject
{
    public string traitId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public HeritableTraitCategory category;
    public string incompatibilityGroup = string.Empty;
    [Range(-100, 100)] public int aptitudeModifier;
    public List<string> compatibleSpeciesTags = new();
    public List<HeritableTraitConsequence> consequences = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(traitId)) errors.Add("Heritable trait id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{traitId}' display name is required.");
        if (authoringRevision < 1) errors.Add($"'{traitId}' authoring revision must be positive.");
        if (consequences == null || consequences.Count == 0
            || consequences.Exists(value => value == null || !value.IsValid))
            errors.Add($"'{traitId}' requires valid gameplay consequences.");
        return errors;
    }
}

public enum HeritableTraitCategory
{
    Anatomy,
    Metabolism,
    Arcane,
    Reproduction,
    ImmunityLongevity
}

public enum HeritableTraitConsequenceKind
{
    None,
    Aptitude,
    EnvironmentalTolerance,
    DiseaseResistance,
    Fertility,
    AgingRate,
    AnatomyCapacity,
    ManaAffinity
}

[Serializable]
public sealed class HeritableTraitConsequence
{
    public HeritableTraitConsequenceKind kind;
    public string targetId = string.Empty;
    [Range(-1f, 1f)] public float multiplierDelta;
    public bool IsValid => kind != HeritableTraitConsequenceKind.None
        && Mathf.Abs(multiplierDelta) > 0.0001f;
}

public static class HeritableTraitModifierResolver
{
    public const float CombinedMinimum = -0.25f;
    public const float CombinedMaximum = 0.25f;

    public static float ResolveCappedDelta(
        IEnumerable<HeritableTraitDefinitionSO> traits,
        HeritableTraitConsequenceKind kind,
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException(
                "A hereditary modifier target id is required.",
                nameof(targetId));
        }

        float total = (traits ?? Array.Empty<HeritableTraitDefinitionSO>())
            .Where(trait => trait != null)
            .SelectMany(trait => trait.consequences
                ?? new List<HeritableTraitConsequence>())
            .Where(consequence => consequence != null
                && consequence.kind == kind
                && string.Equals(
                    consequence.targetId,
                    targetId,
                    StringComparison.Ordinal))
            .Sum(consequence => consequence.multiplierDelta);
        return Mathf.Clamp(total, CombinedMinimum, CombinedMaximum);
    }
}
