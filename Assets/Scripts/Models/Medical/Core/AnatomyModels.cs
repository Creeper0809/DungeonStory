using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum AnatomyFunction
{
    None = 0,
    PowerCirculation = 1 << 0,
    MentalMaintenance = 1 << 1,
    VisualDiscernment = 1 << 2,
    RespiratoryExchange = 1 << 3,
    IntakeProcessing = 1 << 4,
    PurificationProcessing = 1 << 5,
    PrecisionManipulation = 1 << 6,
    PhysicalMobility = 1 << 7,
    AuditorySensing = 1 << 8,
    VitalityResponse = 1 << 9,
    PhysicalPower = 1 << 10,
    Communication = 1 << 11,
    ArcaneConduction = 1 << 12,
    ImmuneDefense = 1 << 13,

    // Stable authored anatomy vocabulary retained for existing profiles.
    // These are aliases of the 14 functional-capacity producers, not a
    // second performance or action-axis system.
    Core = PowerCirculation,
    Consciousness = MentalMaintenance,
    Sight = VisualDiscernment,
    Breathing = RespiratoryExchange,
    Digestion = IntakeProcessing,
    Filtration = PurificationProcessing,
    Manipulation = PrecisionManipulation,
    Mobility = PhysicalMobility
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
    public AnatomyFunction ExpandedFunctions
    {
        get
        {
            AnatomyFunction expanded = functions;
            if ((functions & AnatomyFunction.MentalMaintenance) != 0)
            {
                expanded |= AnatomyFunction.AuditorySensing
                    | AnatomyFunction.Communication;
            }
            if ((functions & AnatomyFunction.PowerCirculation) != 0)
            {
                expanded |= AnatomyFunction.VitalityResponse
                    | AnatomyFunction.ArcaneConduction;
            }
            if ((functions & AnatomyFunction.PurificationProcessing) != 0)
            {
                expanded |= AnatomyFunction.VitalityResponse;
            }
            return expanded;
        }
    }
    public float MaxHealth => Mathf.Max(1f, maxHealth);
    public float CapacityWeight => Mathf.Clamp01(capacityWeight);
    public bool Vital => vital;
    public bool Removable => removable;
    public string PairedGroupId => pairedGroupId?.Trim() ?? string.Empty;
    public bool MapsToLegacyBodyPart => mapsToLegacyBodyPart;
    public CombatBodyPart LegacyBodyPart => legacyBodyPart;

#if UNITY_EDITOR
    public void AddFunctions(AnatomyFunction addedFunctions)
    {
        functions |= addedFunctions;
    }
#endif

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
            source?.Nodes,
            source?.NotApplicableCapacities)
    {
    }

    public AnatomyProfileDefinition(
        string profileId,
        string displayName,
        string anatomyFamily,
        IEnumerable<string> speciesIds,
        IEnumerable<AnatomyNodeDefinition> nodes,
        IEnumerable<AnatomyFunctionalCapacityNotApplicable> notApplicable = null)
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
        NotApplicableCapacities = (notApplicable
                ?? Array.Empty<AnatomyFunctionalCapacityNotApplicable>())
            .Where(value => value != null)
            .ToDictionary(value => value.CapacityId, value => value.Reason);
    }

    public string ProfileId { get; }
    public string DisplayName { get; }
    public string AnatomyFamily { get; }
    public IReadOnlyList<string> SpeciesIds { get; }
    public IReadOnlyList<AnatomyNodeDefinition> Nodes { get; }
    public IReadOnlyDictionary<CharacterFunctionalCapacityId, string>
        NotApplicableCapacities { get; }

    public bool TryGetNode(string nodeId, out AnatomyNodeDefinition node)
    {
        return byId.TryGetValue(nodeId?.Trim() ?? string.Empty, out node);
    }

    public bool TryGetNotApplicableReason(
        CharacterFunctionalCapacityId capacityId,
        out string reason) => NotApplicableCapacities.TryGetValue(
            capacityId,
            out reason)
        && !string.IsNullOrWhiteSpace(reason);
}

public static class CharacterAnatomyStateBounds
{
    public const float MaximumInstalledPartEfficiency = 1.75f;
    public const float MinimumModuleBonus = -100f;
    public const float MaximumModuleBonus = 100f;
    public const float MaximumFunctionalEfficiency =
        MaximumInstalledPartEfficiency + MaximumModuleBonus;
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
        float mentalMaintenance,
        float visualDiscernment,
        float auditorySensing,
        float respiratoryExchange,
        float powerCirculation,
        float intakeProcessing,
        float purificationProcessing,
        float vitalityResponse,
        float physicalPower,
        float precisionManipulation,
        float physicalMobility,
        float communication,
        float arcaneConduction,
        float immuneDefense)
    {
        ProfileId = profileId ?? string.Empty;
        Nodes = nodes ?? Array.Empty<AnatomyNodeHealthState>();
        MentalMaintenance = RequireCapacity(mentalMaintenance, nameof(mentalMaintenance));
        VisualDiscernment = RequireCapacity(visualDiscernment, nameof(visualDiscernment));
        AuditorySensing = RequireCapacity(auditorySensing, nameof(auditorySensing));
        RespiratoryExchange = RequireCapacity(respiratoryExchange, nameof(respiratoryExchange));
        PowerCirculation = RequireCapacity(powerCirculation, nameof(powerCirculation));
        IntakeProcessing = RequireCapacity(intakeProcessing, nameof(intakeProcessing));
        PurificationProcessing = RequireCapacity(purificationProcessing, nameof(purificationProcessing));
        VitalityResponse = RequireCapacity(vitalityResponse, nameof(vitalityResponse));
        PhysicalPower = RequireCapacity(physicalPower, nameof(physicalPower));
        PrecisionManipulation = RequireCapacity(precisionManipulation, nameof(precisionManipulation));
        PhysicalMobility = RequireCapacity(physicalMobility, nameof(physicalMobility));
        Communication = RequireCapacity(communication, nameof(communication));
        ArcaneConduction = RequireCapacity(arcaneConduction, nameof(arcaneConduction));
        ImmuneDefense = RequireCapacity(immuneDefense, nameof(immuneDefense));
    }

    [Obsolete("Use the 14-capacity constructor.")]
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
        : this(
            profileId,
            nodes,
            consciousness,
            sight,
            consciousness,
            breathing,
            1f,
            digestion,
            filtration,
            filtration,
            mobility,
            manipulation,
            mobility,
            consciousness,
            1f,
            filtration)
    {
    }

    public string ProfileId { get; }
    public IReadOnlyList<AnatomyNodeHealthState> Nodes { get; }
    public float MentalMaintenance { get; }
    public float VisualDiscernment { get; }
    public float AuditorySensing { get; }
    public float RespiratoryExchange { get; }
    public float PowerCirculation { get; }
    public float IntakeProcessing { get; }
    public float PurificationProcessing { get; }
    public float VitalityResponse { get; }
    public float PhysicalPower { get; }
    public float PrecisionManipulation { get; }
    public float PhysicalMobility { get; }
    public float Communication { get; }
    public float ArcaneConduction { get; }
    public float ImmuneDefense { get; }

    [Obsolete("Use MentalMaintenance.")] public float Consciousness => MentalMaintenance;
    [Obsolete("Use VisualDiscernment.")] public float Sight => VisualDiscernment;
    [Obsolete("Use RespiratoryExchange.")] public float Breathing => RespiratoryExchange;
    [Obsolete("Use IntakeProcessing.")] public float Digestion => IntakeProcessing;
    [Obsolete("Use PurificationProcessing.")] public float Filtration => PurificationProcessing;
    [Obsolete("Use PrecisionManipulation.")] public float Manipulation => PrecisionManipulation;
    [Obsolete("Use PhysicalMobility.")] public float Mobility => PhysicalMobility;

    public float Get(CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId), capacityId, null)
    };

    private static float RequireCapacity(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Functional capacity must be finite and non-negative.");
        }
        return value;
    }
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
