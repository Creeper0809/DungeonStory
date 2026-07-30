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
        bool mapsToLegacyBodyPart = false)
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

    public float HealthRatio => missing
        ? 0f
        : Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
    public float EffectiveEfficiency => missing
        ? 0f
        : Mathf.Clamp01(HealthRatio * Mathf.Max(0f, installedPartEfficiency));
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

public interface IAnatomyHealthRuntime
{
    AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor);
    AnatomyHealthSnapshot GetAnatomySnapshot(string characterId);
    bool TryDamageNode(
        CharacterActor actor,
        string nodeId,
        float damage,
        float bleeding,
        string reason);
    bool TryHealNode(
        CharacterActor actor,
        string nodeId,
        float health,
        float infectionReduction);
    bool TryRemoveNode(
        CharacterActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out string failureReason);
    bool TryInstallPart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out string failureReason);
    bool TryReplaceNodePart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out AnatomyNodeHealthState replacedNode,
        out string failureReason);
    bool TryAddNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason);
    bool TryReduceNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason);
}

[Serializable]
public sealed class WildlifeAnatomyState
{
    public string wildlifeId = string.Empty;
    public string profileId = string.Empty;
    public List<AnatomyNodeHealthState> nodes = new();
}

public interface IWildlifeAnatomyHealthRuntime
{
    AnatomyHealthSnapshot GetAnatomySnapshot(WildlifeActor actor);
    bool TryHealNode(
        WildlifeActor actor,
        string nodeId,
        float health,
        float infectionReduction);
    bool TryRemoveNode(
        WildlifeActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out string failureReason);
    bool TryInstallPart(
        WildlifeActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out string failureReason);
    bool TryAddNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason);
    bool TryReduceNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason);
    IReadOnlyList<WildlifeAnatomyState> Capture();
    void Restore(
        IEnumerable<WildlifeAnatomyState> states,
        IList<string> warnings);
}
