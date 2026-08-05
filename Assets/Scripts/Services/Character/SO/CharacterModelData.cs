using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CharacterRuntimeProfile
{
    private const int DefaultStatValue = 5;

    private readonly CharacterStatBlock finalStats;
    private readonly CharacterModelModifiers finalModifiers;
    private readonly List<CharacterTraitSO> traits;
    private readonly IReadOnlyList<CharacterTraitSO> traitsView;

    private CharacterRuntimeProfile(
        CharacterSO source,
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> traits)
    {
        Source = source;
        Species = species;
        this.traits = traits?.Where((trait) => trait != null).ToList() ?? new List<CharacterTraitSO>();
        traitsView = ReadOnlyView.List(this.traits);
        finalStats = BuildFinalStats(source, species, this.traits);
        finalModifiers = BuildFinalModifiers(species, this.traits);
    }

    public CharacterSO Source { get; }
    public CharacterSpeciesSO Species { get; }
    public IReadOnlyList<CharacterTraitSO> Traits => traitsView;
    public string SpeciesTag => !string.IsNullOrWhiteSpace(Species?.speciesTag)
        ? Species.speciesTag
        : Source != null
            ? Source.speciesTag
            : string.Empty;

    public static CharacterRuntimeProfile From(CharacterSO source)
    {
        return new CharacterRuntimeProfile(source, source != null ? source.species : null, source?.traits);
    }

    public static CharacterRuntimeProfile From(
        CharacterSO source,
        IEnumerable<CharacterTraitSO> traits)
    {
        return new CharacterRuntimeProfile(source, source != null ? source.species : null, traits);
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
        float speciesStay = Species != null ? Mathf.Max(0f, Species.stayDurationMultiplier) : 1f;
        return speciesStay * Mathf.Max(0f, finalModifiers.stayDurationMultiplier);
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
        return Species != null ? Mathf.Max(0f, Species.crimeRiskMultiplier) : 1f;
    }

    public CharacterSpeciesIncidentType GetIncidentType()
    {
        return Species != null ? Species.incidentType : CharacterSpeciesIncidentType.None;
    }

    public string GetIncidentId()
    {
        return Species?.IncidentId ?? CharacterSpeciesIncidentIds.None;
    }

    public string GetIncidentName()
    {
        return Species?.IncidentDisplayName ?? string.Empty;
    }

    public string GetIncidentDescription()
    {
        return Species?.IncidentDescription ?? string.Empty;
    }

    public SpeciesNeedProfile GetNeedProfile()
    {
        return Species?.needs ?? new SpeciesNeedProfile();
    }

    public SpeciesEnvironmentProfile GetEnvironmentProfile()
    {
        return Species?.environment ?? new SpeciesEnvironmentProfile();
    }

    public string GetAnatomyProfileId()
    {
        return Species?.anatomyProfileId?.Trim() ?? string.Empty;
    }

    public bool UsesMechanicalMaintenance()
    {
        return Species?.needs?.UsesMaintenanceInsteadOfSurgery ?? false;
    }

    public string GetShortDescription()
    {
        return Species != null ? Species.shortDescription : string.Empty;
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

        return traits.Any((trait) => trait != null && trait.traitName == traitName);
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
}
