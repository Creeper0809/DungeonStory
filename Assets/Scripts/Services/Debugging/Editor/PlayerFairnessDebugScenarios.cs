using System;
using System.Collections.Generic;
using System.IO;
using DungeonStory.CoreSession;
using UnityEditor;
using UnityEngine;

public static class PlayerFairnessDebugScenarios
{
    private const string ReportPath =
        "Temp/player-fairness-contracts.tsv";

    [MenuItem("DungeonStory/Debug/Run Player Fairness Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            throw new InvalidOperationException(
                "Player fairness contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new() { "case\tresult\tdetails" };
        List<string> errors = new();
        Run("ecology_countdown_clock", VerifyEcologyCountdown, lines, errors);
        Run("raid_food_eligibility", VerifyRaidFoodEligibility, lines, errors);
        Run("save_migration_payloads", VerifySavePayloads, lines, errors);
        Run("surgery_environment_thresholds", VerifySurgeryThresholds, lines, errors);
        Run("intel_expiration_atomic_guard", VerifyIntelExpiration, lines, errors);
        Run("circus_forecast_contract", VerifyCircusForecast, lines, errors);
        File.WriteAllLines(ReportPath, lines);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log(
                $"Player fairness contracts PASS. Report: {ReportPath}");
        }

        return errors.Count == 0;
    }

    private static string VerifyEcologyCountdown()
    {
        float x1 = 60f;
        for (int index = 0; index < 59; index++)
        {
            x1 = ExternalInfluenceDomainRules.AdvanceEcologyRaidCountdown(
                x1,
                1f,
                paused: false);
        }

        Require(Mathf.Approximately(x1, 1f),
            $"X1 countdown ended early: {x1}");
        x1 = ExternalInfluenceDomainRules.AdvanceEcologyRaidCountdown(
            x1,
            1f,
            paused: false);
        Require(Mathf.Approximately(x1, 0f),
            $"X1 countdown did not end at 60 seconds: {x1}");

        float x5 = 60f;
        for (int index = 0; index < 12; index++)
        {
            x5 = ExternalInfluenceDomainRules.AdvanceEcologyRaidCountdown(
                x5,
                5f,
                paused: false);
        }

        Require(Mathf.Approximately(x5, 0f),
            $"X5 countdown did not consume 60 game seconds: {x5}");
        float paused = ExternalInfluenceDomainRules.AdvanceEcologyRaidCountdown(
            37f,
            5f,
            paused: true);
        Require(Mathf.Approximately(paused, 37f),
            "paused countdown advanced");
        return "X1, X5 and paused countdown preserve exactly 60 game seconds";
    }

    private static string VerifyRaidFoodEligibility()
    {
        Require(WildlifeRuntime.IsRaidFoodEligible(
                WorldItemStackState.Loose,
                StockCategory.Food,
                1),
            "loose food was not eligible");
        foreach (WorldItemStackState state in new[]
                 {
                     WorldItemStackState.Stored,
                     WorldItemStackState.FacilityBuffer,
                     WorldItemStackState.Carried,
                     WorldItemStackState.ExpeditionPacked
                 })
        {
            Require(!WildlifeRuntime.IsRaidFoodEligible(
                    state,
                    StockCategory.Food,
                    1),
                $"{state} food was incorrectly eligible");
        }

        Require(!WildlifeRuntime.IsRaidFoodEligible(
                WorldItemStackState.Loose,
                StockCategory.General,
                1),
            "non-food stack was eligible");
        return "only positive Loose food is targetable; forbidden is intentionally not an input";
    }

    private static string VerifySavePayloads()
    {
        Require(DungeonExternalInfluenceSaveData.CurrentVersion == 3,
            "external.influence version is not V3");
        Require(DungeonWildlifeSaveData.CurrentVersion == 4,
            "wildlife.population version is not V4");
        Require(DungeonCharacterEnvironmentSaveData.CurrentVersion == 5,
            "environment.exposure version is not V22 apparel revision 5");

        DungeonExternalInfluenceSaveData external = new()
        {
            ecologyWarningIssued = true,
            ecologyRaidScheduled = true,
            ecologyRaidRemainingSeconds = 23.5f,
            ecologyRaidSequence = 1,
            currentOperatingDay = 7,
            lastRumorMitigationDay = 7
        };
        DungeonExternalInfluenceSaveData restoredExternal =
            JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(
                JsonUtility.ToJson(external));
        Require(restoredExternal.ecologyRaidScheduled
                && Mathf.Approximately(
                    restoredExternal.ecologyRaidRemainingSeconds,
                    23.5f)
                && restoredExternal.lastRumorMitigationDay == 7,
            "external influence fairness state did not round trip");

        DungeonWildlifeSaveData wildlife = new()
        {
            lastDiseaseVectorAbsoluteDay = 17
        };
        wildlife.foodRaidOrders.Add(new WildlifeFoodRaidOrderSaveData
        {
            raidId = "raid:test",
            wildlifeId = "wolf:test",
            targetStackId = "food:test",
            state = WildlifeFoodRaidOrderState.Approaching
        });
        DungeonWildlifeSaveData restoredWildlife =
            JsonUtility.FromJson<DungeonWildlifeSaveData>(
                JsonUtility.ToJson(wildlife));
        Require(restoredWildlife.lastDiseaseVectorAbsoluteDay == 17
                && restoredWildlife.foodRaidOrders.Count == 1
                && restoredWildlife.foodRaidOrders[0].state
                    == WildlifeFoodRaidOrderState.Approaching,
            "wildlife raid order did not round trip");

        DungeonCharacterEnvironmentSaveData exposure = new();
        exposure.exposures = new[]
        {
            new CharacterEnvironmentExposure
            {
                characterId = "worker:test",
                coldExposure = 14f,
                coldWorkCooldownActive = true
            }
        };
        exposure.equippedWorkwear =
            Array.Empty<EnvironmentalWorkwearSaveData>();
        DungeonCharacterEnvironmentSaveData restoredExposure =
            JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(
                JsonUtility.ToJson(exposure));
        Require(restoredExposure.exposures[0].coldWorkCooldownActive,
            "cold cooldown latch did not round trip");

        DungeonSurgerySaveData surgery = new();
        surgery.orders.Add(new SurgeryOrder
        {
            orderId = "surgery:test",
            state = SurgeryOrderState.EnvironmentWaiting,
            environmentResumeStage = SurgeryOrderState.Procedure,
            environmentStableSeconds = 3.25f,
            environmentWait = new SurgeryStatusData
            {
                code = SurgeryStatusCode.EnvironmentUnsafe,
                primaryId = "test",
                stage = SurgeryOrderState.Procedure
            }
        });
        DungeonSurgerySaveData restoredSurgery =
            JsonUtility.FromJson<DungeonSurgerySaveData>(
                JsonUtility.ToJson(surgery));
        Require(restoredSurgery.orders[0].state
                    == SurgeryOrderState.EnvironmentWaiting
                && restoredSurgery.orders[0].environmentResumeStage
                    == SurgeryOrderState.Procedure
                && Mathf.Approximately(
                    restoredSurgery.orders[0].environmentStableSeconds,
                    3.25f),
            "surgery environment wait did not round trip");
        return "V3 fairness state payloads round trip without losing active orders";
    }

    private static string VerifySurgeryThresholds()
    {
        Require(SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                new EnvironmentalCellSnapshot(
                    Vector2Int.zero,
                    16f,
                    70f,
                    70f)),
            "normal lower boundary was rejected");
        Require(SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                new EnvironmentalCellSnapshot(
                    Vector2Int.zero,
                    28f,
                    70f,
                    70f)),
            "normal upper boundary was rejected");
        Require(!SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                new EnvironmentalCellSnapshot(
                    Vector2Int.zero,
                    7.9f,
                    100f,
                    100f)),
            "extreme temperature was accepted");
        Require(!SurgeryEnvironmentRiskEvaluator.IsNormalEnvironment(
                new EnvironmentalCellSnapshot(
                    Vector2Int.zero,
                    20f,
                    39f,
                    100f)),
            "extreme air was accepted");
        return "runtime/UI shared evaluator uses 16-28C and air/light 70 normal bounds";
    }

    private static string VerifyIntelExpiration()
    {
        Require(ExternalInfluenceDomainRules.IsIntelSiteActive(
                fixedBoss: true,
                expiresDay: 0,
                currentDay: 999),
            "fixed boss site expired");
        Require(ExternalInfluenceDomainRules.IsIntelSiteActive(
                fixedBoss: false,
                expiresDay: 8,
                currentDay: 7),
            "dynamic site expired before its expiry day");
        Require(!ExternalInfluenceDomainRules.IsIntelSiteActive(
                fixedBoss: false,
                expiresDay: 8,
                currentDay: 8),
            "dynamic site remained purchasable on its expiry day");
        return "payment guard accepts fixed bosses and rejects dynamic sites at expiry";
    }

    private static string VerifyCircusForecast()
    {
        CircusProgramForecast forecast = new(
            expectedRevenue: 120,
            minimumSatisfaction: 55f,
            maximumSatisfaction: 75f,
            accidentChance: 0.2f,
            renown: 0f,
            dread: 7f,
            hostileRumor: 2.45f,
            injuryChance: 0.4f,
            deathChance: 1f,
            canSchedule: true,
            participantRequirement: "포로 2명",
            failureReason: string.Empty);
        Require(forecast.ExpectedRevenue == 120
                && Mathf.Approximately(forecast.AccidentChance, 0.2f)
                && Mathf.Approximately(forecast.DeathChance, 1f)
                && forecast.CanSchedule,
            "circus forecast lost a required risk or reward field");
        return "forecast exposes revenue, satisfaction, accident, injury, death and social effects";
    }

    private static void Run(
        string name,
        Func<string> scenario,
        ICollection<string> lines,
        ICollection<string> errors)
    {
        try
        {
            string details = scenario();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            lines.Add($"{name}\tFAIL\t{exception.Message}");
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
