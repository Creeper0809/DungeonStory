using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum AnatomyFunction
{
    None = 0,
    Core = 1 << 0,
    Consciousness = 1 << 1,
    Sight = 1 << 2,
    Breathing = 1 << 3,
    Digestion = 1 << 4,
    Filtration = 1 << 5,
    Manipulation = 1 << 6,
    Mobility = 1 << 7
}

public enum AnatomyNodeKind
{
    BodyPart = 0,
    Organ = 1,
    SensoryOrgan = 2,
    Limb = 3,
    Core = 4
}

public enum SurgicalPartKind
{
    NaturalOrgan = 0,
    Prosthetic = 1,
    Implant = 2,
    ArcaneGraft = 3
}

public enum AnatomyActionAxisId
{
    Awareness = 0,
    Handling = 1,
    Locomotion = 2,
    Sustain = 3,
    Recovery = 4
}

public enum AnatomyActivityId
{
    Movement = 0,
    Accuracy = 1,
    Evasion = 2,
    Work = 3,
    Carry = 4,
    MeleePower = 5,
    Treatment = 6,
    Recovery = 7,
    Overclock = 8
}

public enum PartRecoveryPolicy
{
    Natural = 0,
    AssistedRegeneration = 1,
    MaintenanceOnly = 2,
    ReplacementOnly = 3
}

public enum AnatomyConditionKind
{
    FluidLoss = 0,
    Contamination = 1,
    Overstrain = 2,
    Fracture = 3,
    PartFailure = 4,
    CompatibilityFailure = 5,
    TreatmentRequired = 6
}

[Serializable]
public sealed class AnatomyNodeAxisContribution
{
    [SerializeField] private AnatomyActionAxisId axis;
    [SerializeField, Min(0f)] private float weight;

    public AnatomyNodeAxisContribution()
    {
    }

    public AnatomyNodeAxisContribution(AnatomyActionAxisId axis, float weight)
    {
        this.axis = axis;
        this.weight = Mathf.Max(0f, weight);
    }

    public AnatomyActionAxisId Axis => axis;
    public float Weight => Mathf.Max(0f, weight);
}

[Serializable]
public sealed class AnatomyActivityProfile
{
    [SerializeField] private AnatomyActivityId activity;
    [SerializeField] private List<AnatomyNodeAxisContribution> axisWeights = new();
    [SerializeField, Min(0.1f)] private float maximumFactor = 1f;

    public AnatomyActivityProfile()
    {
    }

    public AnatomyActivityProfile(
        AnatomyActivityId activity,
        float maximumFactor,
        params AnatomyNodeAxisContribution[] weights)
    {
        this.activity = activity;
        this.maximumFactor = Mathf.Max(0.1f, maximumFactor);
        axisWeights = (weights ?? Array.Empty<AnatomyNodeAxisContribution>())
            .Where(item => item != null && item.Weight > 0f)
            .ToList();
    }

    public AnatomyActivityId Activity => activity;
    public IReadOnlyList<AnatomyNodeAxisContribution> AxisWeights => axisWeights;
    public float MaximumFactor => Mathf.Max(0.1f, maximumFactor);
}

public readonly struct AnatomyActionAxisSnapshot
{
    public AnatomyActionAxisSnapshot(
        float awareness,
        float handling,
        float locomotion,
        float sustain,
        float recovery)
    {
        Awareness = Mathf.Max(0f, awareness);
        Handling = Mathf.Max(0f, handling);
        Locomotion = Mathf.Max(0f, locomotion);
        Sustain = Mathf.Max(0f, sustain);
        Recovery = Mathf.Max(0f, recovery);
    }

    public float Awareness { get; }
    public float Handling { get; }
    public float Locomotion { get; }
    public float Sustain { get; }
    public float Recovery { get; }

    public float Get(AnatomyActionAxisId axis)
    {
        return axis switch
        {
            AnatomyActionAxisId.Awareness => Awareness,
            AnatomyActionAxisId.Handling => Handling,
            AnatomyActionAxisId.Locomotion => Locomotion,
            AnatomyActionAxisId.Sustain => Sustain,
            AnatomyActionAxisId.Recovery => Recovery,
            _ => 1f
        };
    }
}

public readonly struct AnatomyActivityFactorSnapshot
{
    public AnatomyActivityFactorSnapshot(
        AnatomyActivityId activity,
        float rawFactor,
        float appliedFactor,
        float cap)
    {
        Activity = activity;
        RawFactor = Mathf.Max(0f, rawFactor);
        AppliedFactor = Mathf.Max(0f, appliedFactor);
        Cap = Mathf.Max(0.1f, cap);
    }

    public AnatomyActivityId Activity { get; }
    public float RawFactor { get; }
    public float AppliedFactor { get; }
    public float Cap { get; }
    public bool IsCapped => RawFactor > AppliedFactor + 0.0001f;
}

[Serializable]
public sealed class AnatomyNodeDefinition
{
    [SerializeField] private string nodeId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private string parentNodeId = string.Empty;
    [SerializeField] private AnatomyNodeKind kind;
    [SerializeField] private AnatomyFunction functions;
    [SerializeField, Min(1f)] private float maxHealth = 20f;
    [SerializeField, Range(0f, 1f)] private float capacityWeight = 1f;
    [SerializeField] private bool vital;
    [SerializeField] private bool removable = true;
    [SerializeField] private string pairedGroupId = string.Empty;
    [SerializeField] private CombatBodyPart legacyBodyPart;
    [SerializeField] private bool mapsToLegacyBodyPart;
    [SerializeField] private List<AnatomyNodeAxisContribution> axisContributions = new();

    public string NodeId => nodeId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? NodeId
        : displayName.Trim();
    public string ParentNodeId => parentNodeId?.Trim() ?? string.Empty;
    public AnatomyNodeKind Kind => kind;
    public AnatomyFunction Functions => functions;
    public float MaxHealth => Mathf.Max(1f, maxHealth);
    public float CapacityWeight => Mathf.Clamp01(capacityWeight);
    public bool Vital => vital;
    public bool Removable => removable;
    public string PairedGroupId => pairedGroupId?.Trim() ?? string.Empty;
    public bool MapsToLegacyBodyPart => mapsToLegacyBodyPart;
    public CombatBodyPart LegacyBodyPart => legacyBodyPart;
    public IReadOnlyList<AnatomyNodeAxisContribution> AxisContributions =>
        axisContributions;

    public AnatomyNodeDefinition()
    {
    }

    public AnatomyNodeDefinition(
        string nodeId,
        string displayName,
        string parentNodeId,
        AnatomyNodeKind kind,
        AnatomyFunction functions,
        float maxHealth,
        float capacityWeight,
        bool vital,
        bool removable,
        string pairedGroupId = "",
        CombatBodyPart legacyBodyPart = default,
        bool mapsToLegacyBodyPart = false,
        IEnumerable<AnatomyNodeAxisContribution> contributions = null)
    {
        this.nodeId = nodeId?.Trim() ?? string.Empty;
        this.displayName = displayName?.Trim() ?? string.Empty;
        this.parentNodeId = parentNodeId?.Trim() ?? string.Empty;
        this.kind = kind;
        this.functions = functions;
        this.maxHealth = Mathf.Max(1f, maxHealth);
        this.capacityWeight = Mathf.Clamp01(capacityWeight);
        this.vital = vital;
        this.removable = removable;
        this.pairedGroupId = pairedGroupId?.Trim() ?? string.Empty;
        this.legacyBodyPart = legacyBodyPart;
        this.mapsToLegacyBodyPart = mapsToLegacyBodyPart;
        axisContributions = (contributions ?? Array.Empty<AnatomyNodeAxisContribution>())
            .Where(item => item != null && item.Weight > 0f)
            .ToList();
    }

    public float GetAxisContribution(AnatomyActionAxisId axis)
    {
        float explicitWeight = axisContributions?
            .Where(item => item != null && item.Axis == axis)
            .Sum(item => item.Weight) ?? 0f;
        if (explicitWeight > 0f)
        {
            return explicitWeight;
        }

        AnatomyFunction mappedFunctions = axis switch
        {
            AnatomyActionAxisId.Awareness =>
                AnatomyFunction.Consciousness | AnatomyFunction.Sight,
            AnatomyActionAxisId.Handling => AnatomyFunction.Manipulation,
            AnatomyActionAxisId.Locomotion => AnatomyFunction.Mobility,
            AnatomyActionAxisId.Sustain =>
                AnatomyFunction.Core | AnatomyFunction.Breathing,
            AnatomyActionAxisId.Recovery =>
                AnatomyFunction.Digestion | AnatomyFunction.Filtration,
            _ => AnatomyFunction.None
        };
        return (functions & mappedFunctions) != 0 ? CapacityWeight : 0f;
    }
}

public sealed class AnatomyProfileDefinition
{
    private readonly IReadOnlyDictionary<string, AnatomyNodeDefinition> byId;

    public AnatomyProfileDefinition(AnatomyProfileSO source)
        : this(
            source?.ProfileId,
            source?.DisplayName,
            source?.AnatomyFamily,
            source?.SpeciesIds,
            source?.Nodes)
    {
    }

    public AnatomyProfileDefinition(
        string profileId,
        string displayName,
        string anatomyFamily,
        IEnumerable<string> speciesIds,
        IEnumerable<AnatomyNodeDefinition> nodes)
    {
        ProfileId = profileId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? ProfileId
            : displayName.Trim();
        AnatomyFamily = string.IsNullOrWhiteSpace(anatomyFamily)
            ? ProfileId
            : anatomyFamily.Trim();
        SpeciesIds = (speciesIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Nodes = (nodes ?? Array.Empty<AnatomyNodeDefinition>())
            .Where(node => node != null && !string.IsNullOrWhiteSpace(node.NodeId))
            .ToArray();
        byId = Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
    }

    public string ProfileId { get; }
    public string DisplayName { get; }
    public string AnatomyFamily { get; }
    public IReadOnlyList<string> SpeciesIds { get; }
    public IReadOnlyList<AnatomyNodeDefinition> Nodes { get; }

    public bool TryGetNode(string nodeId, out AnatomyNodeDefinition node)
    {
        return byId.TryGetValue(nodeId?.Trim() ?? string.Empty, out node);
    }
}

[Serializable]
public sealed class AnatomyNodeHealthState
{
    public string nodeId = string.Empty;
    public float maxHealth = 20f;
    public float currentHealth = 20f;
    public float bleedingPerSecond;
    public float infection;
    public bool missing;
    public string installedPartId = string.Empty;
    public SurgicalPartKind installedPartKind = SurgicalPartKind.NaturalOrgan;
    public float installedPartEfficiency = 1f;
    public float rejectionBurden;
    public float mutationBurden;
    public float moduleBonus;
    public PartRecoveryPolicy recoveryPolicy = PartRecoveryPolicy.Natural;

    public float HealthRatio => missing
        ? 0f
        : Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
    public float EffectiveEfficiency => missing
        ? 0f
        : Mathf.Clamp01(HealthRatio * Mathf.Max(0f, installedPartEfficiency));
    public float ConditionFactor => missing
        ? 0f
        : HealthRatio
            * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(infection / 100f))
            * Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(rejectionBurden / 100f));
    public float FunctionalEfficiency => missing
        ? 0f
        : Mathf.Max(0f,
            ConditionFactor * Mathf.Max(0f, installedPartEfficiency)
            + moduleBonus);
}

public readonly struct AnatomyHealthSnapshot
{
    public AnatomyHealthSnapshot(
        string profileId,
        IReadOnlyList<AnatomyNodeHealthState> nodes,
        float consciousness,
        float sight,
        float breathing,
        float digestion,
        float filtration,
        float manipulation,
        float mobility)
    {
        ProfileId = profileId ?? string.Empty;
        Nodes = nodes ?? Array.Empty<AnatomyNodeHealthState>();
        Consciousness = Mathf.Clamp01(consciousness);
        Sight = Mathf.Clamp01(sight);
        Breathing = Mathf.Clamp01(breathing);
        Digestion = Mathf.Clamp01(digestion);
        Filtration = Mathf.Clamp01(filtration);
        Manipulation = Mathf.Clamp01(manipulation);
        Mobility = Mathf.Clamp01(mobility);
    }

    public string ProfileId { get; }
    public IReadOnlyList<AnatomyNodeHealthState> Nodes { get; }
    public float Consciousness { get; }
    public float Sight { get; }
    public float Breathing { get; }
    public float Digestion { get; }
    public float Filtration { get; }
    public float Manipulation { get; }
    public float Mobility { get; }
}

public interface IAnatomyProfileCatalog
{
    IReadOnlyList<AnatomyProfileDefinition> Profiles { get; }
    AnatomyProfileDefinition GetDefaultHumanoid();
    AnatomyProfileDefinition GetForSpecies(string speciesId);
    bool TryGet(string profileId, out AnatomyProfileDefinition profile);
    IReadOnlyList<string> Validate();
}

[Serializable]
public sealed class WildlifeAnatomyState
{
    public string wildlifeId = string.Empty;
    public string profileId = string.Empty;
    public List<AnatomyNodeHealthState> nodes = new();
}
