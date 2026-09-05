#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class CropHarvestReachableMaximumWitnessDebugScenarios
{
    private const string MenuPath =
        "DungeonStory/V27/Production/Validate Crop Reachable Maximum Witness";
    private const string SpeciesTag = "Beastkin";
    private const int GoldenHarvestTraitId = 304;
    private const string GoldenHarvestConditionId =
        "state:golden-harvest-jackpot";

    [MenuItem(MenuPath)]
    public static void ValidateFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "Crop reachable-maximum witness requires Play Mode.");
        }

        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null)
            ?? throw new InvalidOperationException(
                "Dungeon runtime lifetime scope is not ready.");
        GameObject actorObject = null;
        CharacterSO actorData = null;
        try
        {
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Crop Reachable Maximum Witness");
            actorObject.SetActive(false);
            if (actorObject.GetComponent<AbilityWork>() == null)
                actorObject.AddComponent<AbilityWork>();
            scope.Container.InjectGameObject(actorObject);
            actorObject.SetActive(true);

            actorData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "Crop Reachable Maximum Witness",
                    SpeciesTag);
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(
                new GuidPersistentIdGenerator().NewCharacterId());
            scope.Container.Resolve<ICharacterNarrativeCommand>().Register(
                new CharacterId(actor.Identity.PersistentId),
                new CharacterSpeciesId(SpeciesTag),
                Array.Empty<string>(),
                Array.Empty<string>(),
                BuiltInCharacterProficiencyIds.All.Select(id =>
                    new CharacterStartingProficiencyExperience
                    {
                        proficiencyId = id.Value,
                        experience = 100,
                        learningMultiplier = 1f
                    }).ToArray());
            actor.RefreshAbilityCache();
            actor.Initialize(actorData);
            actor.Progression.ApplyPreparedIdentity(
                "Crop Reachable Maximum Witness",
                SpeciesTag,
                new[] { GoldenHarvestTraitId },
                CharacterPotentialGrade.Ordinary,
                generationSeed: 157181,
                autoChooseDrafts: false);
            actor.SetLifecycleState(CharacterLifecycleState.Active);

            ICharacterProficiencyCommand proficiencies = scope.Container
                .Resolve<ICharacterProficiencyCommand>();
            IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
            CharacterId characterId = CharacterPersistentIdentity.Require(actor);
            proficiencies.AddDirectExperience(
                characterId,
                BuiltInCharacterProficiencyIds.FoodProduction,
                3060f,
                calendar.AbsoluteHour,
                applyLearningMultiplier: false);
            proficiencies.AddDirectExperience(
                characterId,
                BuiltInCharacterProficiencyIds.Fieldwork,
                3060f,
                calendar.AbsoluteHour,
                applyLearningMultiplier: false);

            CharacterPerformanceSnapshot performance = scope.Container
                .Resolve<ICharacterPerformanceQuery>()
                .Evaluate(
                    actor,
                    CropHarvestOutputRules.PerformanceFormulaId,
                    new CharacterPerformanceEvaluationContext
                    {
                        GameplayEffectContext = new GameplayEffectContext(
                            new[] { GoldenHarvestConditionId })
                    });
            Require(performance.IsApplicable,
                performance.Failure?.Message
                ?? "Crop harvest performance witness is not applicable.");
            RequireNear(performance.FunctionalCapacityFactor, 1.1125f,
                "functional capacity");
            RequireNear(
                performance.ProficiencyFactor,
                100f / 58f,
                "Master-current-cap proficiency");
            RequireNear(performance.GameplayEffectFactor, 2.5f,
                "Golden Harvest jackpot effect");
            RequireNear(
                performance.Value,
                1.1125f * (100f / 58f) * 2.5f,
                "worker harvest yield");
            CropHarvestReachableMaximumWitnessSnapshot offline = new
                NaturalGoldenHarvestReachableMaximumWitnessContributor(
                    scope.Container.Resolve<IGameContentDefinitionSource>(),
                    scope.Container.Resolve<CharacterPerformanceFormulaCatalog>())
                .Capture();
            RequireNear(
                offline.WorkerYieldMultiplier,
                performance.Value,
                "offline/live worker witness equality");
            RequireNear(
                offline.ReturnedSeedMultiplier,
                1.5f,
                "offline returned-seed witness");

            IResourceEconomyContentCatalog economy = scope.Container
                .Resolve<IResourceEconomyContentCatalog>();
            CropDefinitionSO crop = economy.Crops.Single(value =>
                value != null
                && string.Equals(
                    value.CropId,
                    "crop:ember-root",
                    StringComparison.Ordinal));
            CropGenomeReachableMaximumWitnessSnapshot genome = new
                CropGenomeReachableMaximumWitnessCatalog(
                    scope.Container.Resolve<IGameContentDefinitionSource>())
                .Capture(crop.CropId);
            Require(string.Equals(
                    genome.GenomeId,
                    "genome:ember-root:heavy",
                    StringComparison.Ordinal),
                "Ember root maximum must use its real authored heavy seed.");
            int outdoor = CropHarvestOutputRules.ResolveHarvestQuantity(
                crop.Yield,
                1f,
                performance.Value,
                1f,
                genome.YieldMultiplier,
                hasSoilDiagnostics: true);
            int indoor = CropHarvestOutputRules.ResolveHarvestQuantity(
                crop.Yield,
                ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
                        "crop-indoor")
                    .Numerator
                    / (float)ProductionOutputFactorAuthority
                        .ResolveMaximumGrandProject("crop-indoor").Denominator,
                performance.Value,
                1f,
                genome.YieldMultiplier,
                hasSoilDiagnostics: true);
            int seeds = CropHarvestOutputRules.ResolveReturnedSeedQuantity(
                CropHarvestOutputRules.MaximumReturnedSeedCount,
                1.5f,
                hasSeedSelection: true);
            Require(outdoor == 28,
                $"Outdoor reachable maximum expected 28, got {outdoor}.");
            Require(indoor == 33,
                $"Indoor reachable maximum expected 33, got {indoor}.");
            Require(seeds == 7,
                $"Returned seed reachable maximum expected 7, got {seeds}.");

            Debug.Log(
                "CROP_REACHABLE_MAXIMUM_WITNESS_PASS;"
                + $"species={SpeciesTag};trait={GoldenHarvestTraitId};"
                + "masterExperience=3060;"
                + $"capacity={performance.FunctionalCapacityFactor:R};"
                + $"proficiency={performance.ProficiencyFactor:R};"
                + $"effect={performance.GameplayEffectFactor:R};"
                + $"worker={performance.Value:R};"
                + $"genome={genome.GenomeId};"
                + $"outdoor={outdoor};indoor={indoor};seeds={seeds};"
                + $"digest={offline.SourceDigest}");
        }
        finally
        {
            if (actorObject != null)
                UnityEngine.Object.Destroy(actorObject);
            if (actorData != null)
                UnityEngine.Object.Destroy(actorData);
        }
    }

    private static void RequireNear(float actual, float expected, string label)
    {
        if (Mathf.Abs(actual - expected) > 0.0001f)
        {
            throw new InvalidOperationException(
                $"Crop witness {label} expected {expected:R}, got {actual:R}.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
