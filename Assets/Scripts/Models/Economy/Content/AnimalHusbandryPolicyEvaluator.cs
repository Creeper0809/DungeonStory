using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryPolicyEvaluator
{
    private static readonly Comparison<HusbandryAnimalState> OldestFirst =
        CompareOldestFirst;

    private readonly IWildlifeSpeciesDefinitionCatalog speciesCatalog;
    private readonly Dictionary<(BuildingInstanceId PenId, WildlifeSpeciesId SpeciesId), List<HusbandryAnimalState>>
        slaughterGroups =
            new Dictionary<(BuildingInstanceId PenId, WildlifeSpeciesId SpeciesId), List<HusbandryAnimalState>>();
    private readonly List<HusbandryAnimalState> femaleCandidates =
        new List<HusbandryAnimalState>();
    private readonly List<HusbandryAnimalState> maleCandidates =
        new List<HusbandryAnimalState>();
    private readonly List<HusbandryAnimalState> juvenileCandidates =
        new List<HusbandryAnimalState>();

    public AnimalHusbandryPolicyEvaluator(
        IWildlifeSpeciesDefinitionCatalog speciesCatalog)
    {
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
    }

    public void RefreshAutoSlaughterDesignations(
        IEnumerable<HusbandryAnimalState> animals,
        Func<BuildingInstanceId, AnimalPenPolicyData> getPolicy,
        Func<HusbandryAnimalState, bool> isAdult)
    {
        foreach (HusbandryAnimalState state in animals)
        {
            if (state.AutoSlaughterDesignated)
            {
                state.SlaughterDesignated = false;
                state.AutoSlaughterDesignated = false;
            }
        }

        foreach (List<HusbandryAnimalState> group in slaughterGroups.Values)
        {
            group.Clear();
        }

        foreach (HusbandryAnimalState state in animals)
        {
            var key = (state.PenId, state.SpeciesId);
            if (!slaughterGroups.TryGetValue(
                    key,
                    out List<HusbandryAnimalState> group))
            {
                group = new List<HusbandryAnimalState>();
                slaughterGroups.Add(key, group);
            }

            group.Add(state);
        }

        foreach (KeyValuePair<(BuildingInstanceId PenId, WildlifeSpeciesId SpeciesId), List<HusbandryAnimalState>>
                 entry in slaughterGroups)
        {
            List<HusbandryAnimalState> members = entry.Value;
            if (members.Count == 0)
            {
                continue;
            }

            AnimalPenPolicyData policy = getPolicy(entry.Key.PenId);
            femaleCandidates.Clear();
            maleCandidates.Clear();
            juvenileCandidates.Clear();
            foreach (HusbandryAnimalState member in members)
            {
                if (!isAdult(member))
                {
                    juvenileCandidates.Add(member);
                }
                else if (member.Sex == AnimalSex.Female)
                {
                    femaleCandidates.Add(member);
                }
                else if (member.Sex == AnimalSex.Male)
                {
                    maleCandidates.Add(member);
                }
            }

            femaleCandidates.Sort(OldestFirst);
            maleCandidates.Sort(OldestFirst);
            juvenileCandidates.Sort(OldestFirst);
            MarkExcess(
                femaleCandidates,
                policy.adultFemaleLimit,
                policy.minimumBreedingFemales,
                policy.protectPregnant);
            MarkExcess(
                maleCandidates,
                policy.adultMaleLimit,
                policy.minimumBreedingMales,
                false);
            MarkExcess(
                juvenileCandidates,
                policy.juvenileLimit,
                0,
                false);
        }
    }

    public float CalculatePenCompatibilityRisk(
        IReadOnlyList<HusbandryAnimalState> occupants,
        AnimalPenPolicyData policy)
    {
        int occupantCount = occupants?.Count ?? 0;
        if (occupantCount == 0)
        {
            return 0f;
        }

        float weightedSeverity = 0f;
        float maximumSeverity = 0f;
        bool hasPlantEater = false;
        bool hasMeatEater = false;
        if (occupantCount > policy.maximumAnimals)
        {
            float severity = Mathf.Clamp01(
                (occupantCount - policy.maximumAnimals)
                / (float)Mathf.Max(1, policy.maximumAnimals));
            weightedSeverity += severity;
            maximumSeverity = Mathf.Max(maximumSeverity, severity);
        }

        for (int leftIndex = 0; leftIndex < occupantCount; leftIndex++)
        {
            if (!TryGetSpecies(occupants[leftIndex], out WildlifeSpeciesDefinition left))
            {
                continue;
            }

            hasPlantEater |= left.Diet == WildlifeDietType.Herbivore;
            hasMeatEater |= left.Diet == WildlifeDietType.Carnivore;
            for (int rightIndex = leftIndex + 1;
                 rightIndex < occupantCount;
                 rightIndex++)
            {
                if (!TryGetSpecies(
                        occupants[rightIndex],
                        out WildlifeSpeciesDefinition right))
                {
                    continue;
                }

                bool predatorPrey =
                    left.Diet == WildlifeDietType.Carnivore
                    && right.Diet == WildlifeDietType.Herbivore
                    || right.Diet == WildlifeDietType.Carnivore
                    && left.Diet == WildlifeDietType.Herbivore;
                if (predatorPrey)
                {
                    const float severity = 0.9f;
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                }

                float aggression = Mathf.Max(left.Aggression, right.Aggression);
                if (aggression >= 0.5f)
                {
                    float severity = Mathf.Clamp01(aggression * 0.6f);
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                }

                float sizeRatio = Mathf.Max(
                    left.Husbandry.BodySize,
                    right.Husbandry.BodySize)
                    / Mathf.Max(
                        0.1f,
                        Mathf.Min(
                            left.Husbandry.BodySize,
                            right.Husbandry.BodySize));
                if (sizeRatio >= 3f)
                {
                    float severity = Mathf.InverseLerp(3f, 8f, sizeRatio);
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                }
            }
        }

        if (hasPlantEater && hasMeatEater)
        {
            const float severity = 0.55f;
            weightedSeverity += severity;
            maximumSeverity = Mathf.Max(maximumSeverity, severity);
        }

        float risk = maximumSeverity <= 0f
            ? 0f
            : Mathf.Clamp01(maximumSeverity + weightedSeverity * 0.08f);
        return policy.allowRiskyMixing ? risk * 0.75f : risk;
    }

    private bool TryGetSpecies(
        HusbandryAnimalState state,
        out WildlifeSpeciesDefinition species)
    {
        species = null;
        return state != null
            && speciesCatalog.TryGetSpecies(state.SpeciesId.Value, out species);
    }

    private static int CompareOldestFirst(
        HusbandryAnimalState left,
        HusbandryAnimalState right)
    {
        return (right?.AgeDays ?? 0f).CompareTo(left?.AgeDays ?? 0f);
    }

    private static void MarkExcess(
        IReadOnlyList<HusbandryAnimalState> candidates,
        int limit,
        int protectedMinimum,
        bool protectPregnant)
    {
        int maximum = Mathf.Max(limit, protectedMinimum);
        int excess = Mathf.Max(0, candidates.Count - maximum);
        for (int index = 0; index < candidates.Count && excess > 0; index++)
        {
            HusbandryAnimalState state = candidates[index];
            if (protectPregnant && state.Pregnant)
            {
                continue;
            }

            state.SlaughterDesignated = true;
            state.AutoSlaughterDesignated = true;
            AnimalHusbandryWorkRules.SetStatus(
                state,
                AnimalHusbandryStatusCode.AutoSlaughterPolicyTarget);
            excess--;
        }
    }
}
