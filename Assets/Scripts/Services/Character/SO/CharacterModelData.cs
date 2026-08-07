using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CharacterRuntimeProfile
{
    private const int DefaultStatValue = 5;

    private readonly CharacterStatBlock finalStats;
    private readonly CharacterModelModifiers finalModifiers;
    private readonly IReadOnlyList<string> expressedTraitIds;
    private readonly IReadOnlyList<string> latentTraitIds;
    private readonly IReadOnlyList<string> traitDisplayNames;
    private readonly IReadOnlyDictionary<string, int> innateAptitudes;
    private readonly SpeciesNeedProfile needProfile;
    private readonly SpeciesEnvironmentProfile environmentProfile;
    private readonly float speciesStayDurationMultiplier;
    private readonly float speciesCrimeRiskMultiplier;
    private readonly CharacterSpeciesIncidentType incidentType;
    private readonly string incidentId;
    private readonly string incidentName;
    private readonly string incidentDescription;
    private readonly string anatomyProfileId;
    private readonly string shortDescription;

    private CharacterRuntimeProfile(
        CharacterSpawnRequest request,
        CharacterStatBlock finalStats,
        CharacterModelModifiers finalModifiers,
        IEnumerable<CharacterTraitSO> expressedTraits,
        CharacterSpeciesSO species)
    {
        CharacterArchetypeId = request.CharacterArchetypeId;
        PhenotypeSpeciesId = request.PhenotypeSpeciesId;
        VisualVariantId = request.VisualVariantId;
        ReproductiveRole = request.ReproductiveRole;
        expressedTraitIds = request.ExpressedTraitIds
            .Select(value => value.Value)
            .ToArray();
        latentTraitIds = request.LatentTraitIds
            .Select(value => value.Value)
            .ToArray();
        traitDisplayNames = (expressedTraits ?? Array.Empty<CharacterTraitSO>())
            .Select(value => value?.traitName?.Trim() ?? string.Empty)
            .ToArray();
        innateAptitudes = new Dictionary<string, int>(
            request.InnateAptitudes,
            StringComparer.Ordinal);
        this.finalStats = CopyStats(finalStats);
        this.finalModifiers = CopyModifiers(finalModifiers);
        SpeciesTag = species.speciesTag?.Trim() ?? string.Empty;
        needProfile = CopyNeeds(species.needs);
        environmentProfile = CopyEnvironment(species.environment);
        speciesStayDurationMultiplier = Mathf.Max(0f, species.stayDurationMultiplier);
        speciesCrimeRiskMultiplier = Mathf.Max(0f, species.crimeRiskMultiplier);
        incidentType = species.incidentType;
        incidentId = species.IncidentId;
        incidentName = species.IncidentDisplayName;
        incidentDescription = species.IncidentDescription;
        anatomyProfileId = species.anatomyProfileId?.Trim() ?? string.Empty;
        shortDescription = species.shortDescription?.Trim() ?? string.Empty;
    }

    public CharacterArchetypeId CharacterArchetypeId { get; }
    public CharacterSpeciesId PhenotypeSpeciesId { get; }
    public string VisualVariantId { get; }
    public ReproductiveRole ReproductiveRole { get; }
    public IReadOnlyList<string> ExpressedTraitIds => expressedTraitIds;
    public IReadOnlyList<string> LatentTraitIds => latentTraitIds;
    public IReadOnlyList<string> TraitDisplayNames => traitDisplayNames;
    public IReadOnlyDictionary<string, int> InnateAptitudes => innateAptitudes;
    public string SpeciesTag { get; }

    internal static CharacterRuntimeProfile Create(
        CharacterSpawnRequest request,
        CharacterSO archetype,
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> expressedTraits)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (archetype == null) throw new ArgumentNullException(nameof(archetype));
        if (species == null) throw new ArgumentNullException(nameof(species));

        CharacterTraitSO[] traits = (expressedTraits ?? Array.Empty<CharacterTraitSO>())
            .ToArray();
        return new CharacterRuntimeProfile(
            request,
            BuildFinalStats(archetype, species, traits),
            BuildFinalModifiers(species, traits),
            traits,
            species);
    }

    public int GetStat(CharacterStatType type)
    {
        return Mathf.Max(0, finalStats.Get(type));
    }

    public int GetStat(string statId)
    {
        return Mathf.Max(0, finalStats.Get(statId));
    }

    public float GetMoveSpeedMultiplier()
    {
        return ClampStatMultiplier(CharacterStatType.MoveSpeed, 0.08f, 0.5f, 1.8f)
            * finalModifiers.moveSpeedMultiplier;
    }

    public float GetMoveModifierOnly()
    {
        return Mathf.Max(0f, finalModifiers.moveSpeedMultiplier);
    }

    public float GetSpendingMultiplier()
    {
        return ClampStatMultiplier(CharacterStatType.Sales, 0.05f, 0.5f, 2f)
            * finalModifiers.spendingMultiplier;
    }

    public float GetSpendingModifierOnly()
    {
        return Mathf.Max(0f, finalModifiers.spendingMultiplier);
    }

    public float GetConsumptionMultiplier()
    {
        return Mathf.Max(0f, finalModifiers.consumptionMultiplier);
    }

    public float GetStayDurationMultiplier()
    {
        return speciesStayDurationMultiplier
            * Mathf.Max(0f, finalModifiers.stayDurationMultiplier);
    }

    public float GetCrowdSensitivityMultiplier()
    {
        return Mathf.Max(0f, finalModifiers.crowdSensitivityMultiplier);
    }

    public float GetWaitPatienceMultiplier()
    {
        return Mathf.Max(0f, finalModifiers.waitPatienceMultiplier);
    }

    public float GetAccidentChanceMultiplier()
    {
        float enduranceMultiplier = Mathf.Clamp(1f - ((GetStat(CharacterStatType.Endurance) - DefaultStatValue) * 0.03f), 0.5f, 1.5f);
        float toughnessMultiplier = Mathf.Clamp(1f - ((GetStat(CharacterStatType.Toughness) - DefaultStatValue) * 0.02f), 0.6f, 1.4f);
        return Mathf.Max(0f, finalModifiers.accidentChanceMultiplier * enduranceMultiplier * toughnessMultiplier);
    }

    public float GetAccidentModifierOnly()
    {
        return Mathf.Max(0f, finalModifiers.accidentChanceMultiplier);
    }

    public float GetCrimeRiskMultiplier()
    {
        return speciesCrimeRiskMultiplier;
    }

    public CharacterSpeciesIncidentType GetIncidentType()
    {
        return incidentType;
    }

    public string GetIncidentId()
    {
        return incidentId;
    }

    public string GetIncidentName()
    {
        return incidentName;
    }

    public string GetIncidentDescription()
    {
        return incidentDescription;
    }

    public SpeciesNeedProfile GetNeedProfile()
    {
        return CopyNeeds(needProfile);
    }

    public SpeciesEnvironmentProfile GetEnvironmentProfile()
    {
        return CopyEnvironment(environmentProfile);
    }

    public string GetAnatomyProfileId()
    {
        return anatomyProfileId;
    }

    public bool UsesMechanicalMaintenance()
    {
        return needProfile.UsesMaintenanceInsteadOfSurgery;
    }

    public string GetShortDescription()
    {
        return shortDescription;
    }

    public float GetCombatPowerMultiplier()
    {
        return Mathf.Max(0f, finalModifiers.combatPowerMultiplier);
    }

    public float GetWorkModifierOnly(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? CalculateWorkModifierOnly(FacilityWorkTypeMap.GetRequired(definition))
            : 1f;
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? CalculateWorkSpeedMultiplier(definition)
            : 1f;
    }

    public float GetWorkPreferenceScore(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? CalculateWorkPreferenceScore(FacilityWorkTypeMap.GetRequired(definition))
            : 0.5f;
    }

    private float CalculateWorkModifierOnly(FacilityWorkType workTypes)
    {
        float typeMultiplier = 1f;
        if ((workTypes & finalModifiers.PreferredLegacyWorkTypes) != 0)
        {
            typeMultiplier *= 1.25f;
        }

        if ((workTypes & finalModifiers.DislikedLegacyWorkTypes) != 0)
        {
            typeMultiplier *= 0.75f;
        }

        if ((workTypes & FacilityWorkType.Research) != 0)
        {
            typeMultiplier *= finalModifiers.researchSpeedMultiplier;
        }

        return Mathf.Max(0f, finalModifiers.workSpeedMultiplier * typeMultiplier);
    }

    private float CalculateWorkSpeedMultiplier(WorkTypeDefinition definition)
    {
        FacilityWorkType legacyType = FacilityWorkTypeMap.GetRequired(definition);
        float statMultiplier = ClampStatMultiplier(
            GetBestWorkStat(legacyType),
            0.06f,
            0.5f,
            2f);
        return Mathf.Max(0f, statMultiplier * CalculateWorkModifierOnly(legacyType));
    }

    private float CalculateWorkPreferenceScore(FacilityWorkType workTypes)
    {
        if (workTypes == FacilityWorkType.None)
        {
            return 0.5f;
        }

        if ((workTypes & finalModifiers.DislikedLegacyWorkTypes) != 0)
        {
            return 0.1f;
        }

        if ((workTypes & finalModifiers.PreferredLegacyWorkTypes) != 0)
        {
            return 1f;
        }

        return 0.5f;
    }

    public float GetFacilityPreferenceScore(FacilityRole roles)
    {
        if (roles == FacilityRole.None)
        {
            return 0.5f;
        }

        if ((roles & finalModifiers.dislikedFacilityRoles) != 0)
        {
            return 0.1f;
        }

        if ((roles & finalModifiers.preferredFacilityRoles) != 0)
        {
            return 1f;
        }

        return 0.5f;
    }

    public bool HasTrait(string traitName)
    {
        if (string.IsNullOrWhiteSpace(traitName)) return false;

        string normalized = traitName.Trim();
        return expressedTraitIds.Contains(normalized, StringComparer.Ordinal)
            || traitDisplayNames.Contains(normalized, StringComparer.Ordinal);
    }

    private float ClampStatMultiplier(
        CharacterStatType statType,
        float perPoint,
        float min,
        float max)
    {
        return Mathf.Clamp(1f + ((GetStat(statType) - DefaultStatValue) * perPoint), min, max);
    }

    private static CharacterStatType GetBestWorkStat(FacilityWorkType workTypes)
    {
        if ((workTypes & FacilityWorkType.Construct) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Research) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Guard) != 0) return CharacterStatType.Attack;
        if ((workTypes & FacilityWorkType.Clean) != 0) return CharacterStatType.Cleaning;
        if ((workTypes & FacilityWorkType.DrawWater) != 0) return CharacterStatType.Endurance;
        if ((workTypes & FacilityWorkType.Cook) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Treat) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Refuel) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Restock) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Repair) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Operate) != 0) return CharacterStatType.Sales;
        if ((workTypes & FacilityWorkType.Rescue) != 0) return CharacterStatType.Toughness;
        return CharacterStatType.Endurance;
    }

    private static CharacterStatBlock BuildFinalStats(
        CharacterSO source,
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> traits)
    {
        CharacterStatBlock result = source != null && source.baseStats != null && source.baseStats.HasAnyValue
            ? CopyStats(source.baseStats)
            : CharacterStatBlock.CreateDefault(DefaultStatValue);

        result.Add(species?.statBonus);
        if (traits != null)
        {
            foreach (CharacterTraitSO trait in traits)
            {
                result.Add(trait?.statBonus);
            }
        }

        return result;
    }

    private static CharacterModelModifiers BuildFinalModifiers(
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> traits)
    {
        CharacterModelModifiers result = new CharacterModelModifiers();
        result.Multiply(species?.modifiers);
        if (traits != null)
        {
            foreach (CharacterTraitSO trait in traits)
            {
                result.Multiply(trait?.modifiers);
            }
        }

        return result;
    }

    private static CharacterStatBlock CopyStats(CharacterStatBlock source)
    {
        CharacterStatBlock result = new CharacterStatBlock();
        result.Add(source);
        return result;
    }

    private static CharacterModelModifiers CopyModifiers(
        CharacterModelModifiers source)
    {
        CharacterModelModifiers result = new CharacterModelModifiers();
        result.Multiply(source);
        return result;
    }

    private static SpeciesNeedProfile CopyNeeds(SpeciesNeedProfile source)
    {
        SpeciesNeedProfile required = source ?? new SpeciesNeedProfile();
        return new SpeciesNeedProfile
        {
            hungerRateMultiplier = required.hungerRateMultiplier,
            thirstRateMultiplier = required.thirstRateMultiplier,
            sleepRateMultiplier = required.sleepRateMultiplier,
            hygieneRateMultiplier = required.hygieneRateMultiplier,
            socialNeedMultiplier = required.socialNeedMultiplier,
            chargeRateMultiplier = required.chargeRateMultiplier,
            integrityWearMultiplier = required.integrityWearMultiplier,
            diet = required.diet,
            metabolism = required.metabolism,
            treatment = required.treatment
        };
    }

    private static SpeciesEnvironmentProfile CopyEnvironment(
        SpeciesEnvironmentProfile source)
    {
        SpeciesEnvironmentProfile required = source
            ?? new SpeciesEnvironmentProfile();
        return new SpeciesEnvironmentProfile
        {
            comfortMinimum = required.comfortMinimum,
            comfortMaximum = required.comfortMaximum,
            safeMinimum = required.safeMinimum,
            safeMaximum = required.safeMaximum,
            lethalMinimum = required.lethalMinimum,
            lethalMaximum = required.lethalMaximum,
            comfortableAirMinimum = required.comfortableAirMinimum,
            comfortableLightMinimum = required.comfortableLightMinimum,
            comfortableLightMaximum = required.comfortableLightMaximum,
            airborneExposureMultiplier = required.airborneExposureMultiplier,
            visualStrainMultiplier = required.visualStrainMultiplier,
            preferredHumidity = required.preferredHumidity,
            drynessSensitivity = required.drynessSensitivity
        };
    }
}
