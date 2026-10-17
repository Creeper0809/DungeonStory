using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public enum EquipmentEvolutionFormulaModifierKind
{
    MultiplierIncrease = 0,
    MultiplierReduction = 1
}

[Serializable]
public sealed class EquipmentEvolutionFormulaRangeDefinition
{
    public string parameterId = string.Empty;
    public long minimumUnits;
    public long maximumUnits;
    public long quantumUnits = 1;
    public int decimalPlaces;
    public int costPerQuantum = 1;

    public NarrativeFormulaQuantizedRange ToRuntime() => new(
        parameterId,
        minimumUnits,
        maximumUnits,
        quantumUnits,
        decimalPlaces,
        costPerQuantum);
}

[Serializable]
public sealed class EquipmentEvolutionFormulaCapabilityDefinition
{
    public string capabilityId = string.Empty;
    public string displayName = string.Empty;
    public string statId = string.Empty;
    public EquipmentEvolutionFormulaModifierKind modifierKind;
    public int baseCost;
    public int triggerFrequencyCostPerUnit;
    public int guaranteedProcCost;
    public int areaCostPerExtraTarget;
    public int multiEffectCostPerExtraEffect;
    public float narrativeAffinity = 1f;
    public List<string> affinityKeys = new();
    public List<string> conflictGroups = new();
    public List<string> forbiddenSynergies = new();
    public List<string> appliedParameterIds = new();
    public string formatterId = string.Empty;
    public string applicatorId = string.Empty;
    public List<EquipmentEvolutionFormulaRangeDefinition> parameterRanges = new();

    public NarrativeFormulaCapabilityDescriptor ToRuntime()
    {
        RequireCanonical(displayName, nameof(displayName));
        RequireCanonical(statId, nameof(statId));
        ValidateApplicator();
        if (!Enum.IsDefined(typeof(EquipmentEvolutionFormulaModifierKind), modifierKind))
            throw new InvalidOperationException($"Unknown equipment modifier kind '{modifierKind}'.");
        return new NarrativeFormulaCapabilityDescriptor(
            capabilityId,
            (parameterRanges ?? new List<EquipmentEvolutionFormulaRangeDefinition>())
                .Select(value => value?.ToRuntime()
                    ?? throw new InvalidOperationException("Equipment formula range cannot be null.")),
            appliedParameterIds,
            baseCost,
            triggerFrequencyCostPerUnit,
            guaranteedProcCost,
            areaCostPerExtraTarget,
            multiEffectCostPerExtraEffect,
            Array.Empty<NarrativeFormulaPairSynergyCost>(),
            narrativeAffinity,
            affinityKeys,
            conflictGroups,
            forbiddenSynergies,
            formatterId,
            applicatorId);
    }

    private void ValidateApplicator()
    {
        (string expectedStatId, EquipmentEvolutionFormulaModifierKind expectedKind) = applicatorId switch
        {
            "equipment:combat-damage:increase" =>
                ("combat.damage", EquipmentEvolutionFormulaModifierKind.MultiplierIncrease),
            "equipment:combat-accuracy:increase" =>
                ("combat.accuracy", EquipmentEvolutionFormulaModifierKind.MultiplierIncrease),
            "equipment:combat-durability:increase" =>
                ("combat.durability", EquipmentEvolutionFormulaModifierKind.MultiplierIncrease),
            "equipment:combat-reload:reduction" =>
                ("combat.reload", EquipmentEvolutionFormulaModifierKind.MultiplierReduction),
            _ => throw new InvalidOperationException(
                $"Unknown equipment formula applicator '{applicatorId ?? string.Empty}'.")
        };
        if (!string.Equals(statId, expectedStatId, StringComparison.Ordinal)
            || modifierKind != expectedKind)
            throw new InvalidOperationException(
                $"Equipment formula applicator '{applicatorId}' does not match its stat and modifier kind.");
    }

    private static string RequireCanonical(string value, string label)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new InvalidOperationException($"Equipment formula {label} must be canonical.");
        return canonical;
    }
}

[Serializable]
public sealed class EquipmentEvolutionFormulaPolicyDefinition
{
    public int formulaVersion = EquipmentEvolutionRules.CurrentFormulaVersion;
    public string catalogSha256 = string.Empty;
    public int baseBudget = 1;
    public int powerScale = 8;
    public double softCapK = 8d;
    public double minimumImportance = 0.25d;
    public double maximumImportance = 4d;
    public List<double> milestoneWeights = new() { 0.5d, 1d, 2d };
    public NarrativeFormulaDrawbackCreditPolicyDefinition drawbackCredit = new();

    public NarrativeFormulaStrengthPolicy ToRuntime() => new(
        formulaVersion,
        baseBudget,
        powerScale,
        softCapK,
        minimumImportance,
        maximumImportance,
        milestoneWeights);

    public NarrativeFormulaDrawbackCreditPolicy RequireDrawbackPolicy() =>
        (drawbackCredit ?? throw new InvalidOperationException(
            "Equipment drawback-credit policy is missing.")).ToRuntime();

    public string RequireCatalogSha256()
    {
        string canonical = catalogSha256?.Trim() ?? string.Empty;
        if (canonical.Length != 64
            || !string.Equals(canonical, catalogSha256, StringComparison.Ordinal)
            || !string.Equals(canonical, canonical.ToLowerInvariant(), StringComparison.Ordinal)
            || canonical.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("Equipment formula catalog SHA-256 is invalid.");
        return canonical;
    }
}

[CreateAssetMenu(
    fileName = "EquipmentEvolutionFormulaCatalog",
    menuName = "Dungeon Story/Evolution/Equipment Formula Catalog")]
public sealed partial class EquipmentEvolutionFormulaCatalogSO : ScriptableObject
{
    public const string ResourcePath = "SO/EquipmentEvolutionFormulaCatalog";
    public const string LegacyV3ResourcePath = "SO/EquipmentEvolutionFormulaCatalogV3";

    public EquipmentEvolutionFormulaPolicyDefinition formulaPolicy = new();
    public List<int> milestoneThresholds = new() { 1, 5, 10 };
    public double ordinaryImportance = 0.5d;
    public double historicalImportance = 2d;
    public List<EquipmentEvolutionFormulaCapabilityDefinition> capabilities = new();

    // Each authored formula generation owns the exact module surface that its
    // SHA-256 commits to. This prevents later catalog content from changing
    // the authority used to validate an already-committed node.
    public List<string> catalogModuleIds = new();

    public NarrativeFormulaStrengthPolicy RequirePolicy()
    {
        NarrativeFormulaStrengthPolicy result = (formulaPolicy
            ?? throw new InvalidOperationException("Equipment formula policy is missing."))
            .ToRuntime();
        string storedSha256 = formulaPolicy.RequireCatalogSha256();
        if (milestoneThresholds == null
            || milestoneThresholds.Count == 0
            || milestoneThresholds.Any(value => value <= 0)
            || !milestoneThresholds.SequenceEqual(milestoneThresholds.OrderBy(value => value))
            || milestoneThresholds.Distinct().Count() != milestoneThresholds.Count)
            throw new InvalidOperationException("Equipment formula milestone thresholds are invalid.");
        if (!NarrativeFormulaGuard.IsFiniteNonNegative(ordinaryImportance)
            || !NarrativeFormulaGuard.IsFiniteNonNegative(historicalImportance)
            || ordinaryImportance < result.MinimumImportance
            || ordinaryImportance > result.MaximumImportance
            || historicalImportance < result.MinimumImportance
            || historicalImportance > result.MaximumImportance)
            throw new InvalidOperationException("Equipment formula importance values are outside policy bounds.");
        RequireCatalog();
        string computedSha256 = ComputeCanonicalSha256();
        if (!string.Equals(storedSha256, computedSha256, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Equipment formula catalog SHA-256 does not match its authored content.");
        return result;
    }

    public string ComputeCanonicalSha256()
    {
        if (formulaPolicy == null)
            throw new InvalidOperationException("Equipment formula policy is missing.");
        IReadOnlyList<string> moduleIds = RequireCatalogModuleIds();
        HashSet<string> includedModuleIds = moduleIds.ToHashSet(StringComparer.Ordinal);
        EvolutionModuleRegistry evolutionModules = new();
        StringBuilder value = new StringBuilder(4096)
            .Append(formulaPolicy.formulaVersion).Append('|')
            .Append(formulaPolicy.baseBudget).Append('|')
            .Append(formulaPolicy.powerScale).Append('|')
            .Append(formulaPolicy.softCapK.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(formulaPolicy.minimumImportance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(formulaPolicy.maximumImportance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(CanonicalDoubleList(formulaPolicy.milestoneWeights)).Append('|')
            .Append(formulaPolicy.drawbackCredit.playerChoiceMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(formulaPolicy.drawbackCredit.automaticMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(formulaPolicy.drawbackCredit.absoluteMaximumCredit).Append('|')
            .Append(formulaPolicy.drawbackCredit.requireNegativeEvidenceForAutomatic).Append('|')
            .Append(string.Join(",", milestoneThresholds ?? new List<int>())).Append('|')
            .Append(ordinaryImportance.ToString("R", CultureInfo.InvariantCulture)).Append('|')
            .Append(historicalImportance.ToString("R", CultureInfo.InvariantCulture));
        foreach (EquipmentEvolutionFormulaCapabilityDefinition capability in
                 (capabilities ?? new List<EquipmentEvolutionFormulaCapabilityDefinition>())
                 .Where(item => item != null)
                 .OrderBy(item => item.capabilityId, StringComparer.Ordinal))
        {
            value.Append('\n').Append(capability.capabilityId).Append('|')
                .Append(capability.displayName).Append('|').Append(capability.statId).Append('|')
                .Append((int)capability.modifierKind).Append('|').Append(capability.baseCost).Append('|')
                .Append(capability.triggerFrequencyCostPerUnit).Append('|')
                .Append(capability.guaranteedProcCost).Append('|')
                .Append(capability.areaCostPerExtraTarget).Append('|')
                .Append(capability.multiEffectCostPerExtraEffect).Append('|')
                .Append(capability.narrativeAffinity.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(capability.formatterId).Append('|').Append(capability.applicatorId).Append('|')
                .Append(CanonicalStringList(capability.affinityKeys)).Append('|')
                .Append(CanonicalStringList(capability.conflictGroups)).Append('|')
                .Append(CanonicalStringList(capability.forbiddenSynergies)).Append('|')
                .Append(CanonicalStringList(capability.appliedParameterIds));
            foreach (EquipmentEvolutionFormulaRangeDefinition range in
                     (capability.parameterRanges ?? new List<EquipmentEvolutionFormulaRangeDefinition>())
                     .Where(item => item != null)
                     .OrderBy(item => item.parameterId, StringComparer.Ordinal))
                value.Append('|').Append(range.parameterId).Append(':').Append(range.minimumUnits)
                    .Append(':').Append(range.maximumUnits).Append(':').Append(range.quantumUnits)
                    .Append(':').Append(range.decimalPlaces).Append(':').Append(range.costPerQuantum);
        }
        foreach (EvolutionModuleDefinition module in evolutionModules.All
                     .Where(item => includedModuleIds.Contains(item.ModuleId))
                     .OrderBy(item => item.ModuleId, StringComparer.Ordinal))
        {
            value.Append("\nmodule|").Append(module.ModuleId).Append('|')
                .Append((int)module.BurdenKind).Append('|')
                .Append(module.RiskWeight).Append('|')
                .Append(module.MaximumDrawbackSeverity).Append('|')
                .Append(CanonicalModifiers(module.Benefits)).Append('|')
                .Append(CanonicalModifiers(module.Burdens)).Append('|')
                .Append(CanonicalStringList(module.NegativeEvidenceMarkers)).Append('|')
                .Append(CanonicalStringList(module.ForbiddenSynergyModuleIds
                    .Where(includedModuleIds.Contains)));
        }
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value.ToString()))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string CanonicalDoubleList(IEnumerable<double> values) => string.Join(
        ",",
        (values ?? Array.Empty<double>()).Select(value =>
            value.ToString("R", CultureInfo.InvariantCulture)));

    private static string CanonicalStringList(IEnumerable<string> values) => string.Join(
        ",",
        (values ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal));

    private static string CanonicalModifiers(
        IEnumerable<EvolutionEffectModifier> values) => string.Join(",",
        (values ?? Array.Empty<EvolutionEffectModifier>())
        .OrderBy(value => value.statId, StringComparer.Ordinal)
        .Select(value => value.statId + ":"
            + value.additive.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.multiplier.ToString("R", CultureInfo.InvariantCulture)));

    public IReadOnlyList<NarrativeFormulaCapabilityDescriptor> RequireCatalog()
    {
        EquipmentEvolutionFormulaCapabilityDefinition[] definitions =
            (capabilities ?? new List<EquipmentEvolutionFormulaCapabilityDefinition>())
            .ToArray();
        if (definitions.Length == 0
            || definitions.Any(value => value == null)
            || definitions.Select(value => value.capabilityId)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new InvalidOperationException("Equipment formula capabilities must be non-empty and distinct.");
        NarrativeFormulaCapabilityDescriptor[] descriptors = definitions
            .Select(value => value.ToRuntime())
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        foreach (NarrativeFormulaCapabilityDescriptor descriptor in descriptors)
        {
            EquipmentEvolutionFormulaCapabilityDefinition definition = RequireCapability(descriptor.CapabilityId);
            if (!string.Equals(descriptor.FormatterId, definition.formatterId, StringComparison.Ordinal)
                || !string.Equals(descriptor.ApplicatorId, definition.applicatorId, StringComparison.Ordinal))
                throw new InvalidOperationException($"Equipment capability '{descriptor.CapabilityId}' formatter/applicator is invalid.");
        }
        _ = RequireCatalogModuleIds();
        return descriptors;
    }

    public IReadOnlyList<string> RequireCatalogModuleIds()
    {
        string[] ids = (catalogModuleIds ?? new List<string>())
            .Select(value => value ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0
            || ids.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || ids.Any(value => !value.StartsWith("equipment:", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                "Equipment formula catalog module IDs must be distinct canonical equipment IDs.");

        EvolutionModuleRegistry evolutionModules = new();
        foreach (string moduleId in ids)
        {
            if (!evolutionModules.TryGet(moduleId, out _))
                throw new InvalidOperationException(
                    $"Equipment formula catalog module '{moduleId}' is not registered.");
        }
        if ((capabilities ?? new List<EquipmentEvolutionFormulaCapabilityDefinition>())
            .Any(value => value == null || !ids.Contains(value.capabilityId,
                StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Every equipment formula capability must be included in its catalog module authority.");
        return Array.AsReadOnly(ids);
    }

    public EquipmentEvolutionFormulaCapabilityDefinition RequireCapability(string capabilityId)
    {
        string canonical = capabilityId?.Trim() ?? string.Empty;
        EquipmentEvolutionFormulaCapabilityDefinition found = capabilities?
            .SingleOrDefault(value => value != null
                && string.Equals(value.capabilityId, canonical, StringComparison.Ordinal));
        return found ?? throw new KeyNotFoundException(
            $"Unknown equipment formula capability '{capabilityId ?? string.Empty}'.");
    }

    public static EquipmentEvolutionFormulaCatalogSO LoadRequired()
    {
        return LoadAtPath(ResourcePath, EquipmentEvolutionRules.CurrentFormulaVersion);
    }

    public static EquipmentEvolutionFormulaCatalogSO LoadForPersistedNode(int formulaVersion)
    {
        if (formulaVersion == EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion)
            return LoadAtPath(
                LegacyV3ResourcePath,
                EquipmentEvolutionRules.DrawbackModuleSelectionFormulaVersion);
        if (formulaVersion >= EquipmentEvolutionRules.CurrentFormulaVersion)
            return LoadAtPath(ResourcePath, formulaVersion);

        // V2 did not store a catalog hash. Its existing descriptors are a
        // strict subset of the current catalog, so retain the pre-V3 route.
        return LoadRequired();
    }

    private static EquipmentEvolutionFormulaCatalogSO LoadAtPath(
        string resourcePath,
        int expectedFormulaVersion)
    {
        EquipmentEvolutionFormulaCatalogSO catalog = Resources.Load<EquipmentEvolutionFormulaCatalogSO>(resourcePath);
        if (catalog == null)
            throw new InvalidOperationException(
                $"Required authored equipment formula catalog Resources/{resourcePath}.asset is missing.");
        catalog.RequirePolicy();
        if (catalog.formulaPolicy.formulaVersion != expectedFormulaVersion)
            throw new InvalidOperationException(
                $"Equipment formula catalog Resources/{resourcePath}.asset has version "
                + $"{catalog.formulaPolicy.formulaVersion}, expected {expectedFormulaVersion}.");
        return catalog;
    }
}
