using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EvolutionModuleActivationKind
{
    Always,
    RoomConditional
}

public enum EvolutionModuleBurdenKind
{
    None = 0,
    OperatingCost = 1,
    OptionalDrawback = 2,
    InseparableRisk = 3
}

[Flags]
public enum NarrativeBurdenStatScope
{
    None = 0,
    Equipment = 1 << 0,
    Facility = 1 << 1
}

public enum NarrativeBurdenHarmDirection
{
    LowerIsHarmful = 0,
    HigherIsHarmful = 1
}

/// <summary>
/// Closed registry for numeric burdens that have a real gameplay projection.
/// Adding a display-only stat here is forbidden: RuntimeConsumer documents the
/// query/projection which consumes it and tests keep this list fail-closed.
/// </summary>
public sealed class NarrativeBurdenStatDefinition
{
    public NarrativeBurdenStatDefinition(
        string statId,
        string displayName,
        NarrativeBurdenStatScope scopes,
        NarrativeBurdenHarmDirection harmDirection,
        string runtimeConsumer)
    {
        StatId = statId ?? throw new ArgumentNullException(nameof(statId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Scopes = scopes;
        HarmDirection = harmDirection;
        RuntimeConsumer = runtimeConsumer ?? throw new ArgumentNullException(nameof(runtimeConsumer));
    }

    public string StatId { get; }
    public string DisplayName { get; }
    public NarrativeBurdenStatScope Scopes { get; }
    public NarrativeBurdenHarmDirection HarmDirection { get; }
    public string RuntimeConsumer { get; }
}

public static class NarrativeBurdenStatCatalog
{
    private static readonly IReadOnlyDictionary<string, NarrativeBurdenStatDefinition> ById;

    static NarrativeBurdenStatCatalog()
    {
        NarrativeBurdenStatDefinition[] values =
        {
            // Equipment values consumed by CombatEquipmentStatProjector or
            // CombatEquipmentLoadoutRuntime.
            Lower("combat.damage", "피해", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentStatProjector/CombatEquipmentLoadoutRuntime"),
            Lower("combat.accuracy", "명중", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentLoadoutRuntime"),
            Lower("combat.penetration", "관통", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentLoadoutRuntime"),
            Lower("combat.defense", "방어", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentStatProjector/CombatEquipmentLoadoutRuntime"),
            Lower("combat.durability", "내구", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentStatProjector"),
            Higher("combat.reload", "재사용 대기", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentLoadoutRuntime"),
            Lower("combat.value", "장비 가치", NarrativeBurdenStatScope.Equipment,
                "CombatEquipmentStatProjector"),

            // Facility values consumed by FacilityEvolutionModifierQuery.
            Lower("work.output", "작업 산출", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetOutputMultiplier"),
            Lower("service.speed", "서비스 속도", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetWorkSpeedMultiplier"),
            Lower("training.speed", "훈련 운용 속도", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetWorkSpeedMultiplier"),
            Lower("security.speed", "경비 운용 속도", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetWorkSpeedMultiplier"),
            Lower("service.support-speed", "서비스 보조 운용 속도", NarrativeBurdenStatScope.Facility,
                "ServiceSessionRuntime.GetHubSnapshot/CreateContract"),
            Lower("research.output", "연구 산출", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetOutputMultiplier"),
            Lower("survival.output", "생존 지원", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetOutputMultiplier"),
            Lower("defense.output", "방어 성능", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetOutputMultiplier"),
            Lower("entertainment.output", "흥행 성능", NarrativeBurdenStatScope.Facility,
                "FacilityEvolutionModifierQuery.GetOutputMultiplier")
        };
        ById = values.ToDictionary(value => value.StatId, StringComparer.Ordinal);
        All = Array.AsReadOnly(values.OrderBy(value => value.StatId, StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<NarrativeBurdenStatDefinition> All { get; }

    public static bool TryGet(string statId, out NarrativeBurdenStatDefinition definition) =>
        ById.TryGetValue(statId?.Trim() ?? string.Empty, out definition);

    public static string DisplayName(string statId) =>
        TryGet(statId, out NarrativeBurdenStatDefinition definition)
            ? definition.DisplayName
            : statId?.Trim() ?? string.Empty;

    public static bool IsHarmful(
        string statId,
        NarrativeBurdenStatScope scope,
        float additive,
        float multiplier,
        out string reason)
    {
        if (!TryGet(statId, out NarrativeBurdenStatDefinition definition)
            || (definition.Scopes & scope) == 0)
        {
            reason = $"burden stat '{statId}' has no registered runtime consumer for {scope}";
            return false;
        }
        if (!IsFinite(additive) || !IsFinite(multiplier) || multiplier < 0f
            || (Math.Abs(additive) > 0.000001f && Math.Abs(multiplier - 1f) > 0.000001f))
        {
            reason = $"burden stat '{statId}' requires one finite additive or multiplier change";
            return false;
        }
        float delta = Math.Abs(additive) > 0.000001f ? additive : multiplier - 1f;
        bool harmful = definition.HarmDirection == NarrativeBurdenHarmDirection.HigherIsHarmful
            ? delta > 0.000001f : delta < -0.000001f;
        reason = harmful ? string.Empty : $"burden stat '{statId}' changes in a beneficial or neutral direction";
        return harmful;
    }

    private static NarrativeBurdenStatDefinition Lower(
        string id, string name, NarrativeBurdenStatScope scope, string consumer) =>
        new(id, name, scope, NarrativeBurdenHarmDirection.LowerIsHarmful, consumer);

    private static NarrativeBurdenStatDefinition Higher(
        string id, string name, NarrativeBurdenStatScope scope, string consumer) =>
        new(id, name, scope, NarrativeBurdenHarmDirection.HigherIsHarmful, consumer);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

[Serializable]
public sealed class EvolutionModuleActivationRule
{
    public EvolutionModuleActivationKind kind = EvolutionModuleActivationKind.Always;
    public List<string> requiredRoomTags = new List<string>();
    public List<string> optionalRoomTags = new List<string>();
    public List<string> forbiddenRoomTags = new List<string>();
    [Range(0f, 100f)] public float minimumCleanliness;
    [Range(0f, 100f)] public float minimumBeauty;
    public float minimumTemperature;
    [Range(0f, 100f)] public float minimumSpace;

    public EvolutionModuleActivationRule Clone()
    {
        return new EvolutionModuleActivationRule
        {
            kind = kind,
            requiredRoomTags = Normalize(requiredRoomTags),
            optionalRoomTags = Normalize(optionalRoomTags),
            forbiddenRoomTags = Normalize(forbiddenRoomTags),
            minimumCleanliness = Mathf.Clamp(minimumCleanliness, 0f, 100f),
            minimumBeauty = Mathf.Clamp(minimumBeauty, 0f, 100f),
            minimumTemperature = minimumTemperature,
            minimumSpace = Mathf.Clamp(minimumSpace, 0f, 100f)
        };
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}

[Serializable]
public sealed class EvolutionEffectModifier
{
    public string statId = string.Empty;
    public float additive;
    public float multiplier = 1f;

    public EvolutionEffectModifier Clone()
    {
        return new EvolutionEffectModifier
        {
            statId = statId ?? string.Empty,
            additive = additive,
            multiplier = Mathf.Max(0f, multiplier)
        };
    }
}

public sealed class EvolutionModuleDefinition
{
    public EvolutionModuleDefinition(
        string moduleId,
        string displayName,
        string roleTag,
        IEnumerable<EvolutionEffectModifier> benefits,
        IEnumerable<EvolutionEffectModifier> burdens,
        EvolutionModuleActivationRule activationRule = null,
        int riskWeight = 0,
        EvolutionModuleBurdenKind burdenKind = EvolutionModuleBurdenKind.None,
        int maximumDrawbackSeverity = 0,
        IEnumerable<string> negativeEvidenceMarkers = null,
        IEnumerable<string> forbiddenSynergyModuleIds = null)
    {
        ModuleId = NormalizeRequired(moduleId, nameof(moduleId));
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? ModuleId
            : displayName.Trim();
        RoleTag = roleTag?.Trim() ?? string.Empty;
        Benefits = NormalizeModifiers(benefits);
        Burdens = NormalizeModifiers(burdens);
        ActivationRule = activationRule?.Clone() ??
            new EvolutionModuleActivationRule();
        RiskWeight = Mathf.Max(0, riskWeight);
        if (!Enum.IsDefined(typeof(EvolutionModuleBurdenKind), burdenKind))
            throw new ArgumentOutOfRangeException(nameof(burdenKind));
        BurdenKind = burdenKind;
        MaximumDrawbackSeverity = Mathf.Max(0, maximumDrawbackSeverity);
        NegativeEvidenceMarkers = NormalizeStrings(negativeEvidenceMarkers);
        ForbiddenSynergyModuleIds = NormalizeStrings(forbiddenSynergyModuleIds);
        NarrativeBurdenStatScope scope = ModuleId.StartsWith("equipment:", StringComparison.Ordinal)
            ? NarrativeBurdenStatScope.Equipment
            : ModuleId.StartsWith("facility:", StringComparison.Ordinal)
                ? NarrativeBurdenStatScope.Facility
                : NarrativeBurdenStatScope.None;
        if (scope != NarrativeBurdenStatScope.None)
        {
            foreach (EvolutionEffectModifier burden in Burdens)
            {
                if (!NarrativeBurdenStatCatalog.IsHarmful(
                        burden.statId, scope, burden.additive, burden.multiplier,
                        out string reason))
                    throw new ArgumentException(reason, nameof(burdens));
            }
        }
        if (BurdenKind is EvolutionModuleBurdenKind.OperatingCost
                or EvolutionModuleBurdenKind.InseparableRisk
            && (Benefits.Count == 0 || Burdens.Count == 0))
            throw new ArgumentException(
                "Operating-cost and inseparable-risk modules require both benefits and burdens.",
                nameof(burdens));
        if (BurdenKind == EvolutionModuleBurdenKind.OptionalDrawback
            && (Benefits.Count != 0 || Burdens.Count == 0
                || MaximumDrawbackSeverity < 1
                || NegativeEvidenceMarkers.Count == 0))
            throw new ArgumentException(
                "Optional drawbacks require burdens, evidence markers, severity, and no benefits.",
                nameof(burdens));
    }

    public string ModuleId { get; }
    public string DisplayName { get; }
    public string RoleTag { get; }
    public IReadOnlyList<EvolutionEffectModifier> Benefits { get; }
    public IReadOnlyList<EvolutionEffectModifier> Burdens { get; }
    public EvolutionModuleActivationRule ActivationRule { get; }
    public int RiskWeight { get; }
    public EvolutionModuleBurdenKind BurdenKind { get; }
    public int MaximumDrawbackSeverity { get; }
    public IReadOnlyList<string> NegativeEvidenceMarkers { get; }
    public IReadOnlyList<string> ForbiddenSynergyModuleIds { get; }

    public bool IsPositiveModule => BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback;

    /// <summary>
    /// A positive module cannot be newly generated when one of its own burdens
    /// targets the same gameplay stat as one of its benefits. The definition
    /// remains loadable so already-committed lineage nodes retain their exact
    /// historical projection.
    /// </summary>
    public bool HasInternallySelfCancellingPositiveAxis => IsPositiveModule
        && Benefits.Any(benefit => benefit != null
            && Burdens.Any(burden => burden != null
                && string.Equals(
                    benefit.statId?.Trim(),
                    burden.statId?.Trim(),
                    StringComparison.Ordinal)));

    public bool MatchesNegativeEvidence(params string[] values)
    {
        if (BurdenKind != EvolutionModuleBurdenKind.OptionalDrawback)
            return false;
        return (values ?? Array.Empty<string>()).Any(value =>
        {
            string normalized = value?.Trim() ?? string.Empty;
            return normalized.Length > 0 && NegativeEvidenceMarkers.Any(marker =>
                normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
        });
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Evolution module IDs cannot be blank.", parameterName);
        }

        return normalized;
    }

    private static IReadOnlyList<EvolutionEffectModifier> NormalizeModifiers(
        IEnumerable<EvolutionEffectModifier> values)
    {
        return Array.AsReadOnly((values ?? Array.Empty<EvolutionEffectModifier>())
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.statId))
            .Select(value => value.Clone())
            .ToArray());
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string> values)
    {
        return Array.AsReadOnly((values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }
}

public interface IEvolutionModuleRegistry
{
    IReadOnlyList<EvolutionModuleDefinition> All { get; }
    bool TryGet(string moduleId, out EvolutionModuleDefinition definition);
}

public readonly struct EvolutionRoomConditionSnapshot
{
    public EvolutionRoomConditionSnapshot(
        IEnumerable<string> tags,
        float cleanliness,
        float beauty,
        float temperature,
        float space)
    {
        Tags = new HashSet<string>(
            tags ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        Cleanliness = Mathf.Clamp(cleanliness, 0f, 100f);
        Beauty = Mathf.Clamp(beauty, 0f, 100f);
        Temperature = temperature;
        Space = Mathf.Clamp(space, 0f, 100f);
    }

    public IReadOnlyCollection<string> Tags { get; }
    public float Cleanliness { get; }
    public float Beauty { get; }
    public float Temperature { get; }
    public float Space { get; }
}

public static class EvolutionModuleActivation
{
    public static bool IsBenefitActive(
        EvolutionModuleActivationRule rule,
        EvolutionRoomConditionSnapshot room)
    {
        if (rule == null || rule.kind == EvolutionModuleActivationKind.Always)
        {
            return true;
        }

        HashSet<string> tags = room.Tags as HashSet<string>
            ?? new HashSet<string>(room.Tags ?? Array.Empty<string>(), StringComparer.Ordinal);
        if (rule.requiredRoomTags.Any(tag => !tags.Contains(tag))
            || rule.forbiddenRoomTags.Any(tags.Contains)
            || room.Cleanliness + 0.001f < rule.minimumCleanliness
            || room.Beauty + 0.001f < rule.minimumBeauty
            || room.Temperature + 0.001f < rule.minimumTemperature
            || room.Space + 0.001f < rule.minimumSpace)
        {
            return false;
        }

        return rule.optionalRoomTags.Count == 0
            || rule.optionalRoomTags.Any(tags.Contains);
    }
}
