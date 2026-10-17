using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    menuName = "DungeonStory/Character/Acquired Trait Settings",
    order = 0)]
public sealed class CharacterAcquiredTraitSettingsSO : ScriptableObject
{
    [SerializeField] private string settingsId = string.Empty;
    [SerializeField, Min(1)] private int maximumActiveTraits;
    [SerializeField]
    private List<CharacterAcquiredTraitManifestationGateDefinition>
        manifestationGates = new();
    [Header("Narrative Formula")]
    [Tooltip("Immutable v1+ policy. Empty authoring is rejected; version 0 remains legacy save-only.")]
    [SerializeField] private CharacterAcquiredTraitFormulaPolicyDefinition formulaPolicy;
    [Tooltip("Formula v2+ drawbacks selected independently from positive trait modules.")]
    [SerializeField] private List<CharacterAcquiredTraitDrawbackCapabilityDefinition>
        drawbackCapabilities = new();

    public string SettingsId => settingsId?.Trim() ?? string.Empty;
    public int MaximumActiveTraits => maximumActiveTraits;
    public IReadOnlyList<CharacterAcquiredTraitManifestationGateDefinition>
        ManifestationGates => manifestationGates
            ??= new List<CharacterAcquiredTraitManifestationGateDefinition>();
    public CharacterAcquiredTraitFormulaPolicyDefinition FormulaPolicy => formulaPolicy;
    public IReadOnlyList<CharacterAcquiredTraitDrawbackCapabilityDefinition>
        DrawbackCapabilities => drawbackCapabilities
            ??= new List<CharacterAcquiredTraitDrawbackCapabilityDefinition>();

    public NarrativeFormulaStrengthPolicy RequireFormulaPolicy()
    {
        if (formulaPolicy == null)
            throw new InvalidOperationException("Acquired-trait formula policy is missing.");
        formulaPolicy.RequireGenerationContext();
        formulaPolicy.RequireCatalogSha256();
        return formulaPolicy.ToRuntime();
    }

    [GameplayInternalOnly(
        "The controlled editor catalog builder writes immutable acquired-trait formula authoring.",
        "CharacterAcquiredTraitFormulaCatalogAssetBuilder")]
    public void ApplyFormulaAuthoring(
        CharacterAcquiredTraitFormulaPolicyDefinition value,
        IEnumerable<CharacterAcquiredTraitDrawbackCapabilityDefinition> drawbacks = null)
    {
        formulaPolicy = value ?? throw new ArgumentNullException(nameof(value));
        if (drawbacks != null)
        {
            drawbackCapabilities = drawbacks
                .Where(item => item != null)
                .OrderBy(item => item.DrawbackId, StringComparer.Ordinal)
                .ToList();
        }
    }

    public bool TryGetGate(
        int meaningfulRecordMilestone,
        out CharacterAcquiredTraitManifestationGateDefinition gate)
    {
        gate = ManifestationGates.FirstOrDefault(value => value != null
            && value.MeaningfulRecordMilestone == meaningfulRecordMilestone);
        return gate != null;
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (SettingsId.Length == 0
            || !string.Equals(settingsId, SettingsId, StringComparison.Ordinal))
            errors.Add("Acquired-trait settings require a stable settings ID.");
        if (maximumActiveTraits <= 0)
            errors.Add("Acquired-trait settings require a positive active-trait limit.");
        if (ManifestationGates.Count == 0)
            errors.Add("Acquired-trait settings require at least one manifestation gate.");
        if (ManifestationGates.Any(value => value == null))
            errors.Add("Acquired-trait settings contain a null manifestation gate.");

        CharacterAcquiredTraitManifestationGateDefinition[] gates =
            ManifestationGates.Where(value => value != null).ToArray();
        if (gates.Any(value => value.MeaningfulRecordMilestone <= 0
            || value.Budget <= 0
            || !Enum.IsDefined(typeof(CharacterSkillRarity), value.Rarity)))
        {
            errors.Add(
                "Acquired-trait settings contain an invalid milestone, rarity, or budget.");
        }
        if (gates.Select(value => value.MeaningfulRecordMilestone)
            .Distinct().Count() != gates.Length)
        {
            errors.Add("Acquired-trait settings contain a duplicate manifestation gate.");
        }
        if (!gates.Select(value => value.MeaningfulRecordMilestone)
                .SequenceEqual(gates.Select(value => value.MeaningfulRecordMilestone)
                    .OrderBy(value => value)))
        {
            errors.Add(
                "Acquired-trait manifestation gates must be stored in ascending milestone order.");
        }
        if (formulaPolicy != null && formulaPolicy.formulaVersion >= 2)
        {
            if (DrawbackCapabilities.Count == 0
                || DrawbackCapabilities.Any(value => value == null))
            {
                errors.Add("Acquired-trait formula v2 requires non-null drawback capabilities.");
            }
            else
            {
                if (DrawbackCapabilities.Select(value => value.DrawbackId)
                    .Distinct(StringComparer.Ordinal).Count() != DrawbackCapabilities.Count)
                    errors.Add("Acquired-trait drawback capabilities contain duplicate IDs.");
                foreach (CharacterAcquiredTraitDrawbackCapabilityDefinition drawback
                         in DrawbackCapabilities)
                    errors.AddRange(drawback.ValidateDefinition());
            }
        }
        return errors;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitDrawbackCapabilityDefinition
{
    public string drawbackId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public List<CharacterNarrativeDomain> domainAffinities = new();
    public List<string> conflictGroups = new();
    [Min(1)] public int maximumCredit = 1;
    public List<GameplayEffectBinding> effects = new();
    public string capabilityId = string.Empty;
    public CharacterSkillCapabilityFormulaDefinition formula;

    public string DrawbackId => drawbackId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public IReadOnlyList<CharacterNarrativeDomain> DomainAffinities =>
        domainAffinities ??= new List<CharacterNarrativeDomain>();
    public IReadOnlyList<string> ConflictGroups =>
        conflictGroups ??= new List<string>();
    public int MaximumCredit => maximumCredit;
    public IReadOnlyList<GameplayEffectBinding> Effects =>
        effects ??= new List<GameplayEffectBinding>();
    public string CapabilityId => capabilityId?.Trim() ?? string.Empty;
    public CharacterSkillCapabilityFormulaDefinition Formula => formula;

    public NarrativeFormulaCapabilityDescriptor RequireFormulaDescriptor()
    {
        if (CapabilityId.Length == 0
            || !string.Equals(capabilityId, CapabilityId, StringComparison.Ordinal)
            || formula == null)
            throw new InvalidOperationException(
                $"Acquired-trait drawback '{DrawbackId}' has no complete formula descriptor.");
        NarrativeFormulaCapabilityDescriptor descriptor = formula.ToRuntime(CapabilityId);
        if (!string.Equals(descriptor.FormatterId, CapabilityId, StringComparison.Ordinal)
            || !string.Equals(descriptor.ApplicatorId, CapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Acquired-trait drawback '{DrawbackId}' formatter/applicator must equal capability ID.");
        return descriptor;
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (DrawbackId.Length == 0
            || !string.Equals(drawbackId, DrawbackId, StringComparison.Ordinal))
            errors.Add("Acquired-trait drawback requires a stable canonical ID.");
        if (DisplayName.Length == 0 || Description.Length == 0)
            errors.Add($"Acquired-trait drawback '{DrawbackId}' requires display text.");
        if (maximumCredit <= 0)
            errors.Add($"Acquired-trait drawback '{DrawbackId}' requires positive maximum credit.");
        if (DomainAffinities.Count == 0
            || DomainAffinities.Any(value =>
                !Enum.IsDefined(typeof(CharacterNarrativeDomain), value))
            || DomainAffinities.Distinct().Count() != DomainAffinities.Count)
            errors.Add($"Acquired-trait drawback '{DrawbackId}' has invalid domain affinities.");
        if (ConflictGroups.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || ConflictGroups.Distinct(StringComparer.Ordinal).Count()
                != ConflictGroups.Count)
            errors.Add($"Acquired-trait drawback '{DrawbackId}' has invalid conflict groups.");
        if (Effects.Count == 0 || Effects.Any(value => value == null))
            errors.Add($"Acquired-trait drawback '{DrawbackId}' requires gameplay effects.");
        GameplayEffectSourceRef source = new(
            GameplayEffectSourceKind.AcquiredTrait,
            DrawbackId.Length == 0 ? "invalid-drawback" : DrawbackId);
        foreach (GameplayEffectBinding binding in Effects.Where(value => value != null))
        {
            if (!binding.IsValidFor(source, out string reason))
                errors.Add($"Acquired-trait drawback '{DrawbackId}' {reason}.");
            else if (!AcquiredTraitBurdenTargetCatalog.IsHarmful(
                         binding.definition, binding.value, out reason))
                errors.Add($"Acquired-trait drawback '{DrawbackId}' {reason}.");
        }
        if (Effects.Where(value => value != null)
            .GroupBy(value => value.bindingId?.Trim() ?? string.Empty,
                StringComparer.Ordinal).Any(group => group.Count() > 1))
            errors.Add($"Acquired-trait drawback '{DrawbackId}' contains duplicate bindings.");
        try { RequireFormulaDescriptor(); }
        catch (Exception exception) { errors.Add(exception.Message); }
        return errors;
    }
}

[Serializable]
public sealed class CharacterAcquiredTraitFormulaPolicyDefinition
{
    [Min(1)] public int formulaVersion = 1;
    [Min(0)] public int baseBudget;
    [Min(0)] public int powerScale;
    [Min(0.0001f)] public float softCapK = 1f;
    [Min(0f)] public float minimumImportance;
    [Min(0f)] public float maximumImportance = 4f;
    public List<float> milestoneWeights = new();
    public NarrativeFormulaDrawbackCreditPolicyDefinition drawbackCredit = new();
    [Min(0)] public int triggerFrequencyUnits;
    public bool guaranteedProc = true;
    [Min(1)] public int targetCount = 1;
    public string catalogSha256 = string.Empty;

    public NarrativeFormulaStrengthPolicy ToRuntime() => new(
        formulaVersion, baseBudget, powerScale, softCapK,
        minimumImportance, maximumImportance,
        (milestoneWeights ?? new List<float>()).Select(value => (double)value));

    public NarrativeFormulaDrawbackCreditPolicy RequireDrawbackPolicy() =>
        (drawbackCredit ?? throw new InvalidOperationException(
            "Acquired-trait drawback-credit policy is missing.")).ToRuntime();

    public NarrativeFormulaGenerationCostContext RequireGenerationContext()
    {
        if (triggerFrequencyUnits < 0 || targetCount < 1)
            throw new InvalidOperationException("Acquired-trait formula generation context is invalid.");
        return new NarrativeFormulaGenerationCostContext(
            triggerFrequencyUnits, guaranteedProc, targetCount);
    }

    public string RequireCatalogSha256()
    {
        string canonical = catalogSha256?.Trim() ?? string.Empty;
        if (canonical.Length != 64
            || !string.Equals(canonical, catalogSha256, StringComparison.Ordinal)
            || canonical.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException(
                "Acquired-trait formula policy requires a canonical 64-character catalog SHA-256.");
        return canonical.ToLowerInvariant();
    }
}
