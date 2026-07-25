using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FacilityCrimeRiskDebugScenarios
{
    private const float Epsilon = 0.0001f;

    [MenuItem("DungeonStory/Debug/Facilities/Run Crime Risk Checks")]
    public static void RunAllFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        VerifySharedSettingsAsset();
        VerifyLegacyFacilityFieldsRemoved();
        VerifyDefaultFormula();
        VerifyBuildingModifierAbility();
        VerifySpeciesDataMultiplier();
        VerifyTriggerBoundary();
        Debug.Log("FacilityCrimeRiskDebugScenarios passed: shared policy, building modifiers, species data, and trigger boundaries.");
    }

    private static void VerifySharedSettingsAsset()
    {
        FacilityCrimeSettingsSO settings = AssetDatabase.LoadAssetAtPath<FacilityCrimeSettingsSO>(
            "Assets/Resources/Config/FacilityCrimeSettings.asset");
        Require(settings != null, "FacilityCrimeSettings asset is missing.");
        RequireApproximately(settings.BaseCrimePressure, 0.01f, "base pressure");
        RequireApproximately(settings.UnstaffedSupervisionRisk, 0.07f, "unstaffed pressure");
        RequireApproximately(settings.StaffedSupervisionReduction, 0.03f, "staffed reduction");
        RequireApproximately(settings.OperationalRiskScale, 10f, "operational scale");
    }

    private static void VerifyLegacyFacilityFieldsRemoved()
    {
        string[] legacyFields =
        {
            "baseCrimePressure",
            "unstaffedSupervisionRisk",
            "staffedSupervisionReduction",
            "lowMoodCrimeRiskWeight",
            "unmetNeedCrimeRiskWeight",
            "crowdCrimeRiskWeight",
            "highValueCrimeRiskWeight",
            "damagedFacilityCrimeRiskWeight"
        };

        foreach (string fieldName in legacyFields)
        {
            Require(typeof(FacilityData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public) == null,
                $"FacilityData still owns shared crime field {fieldName}.");
        }
    }

    private static void VerifyDefaultFormula()
    {
        BuildingFixture fixture = new BuildingFixture();
        try
        {
            IFacilityCrimeRiskEvaluator evaluator = FacilityCrimeEditorTestDependencies.Evaluator;
            float baseline = evaluator.CalculateShopliftingChance(Context(fixture.Building));
            float staffed = evaluator.CalculateShopliftingChance(Context(
                fixture.Building,
                hasServingWorker: true));
            float crowded = evaluator.CalculateShopliftingChance(Context(
                fixture.Building,
                currentUserCount: fixture.Data.Facility.capacity));
            float valuableCart = evaluator.CalculateShopliftingChance(Context(
                fixture.Building,
                cartItemCount: 4,
                cartValue: 500));
            float damaged = evaluator.CalculateShopliftingChance(Context(
                fixture.Building,
                isDamaged: true));
            float operational = evaluator.CalculateOperationalRisk(Context(fixture.Building));

            RequireApproximately(baseline, 0.08f, "default baseline");
            RequireApproximately(staffed, 0f, "staffed baseline");
            RequireApproximately(crowded, 0.12f, "crowded baseline");
            RequireApproximately(valuableCart, 0.13f, "valuable cart baseline");
            RequireApproximately(damaged, 0.13f, "damaged baseline");
            RequireApproximately(operational, 0.8f, "operational baseline");

            fixture.Data.AbilityModules.Add(new BuildingInternalStockAbility
            {
                capacity = 20,
                restockRequestThreshold = 10
            });
            float lowStock = evaluator.CalculateShopliftingChance(Context(fixture.Building));
            RequireApproximately(lowStock, 0.0925f, "low stock baseline");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static void VerifyBuildingModifierAbility()
    {
        BuildingFixture fixture = new BuildingFixture(
            new BuildingCrimeRiskModifierAbility
            {
                multiplier = 2f,
                flatOffset = 0.01f
            });
        try
        {
            float chance = FacilityCrimeEditorTestDependencies.Evaluator
                .CalculateShopliftingChance(Context(fixture.Building));
            RequireApproximately(chance, 0.17f, "building ability modifier");
            Require(fixture.Data.Abilities.OfType<IBuildingCrimeRiskModifier>().Count() == 1,
                "Crime modifier is not exposed through the building ability list.");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static void VerifySpeciesDataMultiplier()
    {
        BuildingFixture fixture = new BuildingFixture();
        CharacterSpeciesSO species = ScriptableObject.CreateInstance<CharacterSpeciesSO>();
        CharacterSO characterData = ScriptableObject.CreateInstance<CharacterSO>();
        GameObject actorObject = null;
        try
        {
            species.speciesTag = "ScenarioSpecies";
            species.crimeRiskMultiplier = 1.37f;
            species.incidentType = CharacterSpeciesIncidentType.None;
            characterData.characterType = CharacterType.Customer;
            characterData.characterName = "Crime Risk Scenario Actor";
            characterData.species = species;
            characterData.speciesTag = species.speciesTag;
            characterData.baseStats = CharacterStatBlock.CreateDefault();

            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject("Crime Risk Scenario Actor");
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.Initialize(characterData);
            foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
            {
                actor.Stats.Stats[condition] = 100f;
            }

            float chance = FacilityCrimeEditorTestDependencies.Evaluator.CalculateShopliftingChance(
                Context(fixture.Building, actor: actor));
            RequireApproximately(actor.GetCrimeRiskMultiplier(), 1.37f, "runtime species multiplier");
            Require(actor.GetIncidentType() == CharacterSpeciesIncidentType.None,
                "Scenario species unexpectedly depends on an incident enum value.");
            RequireApproximately(chance, 0.08f * 1.37f, "species-adjusted chance");

            VerifySpeciesAsset("Species_Slime", 1.05f);
            VerifySpeciesAsset("Species_Orc", 1.2f);
            VerifySpeciesAsset("Species_Vampire", 1.1f);
        }
        finally
        {
            if (actorObject != null) UnityEngine.Object.DestroyImmediate(actorObject);
            UnityEngine.Object.DestroyImmediate(characterData);
            UnityEngine.Object.DestroyImmediate(species);
            fixture.Dispose();
        }
    }

    private static void VerifyTriggerBoundary()
    {
        IFacilityCrimeRiskEvaluator evaluator = FacilityCrimeEditorTestDependencies.Evaluator;
        Require(!evaluator.ShouldTriggerCrime(0f, 0f), "Zero chance triggered crime.");
        Require(evaluator.ShouldTriggerCrime(0.5f, 0.499f), "Roll below chance did not trigger crime.");
        Require(!evaluator.ShouldTriggerCrime(0.5f, 0.5f), "Roll equal to chance triggered crime.");
        Require(evaluator.ShouldTriggerCrime(2f, 0.999f), "Clamped full chance did not trigger crime.");
    }

    private static FacilityCrimeRiskContext Context(
        BuildableObject building,
        CharacterActor actor = null,
        bool hasServingWorker = false,
        bool hasWaitingCheckout = false,
        int currentUserCount = 0,
        int cartItemCount = 1,
        int cartValue = 0,
        int currentStock = 10,
        bool isDamaged = false)
    {
        return new FacilityCrimeRiskContext(
            building,
            actor,
            hasServingWorker,
            hasWaitingCheckout,
            currentUserCount,
            cartItemCount,
            cartValue,
            currentStock,
            isDamaged);
    }

    private static void VerifySpeciesAsset(string assetName, float expectedMultiplier)
    {
        CharacterSpeciesSO species = AssetDatabase.LoadAssetAtPath<CharacterSpeciesSO>(
            $"Assets/Resources/SO/Character/Species/{assetName}.asset");
        Require(species != null, $"Missing species asset {assetName}.");
        RequireApproximately(species.crimeRiskMultiplier, expectedMultiplier, $"{assetName} multiplier");
    }

    private static void RequireApproximately(float actual, float expected, string label)
    {
        Require(Mathf.Abs(actual - expected) <= Epsilon,
            $"Unexpected {label}: expected {expected:0.####}, got {actual:0.####}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class BuildingFixture : IDisposable
    {
        public BuildingFixture(BuildingAbility ability = null)
        {
            Data = ScriptableObject.CreateInstance<BuildingSO>();
            Data.id = 999901;
            Data.width = 1;
            Data.height = 1;
            Data.layer = GridLayer.Building;
            Data.category = BuildingCategory.Shop;
            Data.type = typeof(BuildableObject);
            Data.ReplaceAbilities(new BuildingAbilityCollection());
            Data.Facility = new FacilityData
            {
                roles = FacilityRole.Purchase,
                capacity = 2
            };
            if (ability != null)
            {
                Data.AbilityModules.Add(ability);
            }

            Object = new GameObject("Crime Risk Scenario Building");
            Building = Object.AddComponent<BuildableObject>();
            Building.Initialization(Data, Vector2Int.zero);
        }

        public GameObject Object { get; }
        public BuildableObject Building { get; }
        public BuildingSO Data { get; }

        public void Dispose()
        {
            if (Object != null) UnityEngine.Object.DestroyImmediate(Object);
            if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
        }
    }
}
