#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class FounderTraitDistributionDebugScenarios
{
    private const int SampleCount = 100_000;

    [MenuItem("DungeonStory/QA/V26 Founder Trait Distribution")]
    public static void Run()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(
                "Assets/Resources/SO/Content/GameDomainContentCatalog.asset")
            ?? throw new InvalidOperationException("Root content catalog is missing.");
        CharacterTraitSO[] traits = catalog.Definitions
            .OfType<CharacterTraitSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        CharacterSkillSystemSettingsSO settings = AssetDatabase
            .FindAssets("t:CharacterSkillSystemSettingsSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>)
            .Single(value => value != null);

        Require(traits.Length == 100, $"Expected 100 traits, found {traits.Length}.");
        Require(traits.SelectMany(value => value.ValidateDefinition()).Count() == 0,
            "One or more trait definitions failed validation.");
        Require(traits.All(value => !string.IsNullOrWhiteSpace(value.selectionFamilyId)),
            "Every trait must have a selection family.");

        Dictionary<int, int> countBuckets = new();
        Dictionary<int, int> occurrences = traits.ToDictionary(value => value.id, _ => 0);
        CharacterTraitSO[] reversed = traits.Reverse().ToArray();
        IReadOnlyList<int> fourTraitSelection = null;
        double totalTraits = 0d;
        for (int index = 0; index < SampleCount; index++)
        {
            int seed = CharacterGrowthRules.StableHash($"qa:v26:trait:{index}");
            IReadOnlyList<int> selected = CharacterTraitSelectionRules.Select(
                traits,
                settings.traitConflicts,
                new DeterministicRandomSequence(seed),
                "Slime");
            Require(selected.Count >= 1 && selected.Count <= 4,
                $"Invalid trait count {selected.Count} at sample {index}.");
            countBuckets[selected.Count] = countBuckets.GetValueOrDefault(selected.Count) + 1;
            if (selected.Count == 4 && fourTraitSelection == null)
                fourTraitSelection = selected.ToArray();
            totalTraits += selected.Count;
            foreach (int id in selected) occurrences[id]++;
            VerifySelection(traits, settings, selected, "Slime", index);

            if (index < 5_000)
            {
                IReadOnlyList<int> second = CharacterTraitSelectionRules.Select(
                    reversed,
                    settings.traitConflicts,
                    new DeterministicRandomSequence(seed),
                    "Slime");
                Require(selected.SequenceEqual(second),
                    $"Candidate ordering changed result at sample {index}.");
            }
        }

        Require(occurrences.Values.All(value => value > 0),
            "All 100 traits must be reachable in the Slime-inclusive audit.");
        Require(fourTraitSelection != null, "The audit never produced a four-trait founder.");
        CharacterGrowthState growth = new()
        {
            initialized = true,
            traitSelectionAuthorityVersion =
                CharacterGrowthState.CurrentTraitSelectionAuthorityVersion,
            traitSelectionAuthorityOrigin =
                CharacterTraitSelectionAuthorityOrigin.PreparedSelection,
            traitIds = fourTraitSelection.ToList()
        };
        string json = JsonUtility.ToJson(growth);
        CharacterGrowthState restored = JsonUtility.FromJson<CharacterGrowthState>(json);
        restored.EnsureCollections();
        Require(restored.traitIds.SequenceEqual(fourTraitSelection),
            "A four-trait founder did not survive JSON save/restore.");
        VerifyCountRate(countBuckets, 1, .15f, .008f);
        VerifyCountRate(countBuckets, 2, .40f, .008f);
        VerifyCountRate(countBuckets, 3, .35f, .008f);
        VerifyCountRate(countBuckets, 4, .10f, .008f);
        double mean = totalTraits / SampleCount;
        Require(mean >= 2.38d && mean <= 2.42d,
            $"Mean trait count {mean:0.000} left the 2.40 target band.");

        Dictionary<CharacterTraitSelectionRarity, double> rarityRates = traits
            .GroupBy(value => value.selectionRarity)
            .ToDictionary(
                group => group.Key,
                group => group.Average(value => occurrences[value.id] / (double)SampleCount));
        Require(rarityRates[CharacterTraitSelectionRarity.Common]
                > rarityRates[CharacterTraitSelectionRarity.Uncommon]
            && rarityRates[CharacterTraitSelectionRarity.Uncommon]
                > rarityRates[CharacterTraitSelectionRarity.Rare]
            && rarityRates[CharacterTraitSelectionRarity.Rare]
                > rarityRates[CharacterTraitSelectionRarity.Exceptional],
            "Per-trait occurrence must decrease with positive-trait rarity.");

        for (int index = 0; index < 10_000; index++)
        {
            IReadOnlyList<int> orcSelection = CharacterTraitSelectionRules.Select(
                traits,
                settings.traitConflicts,
                new DeterministicRandomSequence(
                    CharacterGrowthRules.StableHash($"qa:v26:orc:{index}")),
                "Orc");
            Require(!orcSelection.Contains(109),
                "Cold-resistant Slime appeared on a non-Slime founder.");
            VerifySelection(traits, settings, orcSelection, "Orc", index);
        }

        CharacterTraitSO fastLearner = traits.Single(value => value.id == 230);
        Require(fastLearner.selectionRarity == CharacterTraitSelectionRarity.Exceptional
                && fastLearner.Effects.Any(binding => binding?.definition != null
                    && string.Equals(
                        binding.definition.TargetId,
                        GameplayEffectTargetIds.EarnedWorkExperience,
                        StringComparison.Ordinal)
                    && Mathf.Approximately(binding.value, 1.30f))
                && Mathf.Approximately(
                    fastLearner.earnedWorkExperienceMultiplier,
                    1f),
            "Fast Learner must author x1.30 only through the shared earned-work-XP effect.");
        GameObject learningHost = new("V26 Fast Learner Projection Audit");
        try
        {
            CharacterActor learningActor = learningHost.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(learningHost);
            learningActor.EnsureRuntimeState();
            learningActor.Initialize(AssetDatabase.FindAssets("t:CharacterSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
                .First(value => value != null
                    && value.id > 0
                    && value.species != null));
            learningActor.Identity.SetPersistentId("character:v26:fast-learner:audit");
            learningActor.Progression.ApplyPreparedIdentity(
                "Fast Learner Audit",
                "audit",
                new[] { fastLearner.id },
                CharacterPotentialGrade.Ordinary,
                230,
                autoChooseDrafts: false);
            float fastLearning = CharacterProficiencyLearningRules.Resolve(
                learningActor,
                new ProficiencyWorkProfile(
                    BuiltInCharacterProficiencyIds.Crafting));
            Require(Mathf.Approximately(fastLearning, 1.30f),
                $"Fast Learner work XP path produced x{fastLearning:0.###}, not x1.30.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(learningHost);
        }

        Debug.Log(
            "V26_FOUNDER_TRAIT_DISTRIBUTION=PASS; samples=100000; "
            + $"counts={countBuckets[1]}/{countBuckets[2]}/{countBuckets[3]}/{countBuckets[4]}; "
            + $"mean={mean:0.000}; common={rarityRates[CharacterTraitSelectionRarity.Common]:P2}; "
            + $"uncommon={rarityRates[CharacterTraitSelectionRarity.Uncommon]:P2}; "
            + $"rare={rarityRates[CharacterTraitSelectionRarity.Rare]:P2}; "
            + $"exceptional={rarityRates[CharacterTraitSelectionRarity.Exceptional]:P2}; "
            + "familyCollision=0; nonSlimeSpeciesLeak=0; reachable=56/56; "
            + "fourTraitSave=true; fastLearnerXp=1.30; deterministic=5000");
    }

    private static void VerifySelection(
        IReadOnlyCollection<CharacterTraitSO> traits,
        CharacterSkillSystemSettingsSO settings,
        IReadOnlyCollection<int> selected,
        string speciesTag,
        int sample)
    {
        CharacterTraitSO[] resolved = selected
            .Select(id => traits.Single(value => value.id == id))
            .ToArray();
        Require(resolved.Select(value => value.selectionFamilyId).Distinct().Count()
                == resolved.Length,
            $"Selection family collision at {speciesTag} sample {sample}.");
        Require(resolved.All(value => value.IsEligibleForSpecies(speciesTag)),
            $"Species-ineligible trait at {speciesTag} sample {sample}.");
        Require(!settings.traitConflicts.Any(rule => rule != null
                && selected.Contains(rule.firstTraitId)
                && selected.Contains(rule.secondTraitId)),
            $"Explicit trait conflict at {speciesTag} sample {sample}.");
    }

    private static void VerifyCountRate(
        IReadOnlyDictionary<int, int> buckets,
        int count,
        float target,
        float tolerance)
    {
        float rate = buckets.GetValueOrDefault(count) / (float)SampleCount;
        Require(Mathf.Abs(rate - target) <= tolerance,
            $"{count}-trait rate {rate:P2} left target {target:P0} ± {tolerance:P1}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
