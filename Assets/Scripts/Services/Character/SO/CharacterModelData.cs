using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CharacterRuntimeProfile
{
    private readonly CharacterModelModifiers finalModifiers;
    private readonly IReadOnlyList<string> expressedTraitIds;
    private readonly IReadOnlyList<string> latentTraitIds;
    private readonly IReadOnlyList<string> traitDisplayNames;
    private readonly IReadOnlyDictionary<string, float> behaviorUtilityDeltas;
    private readonly IReadOnlyDictionary<string, float> eventWeightMultipliers;
    private readonly IReadOnlyDictionary<string, int> innateAptitudes;
    private readonly float earnedWorkExperienceMultiplier;
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
        behaviorUtilityDeltas = BuildBehaviorUtilityDeltas(expressedTraits);
        eventWeightMultipliers = BuildEventWeightMultipliers(expressedTraits);
        CharacterTraitSO[] authoredTraits = (expressedTraits
                ?? Array.Empty<CharacterTraitSO>())
            .Where(value => value != null)
            .ToArray();
        float legacyExperience = authoredTraits
            .Where(value => !HasEffectTarget(
                value,
                GameplayEffectTargetIds.EarnedWorkExperience))
            .Aggregate(
                1f,
                (current, value) => current
                    * Mathf.Max(.1f, value.earnedWorkExperienceMultiplier));
        earnedWorkExperienceMultiplier = Mathf.Clamp(
            legacyExperience,
            .1f,
            1.75f);
        innateAptitudes = new Dictionary<string, int>(
            request.InnateAptitudes,
            StringComparer.Ordinal);
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
    public float EarnedWorkExperienceMultiplier =>
        earnedWorkExperienceMultiplier;

    public float GetBehaviorUtilityMultiplier(
        IEnumerable<string> actionSemanticTags)
    {
        string[] tags = (actionSemanticTags ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        float delta = behaviorUtilityDeltas
            .Where(pair => BehaviorMatchesAction(pair.Key, tags))
            .Sum(pair => pair.Value);
        return Mathf.Clamp(1f + delta, 0.25f, 2f);
    }

    public float GetEventWeightMultiplier(params string[] categoryOrEventIds)
    {
        float multiplier = 1f;
        foreach (string id in categoryOrEventIds ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(id)
                && eventWeightMultipliers.TryGetValue(id.Trim(), out float value))
                multiplier *= value;
        }
        return Mathf.Clamp(multiplier, 0.1f, 10f);
    }

    private static IReadOnlyDictionary<string, float> BuildBehaviorUtilityDeltas(
        IEnumerable<CharacterTraitSO> traits) =>
        (traits ?? Array.Empty<CharacterTraitSO>())
        .Where(value => value != null)
        .SelectMany(value =>
        {
            IEnumerable<KeyValuePair<string, float>> shared =
                (value.identityRules ?? new List<CharacterIdentityRule>())
                .OfType<BehaviorUtilityRule>()
                .Where(rule => !string.IsNullOrWhiteSpace(rule.behaviorTag)
                    && !Mathf.Approximately(rule.utilityDelta, 0f))
                .Select(rule => new KeyValuePair<string, float>(
                    rule.behaviorTag.Trim(),
                    rule.utilityDelta));
            IEnumerable<KeyValuePair<string, float>> legacy =
                (value.behaviorPreferences
                    ?? new List<CharacterTraitBehaviorPreference>())
                .Where(rule => rule != null && rule.IsValid)
                .Select(rule => new KeyValuePair<string, float>(
                    rule.behaviorTag.Trim(),
                    rule.utilityDelta));
            return shared.Any() ? shared : legacy;
        })
        .GroupBy(value => value.Key, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => Mathf.Clamp(group.Sum(value => value.Value), -0.75f, 1f),
            StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, float> BuildEventWeightMultipliers(
        IEnumerable<CharacterTraitSO> traits) =>
        (traits ?? Array.Empty<CharacterTraitSO>())
        .Where(value => value != null)
        .SelectMany(value =>
        {
            IEnumerable<KeyValuePair<string, float>> shared =
                (value.identityRules ?? new List<CharacterIdentityRule>())
                .OfType<IncidentWeightRule>()
                .Where(rule => !string.IsNullOrWhiteSpace(rule.incidentId)
                    && !Mathf.Approximately(rule.multiplier, 1f))
                .Select(rule => new KeyValuePair<string, float>(
                    rule.incidentId.Trim(),
                    rule.multiplier));
            IEnumerable<KeyValuePair<string, float>> legacy =
                (value.eventWeights ?? new List<CharacterTraitEventWeight>())
                .Where(rule => rule != null && rule.IsValid)
                .Select(rule => new KeyValuePair<string, float>(
                    rule.eventCategoryId.Trim(),
                    rule.multiplier));
            return shared.Any() ? shared : legacy;
        })
        .GroupBy(value => value.Key, StringComparer.Ordinal)
        .ToDictionary(
            group => group.Key,
            group => Mathf.Clamp(
                group.Aggregate(1f, (current, value) => current * value.Value),
                0.1f,
                10f),
            StringComparer.Ordinal);

    private static bool BehaviorMatchesAction(
        string behaviorTag,
        IReadOnlyCollection<string> actionTags)
    {
        return actionTags.Contains(behaviorTag, StringComparer.Ordinal);
    }

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
            BuildFinalModifiers(species, traits),
            traits,
            species);
    }

    public float GetStayDurationMultiplier()
    {
        return speciesStayDurationMultiplier;
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

    public float GetWorkPreferenceScore(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? CalculateWorkPreferenceScore(FacilityWorkTypeMap.GetRequired(definition))
            : 0.5f;
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

    private static CharacterModelModifiers BuildFinalModifiers(
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> traits)
    {
        CharacterModelModifiers result = new CharacterModelModifiers();
        if (species != null)
        {
            result.Multiply(CopyLegacyPreferencesOnly(species.modifiers));
        }
        foreach (CharacterTraitSO trait in traits ?? Array.Empty<CharacterTraitSO>())
        {
            if (trait == null) continue;
            result.Multiply(CopyLegacyPreferencesOnly(trait.modifiers));
        }

        return result;
    }

    private static bool HasEffectTarget(
        CharacterTraitSO trait,
        string targetId) =>
        trait != null && trait.Effects.Any(binding =>
            binding?.definition != null
            && string.Equals(
                binding.definition.TargetId,
                targetId,
                StringComparison.Ordinal));

    private static CharacterModelModifiers CopyLegacyPreferencesOnly(
        CharacterModelModifiers source)
    {
        CharacterModelModifiers result = new();
        if (source == null) return result;
        result.preferredFacilityRoles = source.preferredFacilityRoles;
        result.dislikedFacilityRoles = source.dislikedFacilityRoles;
        result.SetWorkPreferences(
            source.PreferredLegacyWorkTypes,
            source.DislikedLegacyWorkTypes);
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
