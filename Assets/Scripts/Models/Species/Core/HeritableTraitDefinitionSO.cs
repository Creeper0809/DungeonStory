using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "HeritableTrait",
    menuName = "DungeonStory/Population/Heritable Trait")]
public sealed class HeritableTraitDefinitionSO : ScriptableObject
{
    [ContentIdentity] public string traitId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public HeritableTraitCategory category;
    public string incompatibilityGroup = string.Empty;
    [Range(-100, 100)] public int aptitudeModifier;
    [ContentReference(ContentReferenceKind.Species)]
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
    ManaAffinity,
    NeedRate,
    Movement,
    ManaOverloadDamage
}

[Serializable]
public sealed class HeritableTraitConsequence
{
    public HeritableTraitConsequenceKind kind;
    public string targetId = string.Empty;
    [Range(-1f, 1f)] public float multiplierDelta;
    public bool IsValid => float.IsFinite(multiplierDelta)
        && Mathf.Abs(multiplierDelta) > 0.0001f && Mathf.Abs(multiplierDelta) <= 1f
        && TargetsFor(kind).Contains(targetId, StringComparer.Ordinal);

    // Semantic consumer keys, not free-form SO identities.
    private static readonly IReadOnlyDictionary<HeritableTraitConsequenceKind, IReadOnlyList<string>> targets =
        Enum.GetValues(typeof(HeritableTraitConsequenceKind)).Cast<HeritableTraitConsequenceKind>()
            .ToDictionary(kind => kind, kind => (IReadOnlyList<string>)Array.AsReadOnly(CreateTargets(kind)));

    public static IReadOnlyList<string> TargetsFor(HeritableTraitConsequenceKind kind) =>
        targets.TryGetValue(kind, out var values) ? values : Array.Empty<string>();

    private static string[] CreateTargets(HeritableTraitConsequenceKind kind) => kind switch
    {
        HeritableTraitConsequenceKind.Aptitude => new[] { "rune-tuning", "work-burst" },
        HeritableTraitConsequenceKind.EnvironmentalTolerance => new[] { "cold", "dryness", "heat", "low-oxygen", "nutrition" },
        HeritableTraitConsequenceKind.DiseaseResistance => new[] { "all", "memory", "recovery", "toxin" },
        HeritableTraitConsequenceKind.Fertility => new[] { "cross-lineage", "gestation-stability", "offspring-stability", "success-rate" },
        HeritableTraitConsequenceKind.AgingRate => new[] { "biological-age" },
        HeritableTraitConsequenceKind.AnatomyCapacity => new[] { "bone-density", "dexterity", "joints", "mobility", "regeneration" },
        HeritableTraitConsequenceKind.ManaAffinity => new[] { "capacity", "dream", "stability" },
        HeritableTraitConsequenceKind.NeedRate => new[] { "hunger", "reproduction-hunger", "sleep" },
        HeritableTraitConsequenceKind.Movement => new[] { "move-speed" },
        HeritableTraitConsequenceKind.ManaOverloadDamage => new[] { "mana-exposure" },
        _ => Array.Empty<string>()
    };
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
