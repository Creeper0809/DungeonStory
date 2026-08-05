using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterSpeciesId : IEquatable<CharacterSpeciesId>
{
    private readonly string value;

    public CharacterSpeciesId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(CharacterSpeciesId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is CharacterSpeciesId other && Equals(other);
    public override int GetHashCode() =>
        PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterSpeciesId(string value) => new(value);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SpeciesOwnerSelectionPolicy
{
    Selectable = 0,
    NpcOnly = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SpeciesMetabolismKind
{
    Biological = 0,
    Construct = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SpeciesTreatmentKind
{
    BiologicalMedicine = 0,
    MechanicalMaintenance = 1
}

[MovedFrom(true, sourceAssembly: "DungeonStory.Survival")]
public enum MealDietClass
{
    Vegan = 0,
    Vegetarian = 1,
    Mixed = 2,
    Carnivore = 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterSpeciesIncidentType
{
    None,
    SlimeContamination,
    OrcRampage,
    VampireFear
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SpeciesNeedProfile
{
    [Min(0f)] public float hungerRateMultiplier = 1f;
    [Min(0f)] public float thirstRateMultiplier = 1f;
    [Min(0f)] public float sleepRateMultiplier = 1f;
    [Min(0f)] public float hygieneRateMultiplier = 1f;
    [Min(0f)] public float socialNeedMultiplier = 1f;
    [Min(0f)] public float chargeRateMultiplier = 1f;
    [Min(0f)] public float integrityWearMultiplier = 1f;
    public MealDietClass diet = MealDietClass.Mixed;
    public SpeciesMetabolismKind metabolism = SpeciesMetabolismKind.Biological;
    public SpeciesTreatmentKind treatment = SpeciesTreatmentKind.BiologicalMedicine;

    public bool UsesChargeInsteadOfFood =>
        metabolism == SpeciesMetabolismKind.Construct;
    public bool UsesMaintenanceInsteadOfSurgery =>
        treatment == SpeciesTreatmentKind.MechanicalMaintenance;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct SpeciesThermalProfile
{
    public SpeciesThermalProfile(
        float comfortMinimum,
        float comfortMaximum,
        float safeMinimum,
        float safeMaximum,
        float lethalMinimum,
        float lethalMaximum)
    {
        ComfortMinimum = comfortMinimum;
        ComfortMaximum = comfortMaximum;
        SafeMinimum = safeMinimum;
        SafeMaximum = safeMaximum;
        LethalMinimum = lethalMinimum;
        LethalMaximum = lethalMaximum;
    }

    public float ComfortMinimum { get; }
    public float ComfortMaximum { get; }
    public float SafeMinimum { get; }
    public float SafeMaximum { get; }
    public float LethalMinimum { get; }
    public float LethalMaximum { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SpeciesEnvironmentProfile
{
    public float comfortMinimum = 15f;
    public float comfortMaximum = 27f;
    public float safeMinimum;
    public float safeMaximum = 40f;
    public float lethalMinimum = -10f;
    public float lethalMaximum = 48f;
    [Range(0f, 100f)] public float comfortableAirMinimum = 70f;
    [Range(0f, 100f)] public float comfortableLightMinimum = 40f;
    [Range(0f, 100f)] public float comfortableLightMaximum = 100f;
    [Range(0.05f, 2f)] public float airborneExposureMultiplier = 1f;
    [Range(0.05f, 2f)] public float visualStrainMultiplier = 1f;
    [Range(0f, 1f)] public float preferredHumidity = 0.5f;
    [Range(0f, 2f)] public float drynessSensitivity = 1f;

    public SpeciesThermalProfile ToThermalProfile()
    {
        float lethalMin = Mathf.Min(lethalMinimum, lethalMaximum - 4f);
        float lethalMax = Mathf.Max(lethalMaximum, lethalMin + 4f);
        float safeMin = Mathf.Clamp(safeMinimum, lethalMin + 2f, lethalMax - 2f);
        float safeMax = Mathf.Clamp(safeMaximum, safeMin, lethalMax - 2f);
        float comfortMin = Mathf.Clamp(comfortMinimum, safeMin, safeMax);
        float comfortMax = Mathf.Clamp(comfortMaximum, comfortMin, safeMax);
        return new SpeciesThermalProfile(
            comfortMin,
            comfortMax,
            safeMin,
            safeMax,
            lethalMin,
            lethalMax);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SpeciesIncidentDefinition
{
    public string incidentId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public FacilityRole mitigatingRoles;
    public string[] triggerTags = Array.Empty<string>();

    public string StableId => incidentId?.Trim() ?? string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SpeciesPassiveDefinition
{
    public string passiveId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public string[] mechanicTags = Array.Empty<string>();

    public string StableId => passiveId?.Trim() ?? string.Empty;
}

public static class CharacterSpeciesIncidentIds
{
    public const string None = "";
    public const string SlimeContamination = "species-incident:slime-contamination";
    public const string OrcRampage = "species-incident:orc-rampage";
    public const string VampireFear = "species-incident:vampire-fear";
    public const string BeastkinCommotion = "species-incident:beastkin-commotion";
    public const string DemonContractCurse = "species-incident:demon-contract-curse";
    public const string KoboldPartsHoarding = "species-incident:kobold-parts-hoarding";
    public const string MyconidSporeBloom = "species-incident:myconid-spore-bloom";
    public const string HarpyGaleCommotion = "species-incident:harpy-gale-commotion";
    public const string GolemCoreOverload = "species-incident:golem-core-overload";
}

[DrawWithUnity]
public abstract class CharacterSpeciesDefinitionSO : SerializedScriptableObject
{
    public int id;
    public string speciesTag;
    public string displayName;
    public SpeciesOwnerSelectionPolicy ownerSelectionPolicy;
    public string homeFactionId;
    public string anatomyProfileId = "anatomy:humanoid";
    public SpeciesNeedProfile needs = new();
    public SpeciesEnvironmentProfile environment = new();
    public string[] relationTags = Array.Empty<string>();
    public string[] defenseAffinityTags = Array.Empty<string>();
    public string[] strongWorkTypeIds = Array.Empty<string>();
    public string[] weakWorkTypeIds = Array.Empty<string>();
    [TextArea] public string shortDescription;
    [TextArea] public string description;
    public string[] preferredFacilityLabels = Array.Empty<string>();
    public string[] dislikedEnvironmentLabels = Array.Empty<string>();
    [Min(0f)] public float stayDurationMultiplier = 1f;
    [Min(0f)] public float crimeRiskMultiplier = 1f;
    public CharacterSpeciesIncidentType incidentType;
    public SpeciesIncidentDefinition incident = new();
    public string incidentName;
    [TextArea] public string incidentDescription;
    public FacilityRole incidentMitigatingRoles;
    public SpeciesPassiveDefinition combatPassive = new();

    public bool ownerSelectable =>
        ownerSelectionPolicy == SpeciesOwnerSelectionPolicy.Selectable;

    public CharacterSpeciesId DefinitionId => new(speciesTag);
    public string IncidentId => incident?.StableId ?? string.Empty;
    public string IncidentDisplayName =>
        incident?.displayName?.Trim() ?? string.Empty;
    public string IncidentDescription =>
        incident?.description?.Trim() ?? string.Empty;
    public FacilityRole IncidentMitigatingRoles =>
        incident?.mitigatingRoles ?? FacilityRole.None;
}

public static class CharacterSpeciesDefinitionCatalogRequirements
{
    public static IReadOnlyList<T> Normalize<T>(IEnumerable<T> authored)
        where T : CharacterSpeciesDefinitionSO
    {
        T[] values = (authored ?? Array.Empty<T>())
            .Where(value => value != null)
            .ToArray();
        IGrouping<string, T> duplicate = values
            .Where(value => !string.IsNullOrWhiteSpace(value.speciesTag))
            .GroupBy(
                value => value.speciesTag.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate authored character species tag '{duplicate.Key}'.");
        }

        if (values.Length == 0)
        {
            throw new InvalidOperationException(
                "The root content catalog has no authored character species.");
        }

        foreach (T species in values)
        {
            if (!species.DefinitionId.IsValid
                || !string.Equals(
                    species.DefinitionId.Value,
                    species.speciesTag,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(species.displayName)
                || species.needs == null
                || species.environment == null
                || string.IsNullOrWhiteSpace(species.anatomyProfileId)
                || species.incident == null
                || string.IsNullOrWhiteSpace(species.incident.StableId)
                || species.incident.mitigatingRoles == FacilityRole.None)
            {
                throw new InvalidOperationException(
                    $"Character species asset '{species.name}' has incomplete authored content.");
            }
        }

        return values
            .OrderBy(value => value.id)
            .ThenBy(value => value.speciesTag, StringComparer.Ordinal)
            .ToArray();
    }
}

public interface ICharacterSpeciesDefinitionCatalog
{
    IReadOnlyList<CharacterSpeciesDefinitionSO> Definitions { get; }
    bool TryGetDefinition(
        CharacterSpeciesId speciesId,
        out CharacterSpeciesDefinitionSO species);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICharacterSpeciesEnvironmentCatalog
{
    bool TryGetThermalProfile(
        CharacterSpeciesId speciesId,
        out SpeciesThermalProfile profile);
    SpeciesThermalProfile GetRequiredThermalProfile(
        CharacterSpeciesId speciesId);
}
