using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    menuName = "DungeonStory/Character/Acquired Trait Module",
    order = 0)]
public sealed class CharacterAcquiredTraitModuleSO : ScriptableObject
{
    [SerializeField] private string moduleId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField, Min(1)] private int cost;
    [SerializeField] private List<string> conflictGroups = new();
    [SerializeField] private List<CharacterNarrativeDomain> domainAffinities = new();
    [SerializeField] private List<GameplayEffectBinding> effects = new();
    [SerializeField] private List<string> drawbackBindingIds = new();
    [Header("Special Reactions")]
    [SerializeField] private List<CharacterAcquiredTraitSpecialReactionDefinition>
        specialReactions = new();
    [Header("Narrative Formula")]
    [SerializeField] private string capabilityId = string.Empty;
    [SerializeField] private CharacterSkillCapabilityFormulaDefinition formula;

    public string ModuleId => moduleId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public int Cost => cost;
    public IReadOnlyList<string> ConflictGroups => conflictGroups
        ??= new List<string>();
    public IReadOnlyList<CharacterNarrativeDomain> DomainAffinities =>
        domainAffinities ??= new List<CharacterNarrativeDomain>();
    public IReadOnlyList<GameplayEffectBinding> Effects => effects
        ??= new List<GameplayEffectBinding>();
    public IReadOnlyList<string> DrawbackBindingIds => drawbackBindingIds
        ??= new List<string>();
    public IReadOnlyList<CharacterAcquiredTraitSpecialReactionDefinition>
        SpecialReactions => specialReactions
            ??= new List<CharacterAcquiredTraitSpecialReactionDefinition>();
    public string CapabilityId => capabilityId?.Trim() ?? string.Empty;
    public CharacterSkillCapabilityFormulaDefinition Formula => formula;

    public NarrativeFormulaCapabilityDescriptor RequireFormulaDescriptor()
    {
        if (CapabilityId.Length == 0
            || !string.Equals(capabilityId, CapabilityId, StringComparison.Ordinal)
            || formula == null)
            throw new InvalidOperationException(
                $"Acquired-trait module '{ModuleId}' has no complete formula descriptor.");
        NarrativeFormulaCapabilityDescriptor descriptor = formula.ToRuntime(CapabilityId);
        if (!string.Equals(descriptor.FormatterId, CapabilityId, StringComparison.Ordinal)
            || !string.Equals(descriptor.ApplicatorId, CapabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Acquired-trait module '{ModuleId}' formatter/applicator must equal capability ID.");
        return descriptor;
    }

    [GameplayInternalOnly(
        "The controlled editor catalog builder writes immutable acquired-trait formula authoring.",
        "CharacterAcquiredTraitFormulaCatalogAssetBuilder")]
    public void ApplyFormulaAuthoring(
        string nextCapabilityId,
        CharacterSkillCapabilityFormulaDefinition nextFormula)
    {
        string canonical = nextCapabilityId?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, nextCapabilityId, StringComparison.Ordinal))
            throw new ArgumentException("A canonical acquired-trait capability ID is required.", nameof(nextCapabilityId));
        capabilityId = canonical;
        formula = nextFormula ?? throw new ArgumentNullException(nameof(nextFormula));
    }

    [GameplayInternalOnly(
        "The controlled formula catalog builder writes explicit inseparable drawback bindings.",
        "CharacterAcquiredTraitFormulaCatalogAssetBuilder")]
    public void ApplyDrawbackBindingAuthoring(IEnumerable<string> bindingIds)
    {
        string[] values = (bindingIds ?? Array.Empty<string>()).ToArray();
        if (values.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Drawback binding IDs must be canonical and distinct.", nameof(bindingIds));
        drawbackBindingIds = values.OrderBy(value => value, StringComparer.Ordinal).ToList();
    }

    [GameplayInternalOnly(
        "The controlled content builder writes data-driven special reactions.",
        "V25AcquiredTraitContentAssetBuilder")]
    public void ApplySpecialReactionAuthoring(
        IEnumerable<CharacterAcquiredTraitSpecialReactionDefinition> reactions)
    {
        CharacterAcquiredTraitSpecialReactionDefinition[] values =
            (reactions ?? Array.Empty<CharacterAcquiredTraitSpecialReactionDefinition>())
            .ToArray();
        if (values.Any(value => value == null))
            throw new ArgumentException(
                "Acquired-trait reactions cannot contain null entries.",
                nameof(reactions));
        specialReactions = values.OrderBy(value => value.ReactionId,
            StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (ModuleId.Length == 0
            || !string.Equals(moduleId, ModuleId, StringComparison.Ordinal))
            errors.Add("Acquired-trait module requires a stable module ID.");
        if (DisplayName.Length == 0)
            errors.Add($"Acquired-trait module '{ModuleId}' requires a display name.");
        if (Description.Length == 0)
            errors.Add($"Acquired-trait module '{ModuleId}' requires a description.");
        if (cost <= 0)
            errors.Add($"Acquired-trait module '{ModuleId}' requires a positive cost.");
        if (DomainAffinities.Count == 0
            || DomainAffinities.Any(value =>
                !Enum.IsDefined(typeof(CharacterNarrativeDomain), value))
            || DomainAffinities.Distinct().Count() != DomainAffinities.Count)
        {
            errors.Add(
                $"Acquired-trait module '{ModuleId}' has invalid or duplicate domain affinities.");
        }
        if (ConflictGroups.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || ConflictGroups.Select(value => value?.Trim() ?? string.Empty)
                .Distinct(StringComparer.Ordinal).Count() != ConflictGroups.Count)
        {
            errors.Add(
                $"Acquired-trait module '{ModuleId}' has invalid or duplicate conflict groups.");
        }
        if (Effects.Count == 0 || Effects.Any(value => value == null))
        {
            errors.Add(
                $"Acquired-trait module '{ModuleId}' requires non-null gameplay effects.");
        }
        GameplayEffectSourceRef source = new(
            GameplayEffectSourceKind.AcquiredTrait,
            ModuleId);
        foreach (GameplayEffectBinding binding in Effects.Where(value => value != null))
        {
            if (!binding.IsValidFor(source, out string reason))
                errors.Add($"Acquired-trait module '{ModuleId}' {reason}.");
        }
        if (Effects.Where(value => value != null)
            .GroupBy(value => value.bindingId?.Trim() ?? string.Empty, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            errors.Add(
                $"Acquired-trait module '{ModuleId}' contains duplicate effect binding IDs.");
        }
        if (DrawbackBindingIds.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || DrawbackBindingIds.Distinct(StringComparer.Ordinal).Count()
                != DrawbackBindingIds.Count
            || DrawbackBindingIds.Any(id => Effects.All(value => value == null
                || !string.Equals(value.bindingId, id, StringComparison.Ordinal))))
        {
            errors.Add($"Acquired-trait module '{ModuleId}' has invalid drawback binding IDs.");
        }
        if (DrawbackBindingIds.Any(id => Effects.Single(value => value != null
                && string.Equals(value.bindingId, id, StringComparison.Ordinal)).condition == null))
        {
            errors.Add($"Acquired-trait module '{ModuleId}' drawback bindings must have an authored reachable condition.");
        }
        foreach (GameplayEffectBinding binding in Effects.Where(value => value != null
                     && DrawbackBindingIds.Contains(value.bindingId, StringComparer.Ordinal)))
        {
            if (!AcquiredTraitBurdenTargetCatalog.IsHarmful(
                    binding.definition, binding.value, out string reason))
                errors.Add($"Acquired-trait module '{ModuleId}' {reason}.");
        }
        if (SpecialReactions.Any(value => value == null)
            || SpecialReactions.Where(value => value != null)
                .GroupBy(value => value.ReactionId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
        {
            errors.Add(
                $"Acquired-trait module '{ModuleId}' has null or duplicate special reactions.");
        }
        foreach (CharacterAcquiredTraitSpecialReactionDefinition reaction
                 in SpecialReactions.Where(value => value != null))
            errors.AddRange(reaction.ValidateDefinition(ModuleId));
        if (formula != null || !string.IsNullOrWhiteSpace(capabilityId))
        {
            try { RequireFormulaDescriptor(); }
            catch (Exception exception) { errors.Add(exception.Message); }
        }
        return errors;
    }
}
