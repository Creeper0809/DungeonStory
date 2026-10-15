#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class G12GolemUpkeepPlayModeVerifier
{
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const string RequestVariable =
        "DUNGEONSTORY_U0_G12_GOLEM_UPKEEP_VERIFY";

    public static IEnumerator Verify(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        var report = new G12Report
        {
            scope = "U0 G12 Golem recharge, work wear, discharge, and surgical maintenance",
            oldSaveMigrationAdded = false,
            humanApprovalClaimed = false,
            trainingEligible = false
        };
        bool isolatedModeReady = true;
        try
        {
            Require(Application.isBatchMode
                    && DungeonPersistencePaths.IsIsolatedVerification
                    && string.Equals(
                        Environment.GetEnvironmentVariable(RequestVariable),
                        "1",
                        StringComparison.Ordinal),
                "G12 verification requires explicit isolated batch mode.");
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
            isolatedModeReady = false;
        }
        if (!isolatedModeReady)
        {
            Finish(report, warnings, errors);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        DungeonRuntimeLifetimeScope scope = null;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<
                    DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value =>
                    value != null && value.Container != null);
            if (scope == null)
                yield return null;
        }

        try
        {
            Require(scope != null, "Official runtime scope did not initialize.");

            V27CharacterPerformanceDebugScenarios.RunG12Focused();
            SurgeryOutcomeProductionEntryDebugScenarios
                .RunG12GolemOrThrow();
            report.surgeryParentHasMaterialLaborBody = true;
            report.surgeryParentHasGolemBeforeAfter = true;
            GameplayOutcomeLedgerSaveData ledger = scope.Container
                .Resolve<GameplayOutcomeLedger>()
                .CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot[] episodes = ledger.exactOutcomes
                .Concat(ledger.outbox)
                .Where(value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        EnvironmentOutcomeIds.GolemUpkeepEpisode.Value,
                        StringComparison.Ordinal))
                .ToArray();
            report.golemEpisodeCount = episodes.Length;
            report.wearEpisodeCount = CountKind(episodes, "wear");
            report.rechargeEpisodeCount = CountKind(episodes, "recharge");
            report.dischargeEpisodeCount = CountKind(episodes, "discharge");
            report.allEpisodesHaveExactChildren = episodes.All(value =>
                HasFact(value, EnvironmentOutcomeIds.StateBeforeFact.Value)
                && HasFact(value, EnvironmentOutcomeIds.StateAfterFact.Value)
                && HasFact(value, EnvironmentOutcomeIds.PhysicalChildFact.Value)
                && HasFact(value, EnvironmentOutcomeIds.WorkChildFact.Value)
                && HasFact(value, EnvironmentOutcomeIds.BodyChildFact.Value));
            Require(report.golemEpisodeCount == 4
                    && report.wearEpisodeCount == 2
                    && report.rechargeEpisodeCount == 1
                    && report.dischargeEpisodeCount == 1
                    && report.allEpisodesHaveExactChildren
                    && report.surgeryParentHasMaterialLaborBody
                    && report.surgeryParentHasGolemBeforeAfter,
                "G12 durable episode set or child evidence is incomplete.");
            Require(warnings.Count == 0 && errors.Count == 0,
                "Unexpected runtime Console warning/error was observed.");
            report.passed = true;
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
        }
        Finish(report, warnings, errors);
    }

    private static void Finish(
        G12Report report,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        report.warningCount = warnings.Count;
        report.errorCount = errors.Count;
        report.warnings = warnings.ToArray();
        report.errors = errors.ToArray();
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
            EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private static int CountKind(
        IEnumerable<GameplayOutcomeSnapshot> outcomes,
        string kind) => outcomes.Count(value => value.facts.Any(fact =>
            fact != null
            && string.Equals(
                fact.factId,
                EnvironmentOutcomeIds.EpisodeKindFact.Value,
                StringComparison.Ordinal)
            && string.Equals(fact.value, kind, StringComparison.Ordinal)));

    private static bool HasFact(
        GameplayOutcomeSnapshot outcome,
        string factId) => outcome.facts.Any(value => value != null
            && string.Equals(value.factId, factId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(value.value));

    private static void WriteNew(string name, string content)
    {
        string root = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("G12 output directory is missing.");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, name);
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition)
            throw new InvalidOperationException(detail);
    }

    [Serializable]
    private sealed class G12Report
    {
        public string scope;
        public bool passed;
        public int golemEpisodeCount;
        public int wearEpisodeCount;
        public int rechargeEpisodeCount;
        public int dischargeEpisodeCount;
        public bool allEpisodesHaveExactChildren;
        public bool surgeryParentHasMaterialLaborBody;
        public bool surgeryParentHasGolemBeforeAfter;
        public bool oldSaveMigrationAdded;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
        public int warningCount;
        public int errorCount;
        public string[] warnings = Array.Empty<string>();
        public string[] errors = Array.Empty<string>();
        public string failure = string.Empty;
    }
}
#endif
