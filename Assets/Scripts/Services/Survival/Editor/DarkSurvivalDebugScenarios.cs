using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DarkSurvivalDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Survival/Run Dark Survival Scenarios")]
    public static void RunFromMenu()
    {
        List<string> errors = RunAll(logSuccess: true);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Dark survival scenarios failed:\n" + string.Join("\n", errors));
        }

        Debug.Log("Dark survival scenarios passed.");
    }

    public static List<string> RunAll(bool logSuccess = false)
    {
        List<string> errors = new List<string>();
        Run("burden_curve", VerifyBurdenCurve, errors, logSuccess);
        Run("damage_requires_current_deprivation", VerifyDamageRequiresCurrentDeprivation, errors, logSuccess);
        Run("breakdown_thresholds", VerifyBreakdownThresholds, errors, logSuccess);
        Run("thirst_need_and_terrain", VerifyThirstNeedAndTerrain, errors, logSuccess);
        Run("filth_cleaning_work", VerifyFilthCleaningWork, errors, logSuccess);
        Run("shared_water_patch_save", VerifySharedWaterPatchSave, errors, logSuccess);
        Run("humanoid_corpse_contract", VerifyHumanoidCorpseContract, errors, logSuccess);
        Run("current_root_round_trip", VerifyCurrentRootRoundTrip, errors, logSuccess);
        Run("deprivation_authority", VerifyDeprivationAuthority, errors, logSuccess);
        Run("separate_breakdown_actions", VerifySeparateBreakdownActions, errors, logSuccess);
        Run("permanent_taboo_social_memory", VerifyPermanentTabooSocialMemory, errors, logSuccess);
        return errors;
    }

    private static string VerifyBurdenCurve()
    {
        Require(Mathf.Approximately(CharacterDeprivationRuntime.CalculateBurdenDelta(20f, 1f), 0f),
            "burden changed at the lower neutral boundary");
        Require(Mathf.Approximately(CharacterDeprivationRuntime.CalculateBurdenDelta(10f, 1f), 1f),
            "quadratic half deficit was not one work unit");
        Require(Mathf.Approximately(CharacterDeprivationRuntime.CalculateBurdenDelta(0f, 1f), 4f),
            "maximum deficit did not accumulate four burden per second");
        Require(CharacterDeprivationRuntime.CalculateBurdenDelta(40f, 1f) < 0f,
            "burden did not recover at 40 need");
        Require(CharacterDeprivationRuntime.CalculateBurdenDelta(100f, 1f)
                < CharacterDeprivationRuntime.CalculateBurdenDelta(40f, 1f),
            "high need did not recover faster");
        return "quadratic accumulation and recovery verified";
    }

    private static string VerifyDamageRequiresCurrentDeprivation()
    {
        Require(InvokeShouldApplyDeprivationDamage(
                currentNeed: 0f,
                burden: 70f,
                now: 100f,
                nextDamageAt: 100f),
            "active deprivation did not permit its due damage tick");
        Require(!InvokeShouldApplyDeprivationDamage(
                currentNeed: 35f,
                burden: 100f,
                now: 200f,
                nextDamageAt: 100f),
            "historical burden continued damaging a character after eating");
        Require(!InvokeShouldApplyDeprivationDamage(
                currentNeed: 0f,
                burden: 69.9f,
                now: 200f,
                nextDamageAt: 100f),
            "damage fired below the authored burden threshold");
        Require(!InvokeShouldApplyDeprivationDamage(
                currentNeed: 0f,
                burden: 100f,
                now: 99.9f,
                nextDamageAt: 100f),
            "damage fired before its interval elapsed");
        return "damage requires current need<20, burden>=70, and due interval";
    }

    private static bool InvokeShouldApplyDeprivationDamage(
        float currentNeed,
        float burden,
        float now,
        float nextDamageAt)
    {
        System.Reflection.MethodInfo method = typeof(CharacterDeprivationRuntime)
            .GetMethod(
                "ShouldApplyDeprivationDamage",
                System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);
        Require(method != null, "deprivation damage authority predicate is missing");
        object result = method.Invoke(
            null,
            new object[] { currentNeed, burden, now, nextDamageAt });
        return result is bool allowed && allowed;
    }

    private static string VerifyBreakdownThresholds()
    {
        float low = CharacterDeprivationRuntime.GetBreakdownChance(70f, 0.5f);
        float high = CharacterDeprivationRuntime.GetBreakdownChance(100f, 0.5f);
        float miserable = CharacterDeprivationRuntime.GetBreakdownChance(85f, 0f);
        float stable = CharacterDeprivationRuntime.GetBreakdownChance(85f, 1f);
        CharacterAiPersonality reckless = new CharacterAiPersonality { selfCare = 0.25f, patience = 0.25f };
        CharacterAiPersonality composed = new CharacterAiPersonality { selfCare = 2f, patience = 2f };
        float recklessChance = CharacterDeprivationRuntime.GetBreakdownChance(85f, 0.5f, reckless);
        float composedChance = CharacterDeprivationRuntime.GetBreakdownChance(85f, 0.5f, composed);
        Require(low > 0f && high > low, "burden did not increase breakdown chance");
        Require(miserable > stable, "mood did not modify breakdown chance");
        Require(recklessChance > composedChance, "personality did not modify breakdown chance");
        Require(!CharacterDeprivationRuntime.IsForcedBreakdown(100f, 29.9f),
            "forced breakdown fired before 30 seconds");
        Require(CharacterDeprivationRuntime.IsForcedBreakdown(100f, 30f),
            "forced breakdown did not fire at 30 seconds");
        return $"chance={low:P1}->{high:P1}; personality={composedChance:P1}->{recklessChance:P1}";
    }

    private static string VerifyThirstNeedAndTerrain()
    {
        ICharacterNeedDefinitionCatalog catalog = CharacterAiEditorTestDependencies.AuthoredGameplay;
        Require(catalog.TryGet(CharacterCondition.THIRST, out CharacterNeedDefinition thirst)
                && thirst.DisplayName == "갈증",
            "thirst need is not registered");
        GridCell cell = new GridCell(Vector2Int.zero);
        cell.SetTerrainType(GridCellTerrainType.ShallowWater);
        Require(cell.IsWalkableArea && cell.TerrainMoveSpeedMultiplier < 1f,
            "shallow water should be slow but walkable");
        cell.SetTerrainType(GridCellTerrainType.DeepWater);
        Require(!cell.IsWalkableArea, "deep water should block movement");
        return thirst.DisplayName;
    }

    private static string VerifyFilthCleaningWork()
    {
        WorldFilthSnapshot filth = new WorldFilthSnapshot(
            "filth:test",
            WorldFilthType.Waste,
            12f,
            new Vector2Int(3, 2),
            "owner",
            0.8f,
            false);
        Require(Mathf.Approximately(filth.RequiredCleaningWork, 144f),
            "filth work did not scale with amount");
        Require(filth.InfectionRisk > 0.5f, "filth infection risk was lost");
        return $"work={filth.RequiredCleaningWork:0}";
    }

    private static string VerifySharedWaterPatchSave()
    {
        WildlifeHabitatPatch patch = new WildlifeHabitatPatch(
            "wildlife-habitat:test:water",
            WildlifeHabitatType.Water,
            new Vector2Int(5, 0),
            2,
            20f,
            7f,
            0f,
            0.1f,
            linkedWaterSourceId: "water:00000001");
        WildlifeHabitatPatchSaveData save = patch.Capture();
        WildlifeHabitatPatch restored = WildlifeHabitatPatch.FromSave(save);
        Require(restored != null && restored.LinkedWaterSourceId == "water:00000001",
            "linked water source id did not round-trip");
        Require(Mathf.Approximately(restored.CurrentResource, 7f),
            "shared water resource amount did not round-trip");
        return restored.LinkedWaterSourceId;
    }

    private static string VerifyHumanoidCorpseContract()
    {
        ResourceDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        Require(catalog.TryGetDefinition(
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                out DungeonItemDefinition corpse),
            "humanoid corpse definition is missing");
        Require(corpse.MaxStack == 1 && corpse.UnitWeight >= 20f,
            "humanoid corpse is not unique and heavy");
        Require(catalog.TryGetDefinition(
                DarkSurvivalItemDefinitions.HumanoidMeatItemId,
                out DungeonItemDefinition meat)
            && meat.StockCategory == StockCategory.Food,
            "humanoid meat is not a food item");
        Require(catalog.TryGetDefinition(DarkSurvivalItemDefinitions.BoneItemId, out _),
            "bone byproduct is missing");
        return $"corpseStack={corpse.MaxStack}; weight={corpse.UnitWeight:0.#}";
    }

    private static string VerifyCurrentRootRoundTrip()
    {
        Require(DungeonGameSaveData.CurrentVersion == 24, "game save version is not V24");
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonDarkSurvivalSaveData darkSurvival = new DungeonDarkSurvivalSaveData();
        darkSurvival.characters.Add(new CharacterDeprivationState
        {
            characterId = "owner",
            infectionBurden = 27f,
            burdens = Enum.GetValues(typeof(DeprivationKind))
                .Cast<DeprivationKind>()
                .Select(kind => new DeprivationBurdenSaveData
                {
                    kind = kind,
                    burden = kind == DeprivationKind.Thirst ? 74f : 0f
                })
                .ToList(),
            breakdown = new CharacterBreakdownState
            {
                active = true,
                kind = CharacterBreakdownKind.DesperateDrink,
                cause = DeprivationKind.Thirst
            },
            breakdownGeneration = 1,
            dispatchedBreakdownGeneration = 1
        });
        darkSurvival.filth.Add(new WorldFilthSaveData
        {
            filthId = "filth:00000001",
            type = WorldFilthType.Waste,
            amount = 11f,
            gridX = 2,
            gridY = 1
        });
        DungeonSaveSectionPayload.Write(
            save,
            DarkSurvivalSaveSection.Id,
            DungeonDarkSurvivalSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            darkSurvival);
        string json = JsonUtility.ToJson(save);
        DungeonGameSaveData restored = JsonUtility.FromJson<DungeonGameSaveData>(json);
        DungeonDarkSurvivalSaveData restoredDarkSurvival =
            DungeonSaveSectionPayload.ReadOrNew<DungeonDarkSurvivalSaveData>(
                restored,
                DarkSurvivalSaveSection.Id);
        Require(
            restored != null && restored.version == DungeonGameSaveData.CurrentVersion,
            $"V{DungeonGameSaveData.CurrentVersion} root did not round-trip");
        Require(restoredDarkSurvival.characters.Single().burdens
                .Single(entry => entry.kind == DeprivationKind.Thirst).burden == 74f,
            "deprivation burden did not round-trip");
        Require(restoredDarkSurvival.filth.Single().amount == 11f,
            "filth did not round-trip");
        return $"json={json.Length} chars";
    }

    private static string VerifyDeprivationAuthority()
    {
        List<string> errors = CharacterDeprivationAuthorityDebugScenarios.RunAll();
        Require(errors.Count == 0, string.Join(" | ", errors));
        return "typed aggregate, strict restore, and exactly-once transition verified";
    }

    private static string VerifySeparateBreakdownActions()
    {
        Type[] actionTypes =
        {
            typeof(AIDesperateRelief),
            typeof(AIDesperateDrink),
            typeof(AIDesperateEat),
            typeof(AICollapse),
            typeof(AIViolentBreakdown)
        };
        Require(actionTypes.All(type => type.IsSubclassOf(typeof(AIDeprivationBreakdownAction))),
            "a breakdown action is not an independent AIActionSet");
        Require(actionTypes.Distinct().Count() == 5, "breakdown actions were collapsed into one type");
        return string.Join(", ", actionTypes.Select(type => type.Name));
    }

    private static string VerifyPermanentTabooSocialMemory()
    {
        GameObject witnessObject = CharacterAiPlanDebugFixtures.CreateActorObject("TabooMemoryWitness");
        GameObject sourceObject = CharacterAiPlanDebugFixtures.CreateActorObject("TabooMemorySource");
        try
        {
            CharacterActor witness = witnessObject.GetComponent<CharacterActor>();
            CharacterActor source = sourceObject.GetComponent<CharacterActor>();
            CharacterSocialMemory memory = witnessObject.GetComponent<CharacterSocialMemory>();
            memory.Bind(witness);
            memory.RememberCharacterExperience(source, -1f, "금기의 포식을 목격함", 0f);
            float before = memory.GetRelationshipSentiment(source);
            CharacterSocialMemorySnapshot snapshot = memory.CaptureSnapshot();
            memory.RestoreSnapshot(snapshot);
            float after = memory.GetRelationshipSentiment(source);
            Require(before < -0.01f, "taboo witness did not form a negative relationship memory");
            Require(after < -0.01f, "permanent taboo relationship memory was lost on restore");
            Require(snapshot.recentRumors.Any(entry => entry != null && entry.remainingSeconds == 0f),
                "permanent taboo event was not included in the social-memory snapshot");
            return $"relationship {before:0.###}->{after:0.###}; rumors={snapshot.recentRumors.Count}";
        }
        finally
        {
            CharacterAiPlanDebugFixtures.DestroyProbeObject(witnessObject);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(sourceObject);
        }
    }

    private static void Run(string name, Func<string> scenario, List<string> errors, bool logSuccess)
    {
        try
        {
            string detail = scenario();
            if (logSuccess)
            {
                Debug.Log($"[DarkSurvival] {name}: {detail}");
            }
        }
        catch (Exception exception)
        {
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
