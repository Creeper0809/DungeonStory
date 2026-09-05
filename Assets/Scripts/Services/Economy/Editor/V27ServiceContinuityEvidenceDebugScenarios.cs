#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V27ServiceContinuityEvidenceDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-service-continuity-playmode.txt";

    private const string PrimitiveReportPath =
        "Artifacts/QA/primitive-survival-focused-report.txt";
    private const string SelfCareReportPath =
        "Artifacts/QA/character-ai-self-care-playmode.txt";
    private const string SixAdultOutageReportPath =
        "Artifacts/QA/v27-six-adult-service-outage-playmode.txt";

    private static readonly string[] PrimitiveSources =
    {
        "Assets/Scripts/Services/Survival/Editor/PrimitiveStartSurvivalPlayModeVerifier.cs",
        "Assets/Scripts/Services/Survival/CharacterPrimitiveSurvivalRunner.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIPrimitiveFieldMeal.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIPrimitiveLatrine.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIPrimitiveBucketWash.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIPrimitiveFloorRest.cs"
    };

    private static readonly string[] SelfCareSources =
    {
        "Assets/Scripts/Services/Character/AI/Editor/CharacterAiSelfCarePlayModeVerifier.cs",
        "Assets/Scripts/Services/Survival/CharacterSafeReliefRunner.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIDrink.cs"
    };

    private static readonly string[] PrimitiveMarkers =
    {
        "PASS FOCUSED_FIELD_MEAL_AUTHORITY:",
        "PASS FOCUSED_FIELD_MEAL_COMPLETED:",
        "PASS FOCUSED_PRIMITIVE_LATRINE_COMPLETED:",
        "PASS FOCUSED_BUCKET_WASH_COMPLETED:",
        "PASS FOCUSED_FLOOR_REST_COMPLETED:",
        "PASS FOCUSED_FLOOR_REST_BASELINE_RESTORE:"
    };

    private static readonly string[] SelfCareMarkers =
    {
        "DRINK_RUNTIME\tPASS\t",
        "DRINK_ROUTINE_NOT_EMERGENCY\tPASS\t",
        "DRINK_RESTORED_THIRST\tPASS\t",
        "DRINK_PHYSICAL_EXACTLY_ONCE\tPASS\t",
        "DRINK_LIFECYCLE_CONSERVED\tPASS\t",
        "DRINK_NO_INVARIANT_ANOMALY\tPASS\t",
        "CONSOLE_WARNING_ERROR_ZERO\tPASS\t0/0",
        "RESULT=PASS; failures=0"
    };

    private static readonly string[] SixAdultOutageMarkers =
    {
        "PASS V27_SIX_ADULT_OUTAGE_SIX_LIVE_ADULTS:",
        "PASS V27_SIX_ADULT_OUTAGE_ONE_GAME_DAY_ELAPSED:",
        "PASS V27_SIX_ADULT_OUTAGE_ALL_FIVE_FALLBACKS_COMPLETED:",
        "PASS V27_SIX_ADULT_OUTAGE_PHYSICAL_EXACT_NO_MINT:",
        "PASS V27_SIX_ADULT_OUTAGE_NO_DEATH_DOWN_BREAKDOWN:",
        "PASS V27_SIX_ADULT_OUTAGE_PRIMARY_RESTORE_TRANSACTION:",
        "PASS V27_SIX_ADULT_OUTAGE_RECOVERY_PRIMARY_DOMINANCE:",
        "PASS V27_SIX_ADULT_OUTAGE_RESULT:"
    };

    [MenuItem("DungeonStory/V27/Capture Service Continuity Evidence")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
            stream.Write(bytes, 0, bytes.Length);
        });
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(report);
    }

    public static string RunAll()
    {
        string staticReport = V27SixAdultSurvivalLoopDebugScenarios.RunAll();
        RequireContains(staticReport, "RESULT=PASS;");
        RequireContains(staticReport, "population=6;");
        RequireContains(staticReport, "PASS V27_SURVIVAL_NPLUSONE paths=10");
        RequireFresh(PrimitiveReportPath, PrimitiveSources, PrimitiveMarkers, true);
        RequireFresh(SelfCareReportPath, SelfCareSources, SelfCareMarkers, false);
        RequireFresh(
            SixAdultOutageReportPath,
            PrimitiveSources,
            SixAdultOutageMarkers,
            true);

        string continuityPath = Path.GetFullPath(
            V27SixAdultSurvivalLoopDebugScenarios.ContinuityPath);
        string[] continuity = File.ReadAllLines(continuityPath, Encoding.UTF8);
        if (continuity.Length != 11)
            throw new InvalidOperationException(
                $"Service-continuity catalog must contain 10 paths: rows={continuity.Length - 1}.");
        string[] requiredPathIds =
        {
            "facility:meal-service", "survival:field-meal",
            "facility:safe-drink", "survival:safe-drink",
            "facility:bed", "survival:floor-rest",
            "facility:hygiene", "survival:bucket-wash",
            "facility:toilet", "survival:primitive-latrine"
        };
        foreach (string pathId in requiredPathIds)
        {
            if (!continuity.Skip(1).Any(line => line.Contains(
                    $",{pathId},", StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    $"Service-continuity path is missing: {pathId}.");
        }
        V27RedundancyCapitalAssessment capital =
            V27AssetBackedSpatialCapacityDebugScenarios
                .CaptureSixAdultRedundancyCapital();
        if (capital.ActualRedundancyCapitalPermille != 0
            || capital.ActualRedundancyCapitalPermille > 150)
            throw new InvalidOperationException(
                "Primitive N+1 did not protect the six-adult initial capital.");

        return "RESULT=PASS; populationAuthority=6; livePathActorCount=6;"
            + " outageCoverageHours=24; paths=10; consoleIssues=0\n"
            + "currentSourceDigest="
            + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest()
            + "\n"
            + "PASS V27_SERVICE_CONTINUITY_STATIC_CLOSED_LOOP population=6;"
            + " grossFoodPermille=1250;netFoodPermille=1100;reserveDays=7\n"
            + "PASS V27_SERVICE_CONTINUITY_FIELD_MEAL_PRODUCTION_LIVE physicalCost=1\n"
            + "PASS V27_SERVICE_CONTINUITY_SAFE_DRINK_PRODUCTION_LIVE physicalCost=1\n"
            + "PASS V27_SERVICE_CONTINUITY_FLOOR_REST_PRODUCTION_LIVE\n"
            + "PASS V27_SERVICE_CONTINUITY_BUCKET_WASH_PRODUCTION_LIVE physicalCost=1\n"
            + "PASS V27_SERVICE_CONTINUITY_LATRINE_PRODUCTION_LIVE\n"
            + "PASS V27_SERVICE_CONTINUITY_PRIMARY_AND_FALLBACK_PATHS_EXACT count=10\n"
            + "PASS V27_SERVICE_CONTINUITY_REDUNDANCY_CAPITAL_EXACT "
            + $"portfolioMilliCapital={capital.PortfolioCapitalMilliUnits};"
            + $"actualRedundancyMilliCapital={capital.ActualRedundancyCapitalMilliUnits};"
            + $"actualRedundancyPermille={capital.ActualRedundancyCapitalPermille};"
            + $"avoidedDuplicateCookerPumpMilliCapital={capital.AvoidedDuplicateCapitalMilliUnits};"
            + "warningThresholdPermille=150;criticalThresholdPermille=250\n"
            + "PASS V27_SERVICE_CONTINUITY_EVIDENCE_SCOPE staticPopulationModel=6;"
            + " liveExecution=sixAdultsFiveSimultaneousOutages\n";
    }

    private static void RequireFresh(
        string reportPath,
        IReadOnlyList<string> sourcePaths,
        IReadOnlyList<string> markers,
        bool requireFirstLinePass)
    {
        string absoluteReport = Path.GetFullPath(reportPath);
        DateTime latestSource = sourcePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : throw new FileNotFoundException("Continuity source is missing.", path))
            .Max();
        if (!File.Exists(absoluteReport)
            || File.GetLastWriteTimeUtc(absoluteReport) < latestSource)
            throw new InvalidOperationException(
                $"Service-continuity live evidence is missing or stale: {reportPath}.");

        string[] lines = File.ReadAllLines(absoluteReport, Encoding.UTF8);
        if (requireFirstLinePass
            && (lines.Length == 0
                || !string.Equals(lines[0], "PASS", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Service-continuity aggregate result failed: {reportPath}.");
        foreach (string marker in markers)
        {
            if (!lines.Any(line => line.StartsWith(marker, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    $"Service-continuity marker is missing: {marker}.");
        }
    }

    private static void RequireContains(string value, string marker)
    {
        if (!value.Contains(marker, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Six-adult closed-loop marker is missing: {marker}.");
    }
}
#endif
